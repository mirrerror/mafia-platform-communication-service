using System.Net;
using System.Text.Json;
using MafiaCommunicationService.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;

namespace MafiaCommunicationService.Tests;

public class ServiceRegistryClientTests : IDisposable
{
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<ILogger<ServiceRegistryClient>> _mockLogger;
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly HttpClient _httpClient;

    private const string TestDiscoveryUrl = "http://test-discovery-service.com";
    private const string DefaultServiceId = "mafia-communication-service";
    private const string DefaultHost = "localhost";
    private const int DefaultPort = 5000;


    public ServiceRegistryClientTests()
    {
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockLogger = new Mock<ILogger<ServiceRegistryClient>>();
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();

        _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        _mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(_httpClient);

        ClearEnvVars();
    }

    public void Dispose()
    {
        ClearEnvVars();
        GC.SuppressFinalize(this);
    }

    private static void ClearEnvVars()
    {
        Environment.SetEnvironmentVariable("DISCOVERY_SERVICE_URL", null);
        Environment.SetEnvironmentVariable("SERVICE_ID", null);
        Environment.SetEnvironmentVariable("SERVICE_HOST", null);
        Environment.SetEnvironmentVariable("SERVICE_PORT", null);
    }

    private ServiceRegistryClient CreateClient()
    {
        return new ServiceRegistryClient(_mockHttpClientFactory.Object, _mockLogger.Object);
    }

