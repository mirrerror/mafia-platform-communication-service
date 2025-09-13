namespace MafiaCommunicationService.Models;

public class ErrorResponse(string code, string message)
{
    public ErrorModel Error { get; } = new() { Code = code, Message = message };
}