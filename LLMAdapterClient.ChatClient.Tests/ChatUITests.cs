using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LLMAdapterClient.ChatClient.Services;
using LLMAdapterClient.Common;
using Moq;
using Xunit;

namespace LLMAdapterClient.ChatClient.Tests;

public class ChatUITests
{
    [Fact]
    public void DisplayMessage_ShouldNotThrowException()
    {
        // Arrange
        var chatUI = new ConsoleChatUI();
        
        // Act & Assert - we're just checking it doesn't throw
        var exception = Record.Exception(() => chatUI.DisplayMessage("system", "Test message"));
        Assert.Null(exception);
    }
    
    [Fact]
    public async Task DisplayStreamingMessageAsync_ShouldHandleTokens()
    {
        // Arrange
        var chatUI = new ConsoleChatUI();
        var tokens = new List<string> { "Hello", ", ", "world", "!" };
        var asyncTokens = GetAsyncTokens(tokens);
        
        // Act & Assert - we're just checking it doesn't throw
        var exception = await Record.ExceptionAsync(() => chatUI.DisplayStreamingMessageAsync("assistant", asyncTokens));
        Assert.Null(exception);
    }
    
    [Fact]
    public void AnnounceNewAdapter_ShouldNotThrowException()
    {
        // Arrange
        var chatUI = new ConsoleChatUI();
        var mockAdapter = new Mock<IAdapterInfo>();
        mockAdapter.Setup(a => a.Name).Returns("TestAdapter");
        mockAdapter.Setup(a => a.FilePath).Returns("/path/to/adapter");
        mockAdapter.Setup(a => a.Created).Returns(DateTime.Now);
        mockAdapter.Setup(a => a.Metadata).Returns(new Dictionary<string, object>
        {
            { "test", "value" }
        });
        
        // Act & Assert - we're just checking it doesn't throw
        var exception = Record.Exception(() => chatUI.AnnounceNewAdapter(mockAdapter.Object));
        Assert.Null(exception);
    }
    
    [Fact]
    public void DisplayError_ShouldNotThrowException()
    {
        // Arrange
        var chatUI = new ConsoleChatUI();
        
        // Act & Assert - we're just checking it doesn't throw
        var exception = Record.Exception(() => chatUI.DisplayError("Test error"));
        Assert.Null(exception);
    }
    
    [Fact]
    public void DisplaySystemMessage_ShouldNotThrowException()
    {
        // Arrange
        var chatUI = new ConsoleChatUI();
        
        // Act & Assert - we're just checking it doesn't throw
        var exception = Record.Exception(() => chatUI.DisplaySystemMessage("Test system message"));
        Assert.Null(exception);
    }
    
    private async IAsyncEnumerable<string> GetAsyncTokens(IEnumerable<string> tokens)
    {
        foreach (var token in tokens)
        {
            await Task.Delay(10); // Simulate some processing time
            yield return token;
        }
    }
} 