    private void VerifyLog<T>(Mock<ILogger<T>> loggerMock, LogLevel level, string messageContains, Times times) where T : class
    {
        loggerMock.Verify(
            log => log.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(messageContains)),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times);
    }

    private void VerifyLogException<T>(Mock<ILogger<T>> loggerMock, LogLevel level, string messageContains, Exception expectedException, Times times) where T : class
    {
        loggerMock.Verify(
            log => log.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(messageContains)),
                expectedException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times);
    }

    [Fact]
    public void Constructor_UsesDefaults_WhenEnvVarsNotSet()
    {
        var client = CreateClient();

        VerifyLog(_mockLogger, LogLevel.Warning, "SERVICE_PORT not found or invalid", Times.Once());
    }

    [Fact]
    public void Constructor_ReadsEnvVars_Correctly()
    {
        const string expectedServiceId = "my-comm-service";
        const string expectedHost = "comm.example.com";
        const string expectedPortStr = "8080";
        Environment.SetEnvironmentVariable("SERVICE_ID", expectedServiceId);
        Environment.SetEnvironmentVariable("SERVICE_HOST", expectedHost);
        Environment.SetEnvironmentVariable("SERVICE_PORT", expectedPortStr);
        Environment.SetEnvironmentVariable("DISCOVERY_SERVICE_URL", TestDiscoveryUrl);

        var client = CreateClient();

        VerifyLog(_mockLogger, LogLevel.Warning, "SERVICE_PORT not found or invalid", Times.Never());
    }


    [Fact]
    public async Task RegisterAsync_Skips_WhenDiscoveryUrlNotSet()
    {
        ClearEnvVars();
        var client = CreateClient();

        await client.RegisterAsync();

        Assert.Null(client.InstanceId);
        VerifyLog(_mockLogger, LogLevel.Warning, "DISCOVERY_SERVICE_URL is not set", Times.Once());
        
        _mockHttpMessageHandler.Protected()
            .Verify("SendAsync", Times.Never(),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task RegisterAsync_Succeeds_WhenApiCallIsSuccessful()
    {
        Environment.SetEnvironmentVariable("DISCOVERY_SERVICE_URL", TestDiscoveryUrl);
        var client = CreateClient();
        var expectedUri = new Uri($"{TestDiscoveryUrl}/api/discovery/register");

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(m =>
                    m.Method == HttpMethod.Post &&
                    m.RequestUri == expectedUri),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK))
            .Verifiable();

        await client.RegisterAsync();

        Assert.NotNull(client.InstanceId);
        
        _mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(m => m.Method == HttpMethod.Post && m.RequestUri == expectedUri),
            ItExpr.IsAny<CancellationToken>());
        VerifyLog(_mockLogger, LogLevel.Information, "Service registered with discovery", Times.Once());
        VerifyLog(_mockLogger, LogLevel.Error, "Failed to register service", Times.Never());
    }

    [Fact]
    public async Task RegisterAsync_UsesEnvVarsInPayload()
    {
        const string expectedServiceId = "payload-test-svc";
        const string expectedHost = "payload.host.test";
        const int expectedPort = 9999;
        Environment.SetEnvironmentVariable("DISCOVERY_SERVICE_URL", TestDiscoveryUrl);
        Environment.SetEnvironmentVariable("SERVICE_ID", expectedServiceId);
        Environment.SetEnvironmentVariable("SERVICE_HOST", expectedHost);
        Environment.SetEnvironmentVariable("SERVICE_PORT", expectedPort.ToString());

        var client = CreateClient();
        HttpRequestMessage? capturedRequest = null;

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(m =>
                    m.Method == HttpMethod.Post &&
                    m.RequestUri!.ToString().Contains("register")),
                ItExpr.IsAny<CancellationToken>()
            )
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        await client.RegisterAsync();

        Assert.NotNull(capturedRequest);
        Assert.NotNull(capturedRequest.Content);

        var jsonPayload = await capturedRequest.Content.ReadAsStringAsync();
        using var jsonDoc = JsonDocument.Parse(jsonPayload);
        var root = jsonDoc.RootElement;

        Assert.True(root.TryGetProperty("serviceId", out var serviceIdElement));
        Assert.Equal(expectedServiceId, serviceIdElement.GetString());

        Assert.True(root.TryGetProperty("instanceId", out var instanceIdElement));
        Assert.Equal(client.InstanceId, instanceIdElement.GetString());

        Assert.True(root.TryGetProperty("host", out var hostElement));
        Assert.Equal(expectedHost, hostElement.GetString());

        Assert.True(root.TryGetProperty("port", out var portElement));
        Assert.Equal(expectedPort, portElement.GetInt32());
    }

    [Fact]
    public async Task RegisterAsync_UsesDefaultsInPayload_WhenEnvVarsNotSet()
    {
        Environment.SetEnvironmentVariable("DISCOVERY_SERVICE_URL", TestDiscoveryUrl);
        ClearEnvVars();
        Environment.SetEnvironmentVariable("DISCOVERY_SERVICE_URL", TestDiscoveryUrl);

        var client = CreateClient();
        HttpRequestMessage? capturedRequest = null;

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(m => m.Method == HttpMethod.Post && m.RequestUri!.ToString().Contains("register")),
                ItExpr.IsAny<CancellationToken>()
            )
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        await client.RegisterAsync();

        Assert.NotNull(capturedRequest);
        Assert.NotNull(capturedRequest.Content);

        var jsonPayload = await capturedRequest.Content.ReadAsStringAsync();
        using var jsonDoc = JsonDocument.Parse(jsonPayload);
        var root = jsonDoc.RootElement;

        Assert.True(root.TryGetProperty("serviceId", out var serviceIdElement));
        Assert.Equal(DefaultServiceId, serviceIdElement.GetString());
        Assert.True(root.TryGetProperty("instanceId", out var instanceIdElement));
        Assert.Equal(client.InstanceId, instanceIdElement.GetString());

        Assert.True(root.TryGetProperty("host", out var hostElement));
        Assert.Equal(DefaultHost, hostElement.GetString());

        Assert.True(root.TryGetProperty("port", out var portElement));
        Assert.Equal(DefaultPort, portElement.GetInt32());
    }


    [Fact]
    public async Task RegisterAsync_Fails_WhenApiCallFails()
    {
        Environment.SetEnvironmentVariable("DISCOVERY_SERVICE_URL", TestDiscoveryUrl);
        var client = CreateClient();
        const string errorBody = "Invalid registration data";

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(m => m.RequestUri!.ToString().Contains("register")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(errorBody)
            });

        await client.RegisterAsync();

        Assert.Null(client.InstanceId);
        VerifyLog(_mockLogger, LogLevel.Error, "Failed to register service", Times.Once());
        VerifyLog(_mockLogger, LogLevel.Error, $"Body: {errorBody}", Times.Once());
        VerifyLog(_mockLogger, LogLevel.Information, "Service registered", Times.Never());
    }

    [Fact]
    public async Task RegisterAsync_Handles_ExceptionDuringApiCall()
    {
        Environment.SetEnvironmentVariable("DISCOVERY_SERVICE_URL", TestDiscoveryUrl);
        var client = CreateClient();
        var testException = new HttpRequestException("Simulated network error");

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(m => m.RequestUri!.ToString().Contains("register")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ThrowsAsync(testException);

        await client.RegisterAsync();

        Assert.Null(client.InstanceId);
        VerifyLogException(_mockLogger, LogLevel.Error, "Error occurred during service registration", testException, Times.Once());
    }

    [Fact]
    public async Task DeregisterAsync_Skips_WhenInstanceIdIsNull()
    {
        Environment.SetEnvironmentVariable("DISCOVERY_SERVICE_URL", TestDiscoveryUrl);
        var client = CreateClient();

        await client.DeregisterAsync();

        _mockHttpMessageHandler.Protected()
            .Verify("SendAsync", Times.Never(),
                ItExpr.Is<HttpRequestMessage>(m => m.Method == HttpMethod.Delete),
                ItExpr.IsAny<CancellationToken>());
        VerifyLog(_mockLogger, LogLevel.Information, "Service deregistered", Times.Never());
    }

    [Fact]
    public async Task DeregisterAsync_Skips_WhenDiscoveryUrlNotSet()
    {
        Environment.SetEnvironmentVariable("DISCOVERY_SERVICE_URL", TestDiscoveryUrl);
        var clientRegistered = CreateClient();
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));
        await clientRegistered.RegisterAsync();
        Assert.NotNull(clientRegistered.InstanceId);

        Environment.SetEnvironmentVariable("DISCOVERY_SERVICE_URL", null);
        var clientToTest = CreateClient();

        Environment.SetEnvironmentVariable("DISCOVERY_SERVICE_URL", TestDiscoveryUrl);
        var clientNoInstanceId = CreateClient();

        await clientNoInstanceId.DeregisterAsync();

        _mockHttpMessageHandler.Protected()
            .Verify("SendAsync", Times.Never(),
                ItExpr.Is<HttpRequestMessage>(m => m.Method == HttpMethod.Delete),
                ItExpr.IsAny<CancellationToken>());
        VerifyLog(_mockLogger, LogLevel.Information, "Service deregistered", Times.Never());

         Environment.SetEnvironmentVariable("DISCOVERY_SERVICE_URL", null);
         var clientNoUrl = CreateClient();

         _mockHttpMessageHandler.Invocations.Clear();
         _mockLogger.Invocations.Clear();

         await clientNoUrl.DeregisterAsync();

        _mockHttpMessageHandler.Protected()
            .Verify("SendAsync", Times.Never(),
                ItExpr.Is<HttpRequestMessage>(m => m.Method == HttpMethod.Delete),
                ItExpr.IsAny<CancellationToken>());
         VerifyLog(_mockLogger, LogLevel.Information, "Service deregistered", Times.Never());

    }


    [Fact]
    public async Task DeregisterAsync_Succeeds_WhenApiCallIsSuccessful()
    {
        Environment.SetEnvironmentVariable("DISCOVERY_SERVICE_URL", TestDiscoveryUrl);
        var client = CreateClient();

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(m => m.Method == HttpMethod.Post && m.RequestUri!.ToString().Contains("register")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));
        await client.RegisterAsync();
        var instanceId = client.InstanceId;
        Assert.NotNull(instanceId);
        var expectedUri = new Uri($"{TestDiscoveryUrl}/api/discovery/deregister/{instanceId}");


        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(m =>
                    m.Method == HttpMethod.Delete &&
                    m.RequestUri == expectedUri),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK))
            .Verifiable();

        await client.DeregisterAsync();

         _mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(m => m.Method == HttpMethod.Delete && m.RequestUri == expectedUri),
            ItExpr.IsAny<CancellationToken>());
        VerifyLog(_mockLogger, LogLevel.Information, "Service deregistered from discovery", Times.Once());
        VerifyLog(_mockLogger, LogLevel.Error, "Failed to deregister", Times.Never());
    }

    [Fact]
    public async Task DeregisterAsync_LogsError_WhenApiCallFails()
    {
        Environment.SetEnvironmentVariable("DISCOVERY_SERVICE_URL", TestDiscoveryUrl);
        var client = CreateClient();

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.Is<HttpRequestMessage>(m => m.RequestUri!.ToString().Contains("register")), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));
        await client.RegisterAsync();
        var instanceId = client.InstanceId;
        Assert.NotNull(instanceId);

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(m => m.RequestUri!.ToString().Contains($"deregister/{instanceId}")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        await client.DeregisterAsync();

        VerifyLog(_mockLogger, LogLevel.Error, "Failed to deregister service", Times.Once());
        VerifyLog(_mockLogger, LogLevel.Error, "Status code: InternalServerError", Times.Once());
        VerifyLog(_mockLogger, LogLevel.Information, "Service deregistered", Times.Never());
    }

    [Fact]
    public async Task DeregisterAsync_Handles_ExceptionDuringApiCall()
    {
        Environment.SetEnvironmentVariable("DISCOVERY_SERVICE_URL", TestDiscoveryUrl);
        var client = CreateClient();
        var testException = new HttpRequestException("Deregister network error");

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.Is<HttpRequestMessage>(m => m.RequestUri!.ToString().Contains("register")), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));
        await client.RegisterAsync();
        Assert.NotNull(client.InstanceId);

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(m => m.RequestUri!.ToString().Contains("deregister")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ThrowsAsync(testException);

        await client.DeregisterAsync();

        VerifyLogException(_mockLogger, LogLevel.Error, "Error occurred during service deregistration", testException, Times.Once());
    }


    [Fact]
    public async Task SendHeartbeatAsync_Skips_WhenInstanceIdIsNull()
    {
        Environment.SetEnvironmentVariable("DISCOVERY_SERVICE_URL", TestDiscoveryUrl);
        var client = CreateClient();

        await client.SendHeartbeatAsync();

        VerifyLog(_mockLogger, LogLevel.Warning, "Skipping heartbeat. Service not registered", Times.Once());
        _mockHttpMessageHandler.Protected()
            .Verify("SendAsync", Times.Never(),
                ItExpr.Is<HttpRequestMessage>(m => m.RequestUri!.ToString().Contains("heartbeat")),
                ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task SendHeartbeatAsync_Skips_WhenDiscoveryUrlNotSet()
    {
        Environment.SetEnvironmentVariable("DISCOVERY_SERVICE_URL", TestDiscoveryUrl);
        var clientRegistered = CreateClient();
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));
        await clientRegistered.RegisterAsync();
        Assert.NotNull(clientRegistered.InstanceId);

        Environment.SetEnvironmentVariable("DISCOVERY_SERVICE_URL", null);
        var clientToTest = CreateClient();

        _mockLogger.Invocations.Clear();


        await clientToTest.SendHeartbeatAsync();

        VerifyLog(_mockLogger, LogLevel.Warning, "Skipping heartbeat. Service not registered or discovery URL not set.", Times.Once());
        _mockHttpMessageHandler.Protected()
            .Verify("SendAsync", Times.Never(),
                ItExpr.Is<HttpRequestMessage>(m => m.RequestUri!.ToString().Contains("heartbeat")),
                ItExpr.IsAny<CancellationToken>());
    }


    [Fact]
    public async Task SendHeartbeatAsync_Succeeds_WhenApiCallIsSuccessful()
    {
        Environment.SetEnvironmentVariable("DISCOVERY_SERVICE_URL", TestDiscoveryUrl);
        var client = CreateClient();

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.Is<HttpRequestMessage>(m => m.RequestUri!.ToString().Contains("register")), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));
        await client.RegisterAsync();
        var instanceId = client.InstanceId;
        Assert.NotNull(instanceId);
        var expectedUri = new Uri($"{TestDiscoveryUrl}/api/discovery/heartbeat/{instanceId}");


        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(m =>
                    m.Method == HttpMethod.Post &&
                    m.RequestUri == expectedUri &&
                    m.Content == null),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK))
            .Verifiable();

        await client.SendHeartbeatAsync();

        _mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(m => m.Method == HttpMethod.Post && m.RequestUri == expectedUri && m.Content == null),
            ItExpr.IsAny<CancellationToken>());

         _mockLogger.Verify(
            log => log.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Heartbeat sent successfully.")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        VerifyLog(_mockLogger, LogLevel.Warning, "Heartbeat failed", Times.Never());
    }


    [Fact]
    public async Task SendHeartbeatAsync_AttemptsReRegistration_WhenApiCallFailsWithNotFound()
    {
        Environment.SetEnvironmentVariable("DISCOVERY_SERVICE_URL", TestDiscoveryUrl);
        var client = CreateClient();
        var registerUri = new Uri($"{TestDiscoveryUrl}/api/discovery/register");

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(m => m.RequestUri == registerUri),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        await client.RegisterAsync();
        var initialInstanceId = client.InstanceId;
        Assert.NotNull(initialInstanceId);
        var heartbeatUri = new Uri($"{TestDiscoveryUrl}/api/discovery/heartbeat/{initialInstanceId}");


        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(m =>
                    m.Method == HttpMethod.Post &&
                    m.RequestUri == heartbeatUri),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NotFound))
            .Verifiable();


        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(m =>
                    m.Method == HttpMethod.Post &&
                    m.RequestUri == registerUri),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK))
            .Verifiable();


        await client.SendHeartbeatAsync();

        _mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(m => m.Method == HttpMethod.Post && m.RequestUri == heartbeatUri),
            ItExpr.IsAny<CancellationToken>());
        VerifyLog(_mockLogger, LogLevel.Warning, "Heartbeat failed. Status code: NotFound. Attempting to re-register...", Times.Once());

         _mockHttpMessageHandler.Protected().Verify(
             "SendAsync",
             Times.Exactly(2),
             ItExpr.Is<HttpRequestMessage>(m => m.Method == HttpMethod.Post && m.RequestUri == registerUri),
             ItExpr.IsAny<CancellationToken>());
        VerifyLog(_mockLogger, LogLevel.Information, "Service registered with discovery", Times.Exactly(2));

        Assert.NotNull(client.InstanceId);
        Assert.NotEqual(initialInstanceId, client.InstanceId);
    }


     [Fact]
    public async Task SendHeartbeatAsync_Handles_ExceptionDuringApiCall()
    {
        Environment.SetEnvironmentVariable("DISCOVERY_SERVICE_URL", TestDiscoveryUrl);
        var client = CreateClient();
        var testException = new HttpRequestException("Heartbeat network error");
        var registerUri = new Uri($"{TestDiscoveryUrl}/api/discovery/register");


        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.Is<HttpRequestMessage>(m => m.RequestUri == registerUri), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));
        await client.RegisterAsync();
        Assert.NotNull(client.InstanceId);
        var heartbeatUri = new Uri($"{TestDiscoveryUrl}/api/discovery/heartbeat/{client.InstanceId}");


        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(m => m.RequestUri == heartbeatUri),
                ItExpr.IsAny<CancellationToken>()
            )
            .ThrowsAsync(testException);

        await client.SendHeartbeatAsync();

        VerifyLogException(_mockLogger, LogLevel.Error, "Error occurred while sending heartbeat", testException, Times.Once());
        
        _mockHttpMessageHandler.Protected()
             .Verify("SendAsync", Times.Once(),
                 ItExpr.Is<HttpRequestMessage>(m => m.Method == HttpMethod.Post && m.RequestUri == registerUri),
                 ItExpr.IsAny<CancellationToken>());
    }
}