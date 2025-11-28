using Grpc.Core;
using MafiaCommunicationService.Protos;
using System.Net;

namespace MafiaCommunicationService.Services;

public class ServiceRegistryClient
{
    private readonly ILogger<ServiceRegistryClient> _logger;
    private readonly RegistrationService.RegistrationServiceClient? _grpcClient;

    private readonly string _serviceId;
    private readonly string _serviceHost;
    private readonly int _restPort;
    private readonly int _rpcPort;
    private readonly string _topicName;
    private readonly string _subscribedTopics;

    public string? InstanceId { get; private set; }

    public ServiceRegistryClient(
        ILogger<ServiceRegistryClient> logger, 
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        
        _grpcClient = serviceProvider.GetService<RegistrationService.RegistrationServiceClient>();

        _serviceId = Environment.GetEnvironmentVariable("SERVICE_ID") ?? "mafia-communication-service";
        _topicName = Environment.GetEnvironmentVariable("SERVICE_TOPIC") ?? "chat-topic";
        _subscribedTopics = Environment.GetEnvironmentVariable("SUBSCRIBED_TOPICS") ?? "";

        _serviceHost = "localhost";
        var hostnameFromEnv = Environment.GetEnvironmentVariable("HOSTNAME");

        if (!string.IsNullOrEmpty(hostnameFromEnv))
        {
            _serviceHost = hostnameFromEnv;
             _logger.LogInformation("Resolved hostname from env: {Hostname}", _serviceHost);
        }
        else
        {
            try { _serviceHost = Dns.GetHostName(); }
            catch { /* Ignore */ }
        }
            
        var portStr = Environment.GetEnvironmentVariable("SERVICE_PORT");
        if (!int.TryParse(portStr, out _restPort)) _restPort = 8080;

        var rpcPortStr = Environment.GetEnvironmentVariable("RPC_PORT");
        if (!int.TryParse(rpcPortStr, out _rpcPort)) _rpcPort = 6000;
        
        _logger.LogInformation("ServiceRegistryClient initialized. ServiceId: {Id}", _serviceId);
    }

    public async Task RegisterAsync()
    {
        if (_grpcClient == null)
        {
            _logger.LogWarning("gRPC Client is null (DISCOVERY_SERVICE_GRPC_URL might be missing). Skipping registration.");
            return;
        }

        try
        {
            var request = new RegisterRequest
            {
                ServiceId = _serviceId,
                Host = _serviceHost,
                RestPort = _restPort,
                RpcPort = _rpcPort,
                TopicName = _topicName
            };

            request.Metadata.Add("language", "csharp");
            
            if (!string.IsNullOrEmpty(_subscribedTopics))
            {
                request.Metadata.Add("subscribedTopics", _subscribedTopics);
            }

            _logger.LogInformation("Sending gRPC Registration with metadata (Topics: {Topics})...", _subscribedTopics);
            var response = await _grpcClient.RegisterAsync(request);

            InstanceId = response.InstanceId;
            _logger.LogInformation("Service registered! Instance ID: {InstanceId}", InstanceId);
        }
        catch (RpcException ex)
        {
            InstanceId = null;
            _logger.LogError(ex, "gRPC Error during service registration: {Status}", ex.Status);
        }
        catch (Exception ex)
        {
            InstanceId = null;
            _logger.LogError(ex, "Unexpected error during service registration.");
        }
    }

    public async Task DeregisterAsync()
    {
        if (string.IsNullOrEmpty(InstanceId) || _grpcClient == null) return;

        try
        {
            var response = await _grpcClient.DeregisterAsync(new DeregisterRequest { InstanceId = InstanceId });
            _logger.LogInformation("Service deregistered. Status: {Status}", response.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during service deregistration.");
        }
    }
}