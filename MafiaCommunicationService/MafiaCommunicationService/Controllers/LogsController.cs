using Microsoft.AspNetCore.Mvc;
using MafiaCommunicationService.Models;

namespace MafiaCommunicationService.Controllers;

[ApiController]
[Route("api/logs")]
public class LogsController(ILogger<LogsController> logger) : ControllerBase
{
    private readonly string _logFilePath = Environment.GetEnvironmentVariable("LOG_FILE_PATH") ?? "logs/service.log";

    [HttpGet("download")]
    public IActionResult DownloadLogs()
    {
        if (!System.IO.File.Exists(_logFilePath))
        {
            logger.LogWarning("Log file not found at path: {LogFilePath}", _logFilePath);
            return NotFound(new ErrorResponse("NOT_FOUND", "Log file not found."));
        }

        try
        {
            var stream = new FileStream(_logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            
            logger.LogInformation("Log file downloaded by user from IP: {IpAddress}", HttpContext.Connection.RemoteIpAddress);
            
            return new FileStreamResult(stream, "application/octet-stream")
            {
                FileDownloadName = $"mafia-service-logs_{DateTime.UtcNow:yyyyMMdd_HHmmss}.log"
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to read log file for download.");
            return StatusCode(500, new ErrorResponse("SERVER_ERROR", "Could not read log file."));
        }
    }
}