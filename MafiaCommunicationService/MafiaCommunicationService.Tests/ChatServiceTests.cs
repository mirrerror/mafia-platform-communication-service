using MafiaCommunicationService.Services;

namespace MafiaCommunicationService.Tests;

public class ChatServiceTests
{
    private readonly IChatService _chatService;

    public ChatServiceTests()
    {
        _chatService = new InMemoryChatService();
    }

    [Theory]
    [InlineData("mafia_night_123", true)]
    [InlineData("non_existent_lobby", false)]
    public async Task LobbyExistsAsync_ShouldReturnExpectedResult(string lobbyId, bool expected)
    {
        var result = await _chatService.LobbyExistsAsync(lobbyId);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("mafia_night_123", "mafia", true)]
    [InlineData("mafia_night_123", "non_existent_channel", false)]
    [InlineData("non_existent_lobby", "mafia", false)]
    public async Task PrivateChannelExistsAsync_ShouldReturnExpectedResult(string lobbyId, string channelName, bool expected)
    {
        var result = await _chatService.PrivateChannelExistsAsync(lobbyId, channelName);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("mafia_night_123", "mafia", 101, true)] // User with access
    [InlineData("mafia_night_123", "mafia", 999, false)] // User without access
    [InlineData("mafia_night_123", "non_existent_channel", 101, false)] // Non-existent channel
    [InlineData("non_existent_lobby", "mafia", 101, false)] // Non-existent lobby
    public async Task UserHasAccessToChannelAsync_ShouldReturnExpectedResult(string lobbyId, string channelName, long userId, bool expected)
    {
        var result = await _chatService.UserHasAccessToChannelAsync(lobbyId, channelName, userId);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void IsGlobalChatEnabled_ShouldDefaultToFalse()
    {
        Assert.False(_chatService.IsGlobalChatEnabled("new_lobby_id"));
    }

    [Fact]
    public void ToggleGlobalChat_ShouldFlipBooleanState()
    {
        const string lobbyId = "mafia_night_123";

        var initialStatus = _chatService.IsGlobalChatEnabled(lobbyId);
        var newStatus1 = _chatService.ToggleGlobalChat(lobbyId);
        var newStatus2 = _chatService.ToggleGlobalChat(lobbyId);

        Assert.False(initialStatus);
        Assert.True(newStatus1);
        Assert.False(newStatus2);
    }
}