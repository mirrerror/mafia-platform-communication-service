namespace MafiaCommunicationService.Services;

public class HeartbeatService(ServiceRegistryClient registryClient, ILogger<HeartbeatService> logger)
    : BackgroundService
{
    private readonly TimeSpan _heartbeatInterval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Heartbeat service starting.");

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await registryClient.SendHeartbeatAsync();
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "An unhandled error occurred in the heartbeat service loop.");
                }

                await Task.Delay(_heartbeatInterval, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when the service is stopping
        }
        finally
        {
            logger.LogInformation("Heartbeat service stopping.");
        }
    }
}