using MafiaCommunicationService.Controllers;
using MafiaCommunicationService.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace MafiaCommunicationService.Tests;

public class LogsControllerTests : IDisposable
{
    private readonly Mock<ILogger<LogsController>> _loggerMock = new();
    private string? _tempFile;

    public void Dispose()
    {
        if (!string.IsNullOrEmpty(_tempFile) && File.Exists(_tempFile))
        {
            File.Delete(_tempFile);
        }
        
        Environment.SetEnvironmentVariable("LOG_FILE_PATH", null);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Constructor_UsesDefaultLogPath_WhenEnvVarIsMissing()
    {
        Environment.SetEnvironmentVariable("LOG_FILE_PATH", null);
        
        var logger = Mock.Of<ILogger<LogsController>>();
        var controller = new LogsController(logger);

        var result = controller.DownloadLogs();

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(notFoundResult.Value);
        Assert.Equal("NOT_FOUND", error.Error.Code);
    }

    [Fact]
    public void DownloadLogs_FileExists_ReturnsFileStreamResult()
    {
        _tempFile = Path.GetTempFileName();
        File.WriteAllText(_tempFile, "test log data");
        Environment.SetEnvironmentVariable("LOG_FILE_PATH", _tempFile);

        var controller = new LogsController(_loggerMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = controller.DownloadLogs();

        var fileResult = Assert.IsType<FileStreamResult>(result);
        Assert.Equal("application/octet-stream", fileResult.ContentType);
        Assert.Contains("mafia-service-logs", fileResult.FileDownloadName, StringComparison.OrdinalIgnoreCase);
        
        fileResult.FileStream.Dispose();
    }

    [Fact]
    public void DownloadLogs_FileDoesNotExist_ReturnsNotFoundResult()
    {
        Environment.SetEnvironmentVariable("LOG_FILE_PATH", "non-existent-file.log");
        var controller = new LogsController(_loggerMock.Object);

        var result = controller.DownloadLogs();

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(notFoundResult.Value);
        Assert.Equal("NOT_FOUND", error.Error.Code);
    }

    [Fact]
    public void DownloadLogs_FileIsInUse_ReturnsInternalServerError()
    {
        _tempFile = Path.GetTempFileName();
        File.WriteAllText(_tempFile, "test log data");
        Environment.SetEnvironmentVariable("LOG_FILE_PATH", _tempFile);

        var controller = new LogsController(_loggerMock.Object);

        using var _ = new FileStream(_tempFile, FileMode.Open, FileAccess.Read, FileShare.None);
        var result = controller.DownloadLogs();

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
        var error = Assert.IsType<ErrorResponse>(objectResult.Value);
        Assert.Equal("SERVER_ERROR", error.Error.Code);
    }
}