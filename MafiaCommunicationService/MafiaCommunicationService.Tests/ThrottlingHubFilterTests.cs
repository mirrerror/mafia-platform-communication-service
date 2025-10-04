using System.Reflection;
using MafiaCommunicationService.Filters;
using Microsoft.AspNetCore.SignalR;
using Moq;

namespace MafiaCommunicationService.Tests;

public class ThrottlingHubFilterTests
{
    public ThrottlingHubFilterTests()
    {
        Environment.SetEnvironmentVariable("REQUEST_TIMEOUT_SECONDS", "1");
    }

    private static HubInvocationContext CreateHubInvocationContext()
    {
        var mockHubCallerContext = new Mock<HubCallerContext>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        var mockHub = new Mock<Hub>();
        var methodInfo = typeof(ThrottlingHubFilterTests).GetMethod(nameof(CreateHubInvocationContext), BindingFlags.NonPublic | BindingFlags.Static);

        return new HubInvocationContext(mockHubCallerContext.Object, mockServiceProvider.Object, mockHub.Object, methodInfo!, []);
    }

    [Fact]
    public void Constructor_WhenEnvVarIsNotSet_UsesDefaultTimeout()
    {
        Environment.SetEnvironmentVariable("REQUEST_TIMEOUT_SECONDS", null);
        var semaphore = new SemaphoreSlim(1, 1);

        var filter = new ThrottlingHubFilter(semaphore);

        var timeoutField = typeof(ThrottlingHubFilter).GetField("_requestTimeoutMilliseconds", BindingFlags.NonPublic | BindingFlags.Instance);
        var timeoutValue = (int)timeoutField!.GetValue(filter)!;
        Assert.Equal(30000, timeoutValue);
    }

    [Fact]
    public void Constructor_WhenEnvVarIsInvalid_UsesDefaultTimeout()
    {
        Environment.SetEnvironmentVariable("REQUEST_TIMEOUT_SECONDS", "invalid-value");
        var semaphore = new SemaphoreSlim(1, 1);

        var filter = new ThrottlingHubFilter(semaphore);

        var timeoutField = typeof(ThrottlingHubFilter).GetField("_requestTimeoutMilliseconds", BindingFlags.NonPublic | BindingFlags.Instance);
        var timeoutValue = (int)timeoutField!.GetValue(filter)!;
        Assert.Equal(30000, timeoutValue);
    }

    [Fact]
    public async Task InvokeMethodAsync_WhenConcurrencySlotIsAvailable_CallsNext()
    {
        Environment.SetEnvironmentVariable("REQUEST_TIMEOUT_SECONDS", "1");
        var semaphore = new SemaphoreSlim(1, 1);
        var filter = new ThrottlingHubFilter(semaphore);
        var hubInvocationContext = CreateHubInvocationContext();
        var next = new Func<HubInvocationContext, ValueTask<object?>>(_ => new ValueTask<object?>(new object()));

        var result = await filter.InvokeMethodAsync(hubInvocationContext, next);

        Assert.NotNull(result);
        Assert.Equal(1, semaphore.CurrentCount);
    }
    
    [Fact]
    public async Task InvokeMethodAsync_WhenConcurrencyLimitIsReached_ThrowsHubException()
    {
        var semaphore = new SemaphoreSlim(1, 1);
        await semaphore.WaitAsync();

        var filter = new ThrottlingHubFilter(semaphore);
        var hubInvocationContext = CreateHubInvocationContext();
        var next = new Func<HubInvocationContext, ValueTask<object?>>(_ => new ValueTask<object?>(new object()));

        var exception = await Assert.ThrowsAsync<HubException>(() => filter.InvokeMethodAsync(hubInvocationContext, next).AsTask());
        Assert.Contains("overloaded", exception.Message);
        
        semaphore.Release();
    }
    
    [Fact]
    public async Task InvokeMethodAsync_WhenInvocationTimesOut_ThrowsHubException()
    {
        var semaphore = new SemaphoreSlim(1, 1);
        var filter = new ThrottlingHubFilter(semaphore);
        var hubInvocationContext = CreateHubInvocationContext();
        var next = new Func<HubInvocationContext, ValueTask<object?>>(async _ =>
        {
            await Task.Delay(2000);
            return null;
        });

        var exception = await Assert.ThrowsAsync<HubException>(() => filter.InvokeMethodAsync(hubInvocationContext, next).AsTask());
        Assert.Contains("REQUEST_TIMEOUT", exception.Message);
        Assert.Equal(1, semaphore.CurrentCount);
    }
    
    [Fact]
    public async Task InvokeMethodAsync_WhenExceptionInNext_ReleasesSemaphore()
    {
        var semaphore = new SemaphoreSlim(1, 1);
        var filter = new ThrottlingHubFilter(semaphore);
        var hubInvocationContext = CreateHubInvocationContext();
        var next = new Func<HubInvocationContext, ValueTask<object?>>(_ => throw new InvalidOperationException("Test Exception"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => filter.InvokeMethodAsync(hubInvocationContext, next).AsTask());
        Assert.Equal(1, semaphore.CurrentCount);
    }
}

