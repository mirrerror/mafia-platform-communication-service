using Microsoft.AspNetCore.SignalR;

namespace MafiaCommunicationService.Filters;

public class ThrottlingHubFilter : IHubFilter
{
    private readonly SemaphoreSlim _semaphore;
    private readonly int _requestTimeoutMilliseconds;

    public ThrottlingHubFilter(SemaphoreSlim semaphore)
    {
        _semaphore = semaphore;

        var timeoutSecondsStr = Environment.GetEnvironmentVariable("REQUEST_TIMEOUT_SECONDS") ?? "30";
        if (!int.TryParse(timeoutSecondsStr, out var timeoutSeconds))
        {
            timeoutSeconds = 30;
        }
        _requestTimeoutMilliseconds = timeoutSeconds * 1000;
    }

    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        if (!await _semaphore.WaitAsync(TimeSpan.FromSeconds(1)))
        {
            throw new HubException("CONCURRENCY_LIMIT_REACHED: The service is temporarily overloaded. Please try again later.");
        }

        try
        {
            using var timeoutCts = new CancellationTokenSource(_requestTimeoutMilliseconds);
            
            var originalTask = next(invocationContext);
            var delayTask = Task.Delay(_requestTimeoutMilliseconds, timeoutCts.Token);

            var completedTask = await Task.WhenAny(originalTask.AsTask(), delayTask);

            if (completedTask == delayTask)
            {
                throw new HubException("REQUEST_TIMEOUT: The hub operation took too long and was terminated.");
            }
            
            await timeoutCts.CancelAsync(); 
            return await originalTask;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}