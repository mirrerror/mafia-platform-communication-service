using MafiaCommunicationService.Models;

namespace MafiaCommunicationService.Middleware;

public class RequestThrottlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly SemaphoreSlim _semaphore;
    private readonly int _requestTimeoutMilliseconds;
    private readonly ILogger<RequestThrottlingMiddleware> _logger;

    public RequestThrottlingMiddleware(RequestDelegate next, SemaphoreSlim semaphore, ILogger<RequestThrottlingMiddleware> logger)
    {
        _next = next;
        _semaphore = semaphore;
        _logger = logger;

        var timeoutSecondsStr = Environment.GetEnvironmentVariable("REQUEST_TIMEOUT_SECONDS") ?? "30";
        if (!int.TryParse(timeoutSecondsStr, out var timeoutSeconds))
        {
            timeoutSeconds = 30;
        }
        _requestTimeoutMilliseconds = timeoutSeconds * 1000;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!await _semaphore.WaitAsync(TimeSpan.FromSeconds(1)))
        {
            _logger.LogWarning("Service unavailable (503). Concurrency limit reached. Request from {IpAddress} to {Path} was throttled.", 
                context.Connection.RemoteIpAddress, context.Request.Path);
                
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsJsonAsync(new ErrorResponse("CONCURRENCY_LIMIT_REACHED", "The service is temporarily overloaded. Please try again later."));
            return;
        }

        try
        {
            using var timeoutCts = new CancellationTokenSource(_requestTimeoutMilliseconds);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, context.RequestAborted);
            
            context.RequestAborted = linkedCts.Token;

            await _next(context);
        }
        catch (OperationCanceledException)
        {
            if (!context.Response.HasStarted)
            {
                _logger.LogWarning("Request timeout (408). Request from {IpAddress} to {Path} exceeded {Timeout}ms.",
                    context.Connection.RemoteIpAddress, context.Request.Path, _requestTimeoutMilliseconds);

                context.Response.StatusCode = StatusCodes.Status408RequestTimeout;
                await context.Response.WriteAsJsonAsync(new ErrorResponse("REQUEST_TIMEOUT", "The request took too long to process."));
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }
}