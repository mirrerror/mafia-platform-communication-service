using System.Collections.Concurrent;
using System.Text.Json;
using Grpc.Core;
using MafiaCommunicationService.Hubs;
using MafiaCommunicationService.Models;
using MafiaCommunicationService.Protos;
using Microsoft.AspNetCore.SignalR;

namespace MafiaCommunicationService.Services;

public class GrpcSubscriberService(
    ILogger<GrpcSubscriberService> logger,
    IHubContext<ChatHub> hubContext,
    IChatService chatService) : MessageSubscriber.MessageSubscriberBase
{
    
    private static readonly ConcurrentDictionary<string, string> PendingTransactions = new();
    
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public override async Task<MessageResponse> ReceiveMessage(MessageRequest request, ServerCallContext context)
    {
        logger.LogInformation("Received gRPC Message: {Payload}", request.Payload);

        try
        {
            await hubContext.Clients.All.SendAsync("ReceiveSystemNotification", new 
            { 
                Type = "System", 
                Content = request.Payload, 
                Timestamp = DateTime.UtcNow 
            });
            
            return new MessageResponse { Acknowledged = true };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to broadcast SignalR message");
            return new MessageResponse { Acknowledged = false };
        }
    }

    public override async Task<PrepareResponse> Prepare(PrepareRequest request, ServerCallContext context)
    {
        logger.LogInformation("2PC Prepare Phase for Tx {TxId}.", request.TransactionId);

        if (string.IsNullOrWhiteSpace(request.Payload))
        {
            logger.LogWarning("Tx {TxId} rejected: Empty payload", request.TransactionId);
            return new PrepareResponse { VoteCommit = false };
        }

        var canCommit = false;

        try
        {
            if (request.Payload.StartsWith("CreateLobby:"))
            {
                var jsonPart = request.Payload["CreateLobby:".Length..];
                
                var dto = JsonSerializer.Deserialize<LobbyCreationDto>(jsonPart, JsonOptions);
                
                if (dto == null || string.IsNullOrWhiteSpace(dto.LobbyId))
                {
                    logger.LogWarning("Tx {TxId} rejected: Invalid JSON or missing LobbyId", request.TransactionId);
                    return new PrepareResponse { VoteCommit = false };
                }

                var existingLobby = await chatService.GetLobbyAsync(dto.LobbyId);
                if (existingLobby == null)
                {
                    canCommit = true;
                }
                else
                {
                    logger.LogWarning("Tx {TxId} rejected: Lobby {LobbyId} already exists", request.TransactionId, dto.LobbyId);
                }
            }
            else
            {
                canCommit = true; 
            }
        }
        catch (JsonException jsonEx)
        {
            logger.LogError(jsonEx, "Tx {TxId} rejected: Invalid JSON format", request.TransactionId);
            canCommit = false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during Prepare phase for Tx {TxId}", request.TransactionId);
            canCommit = false;
        }

        if (canCommit)
        {
            PendingTransactions[request.TransactionId] = request.Payload;
        }

        return new PrepareResponse { VoteCommit = canCommit };
    }

    public override async Task<CommitResponse> Commit(CommitRequest request, ServerCallContext context)
    {
        logger.LogInformation("2PC Commit Phase for Tx {TxId}", request.TransactionId);

        if (PendingTransactions.TryRemove(request.TransactionId, out var payload))
        {
            try
            {
                if (payload.StartsWith("CreateLobby:"))
                {
                    var jsonPart = payload["CreateLobby:".Length..];
                    
                    var dto = JsonSerializer.Deserialize<LobbyCreationDto>(jsonPart, JsonOptions);

                    if (dto != null)
                    {
                        var existing = await chatService.GetLobbyAsync(dto.LobbyId);
                        if (existing == null)
                        {
                            await chatService.CreateNewLobbyAsync(dto);
                            
                            logger.LogInformation("Successfully committed Tx {TxId}: Created Lobby {LobbyId} with {Count} channels", 
                                request.TransactionId, dto.LobbyId, dto.PrivateChannels?.Count ?? 0);
                        }
                    }
                }
                
                await hubContext.Clients.All.SendAsync("ReceiveSystemNotification", new 
                { 
                    Type = "TransactionCommitted", 
                    TransactionId = request.TransactionId 
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to execute Commit for Tx {TxId}", request.TransactionId);
            }
        }
        else
        {
            logger.LogWarning("Received Commit for unknown or already processed Tx {TxId}", request.TransactionId);
        }

        return new CommitResponse { Acknowledged = true };
    }

    public override Task<RollbackResponse> Rollback(RollbackRequest request, ServerCallContext context)
    {
        logger.LogInformation("2PC Rollback Phase for Tx {TxId}", request.TransactionId);

        if (PendingTransactions.TryRemove(request.TransactionId, out _))
        {
            logger.LogInformation("Rolled back Tx {TxId}. Discarded payload", request.TransactionId);
        }
        else
        {
            logger.LogWarning("Received Rollback for unknown Tx {TxId}", request.TransactionId);
        }

        return Task.FromResult(new RollbackResponse { Acknowledged = true });
    }
    
}