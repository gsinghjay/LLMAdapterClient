using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LLMAdapterClient.ChatClient.Services;
using LLMAdapterClient.Common;
using Moq;
using Xunit;

namespace LLMAdapterClient.ChatClient.Tests;

public class ChatSessionTests
{
    private readonly Mock<IModelService> _mockModelService;
    private readonly Mock<IAdapterManager> _mockAdapterManager;
    private readonly Mock<IChatUI> _mockChatUI;
    private readonly Mock<IAdapterPublisher> _mockPublisher;
    private readonly Mock<IAdapterInfo> _mockAdapter;

    public ChatSessionTests()
    {
        _mockModelService = new Mock<IModelService>();
        _mockAdapterManager = new Mock<IAdapterManager>();
        _mockChatUI = new Mock<IChatUI>();
        _mockPublisher = new Mock<IAdapterPublisher>();
        _mockAdapter = new Mock<IAdapterInfo>();
        
        // Set up basic mocks
        _mockAdapter.Setup(a => a.Name).Returns("TestAdapter");
        _mockAdapter.Setup(a => a.FilePath).Returns("/path/to/adapter");
        _mockAdapter.Setup(a => a.Created).Returns(DateTime.Now);
        _mockAdapter.Setup(a => a.Metadata).Returns(new Dictionary<string, object>());
        
        _mockAdapterManager.Setup(m => m.GetLatestAdapterAsync(It.IsAny<IAdapterPublisher>()))
            .ReturnsAsync(_mockAdapter.Object);
            
        _mockModelService.Setup(m => m.IsInitialized).Returns(true);
        _mockModelService.Setup(m => m.CurrentAdapter).Returns(_mockAdapter.Object);
    }
    
    [Fact]
    public void Constructor_ShouldThrowIfArgumentsAreNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ChatSession(null!, _mockAdapterManager.Object, _mockChatUI.Object));
        Assert.Throws<ArgumentNullException>(() => new ChatSession(_mockModelService.Object, null!, _mockChatUI.Object));
        Assert.Throws<ArgumentNullException>(() => new ChatSession(_mockModelService.Object, _mockAdapterManager.Object, null!));
    }
    
    [Fact]
    public async Task StartAsync_ShouldInitializeModelWithLatestAdapter()
    {
        // Arrange
        var session = new ChatSession(_mockModelService.Object, _mockAdapterManager.Object, _mockChatUI.Object);
        
        // Setup for early exit from the chat loop
        _mockChatUI.Setup(c => c.GetUserInputAsync())
            .ReturnsAsync("/exit");
        
        // Act
        await session.StartAsync(_mockPublisher.Object);
        
        // Assert
        _mockAdapterManager.Verify(m => m.GetLatestAdapterAsync(_mockPublisher.Object), Times.Once);
        _mockAdapterManager.Verify(m => m.InitializeModelWithAdapterAsync(_mockModelService.Object, _mockAdapter.Object), Times.Once);
        _mockAdapterManager.Verify(m => m.MonitorForNewAdaptersAsync(_mockPublisher.Object, It.IsAny<CancellationToken>()), Times.Once);
    }
    
    [Fact]
    public async Task StartAsync_ShouldHandleUserMessage()
    {
        // Arrange
        var session = new ChatSession(_mockModelService.Object, _mockAdapterManager.Object, _mockChatUI.Object);
        
        // Setup user input sequence
        var inputQueue = new Queue<string>();
        inputQueue.Enqueue("Hello, Assistant!");
        inputQueue.Enqueue("/exit");
        
        _mockChatUI.Setup(c => c.GetUserInputAsync())
            .Returns(() => Task.FromResult(inputQueue.Count > 0 ? inputQueue.Dequeue() : "/exit"));
            
        _mockModelService.Setup(m => m.GenerateStreamingResponseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(ChatSessionAsyncEnumerableExtensions.ToAsyncEnumerable(new[] { "Hello", ", ", "User", "!" }));
        
        // Act
        await session.StartAsync(_mockPublisher.Object);
        
        // Assert
        _mockChatUI.Verify(c => c.DisplayMessage("user", "Hello, Assistant!"), Times.Once);
        _mockModelService.Verify(m => m.GenerateStreamingResponseAsync("Hello, Assistant!", It.IsAny<CancellationToken>()), Times.Once);
        _mockChatUI.Verify(c => c.DisplayStreamingMessageAsync("assistant", It.IsAny<IAsyncEnumerable<string>>()), Times.Once);
    }
    
    [Fact]
    public async Task StartAsync_ShouldHandleSpecialCommands()
    {
        // Arrange
        var session = new ChatSession(_mockModelService.Object, _mockAdapterManager.Object, _mockChatUI.Object);
        
        // Setup user input sequence
        var inputQueue = new Queue<string>();
        inputQueue.Enqueue("/help");
        inputQueue.Enqueue("/clear");
        inputQueue.Enqueue("/exit");
        
        _mockChatUI.Setup(c => c.GetUserInputAsync())
            .Returns(() => Task.FromResult(inputQueue.Count > 0 ? inputQueue.Dequeue() : "/exit"));
        
        // Act
        await session.StartAsync(_mockPublisher.Object);
        
        // Assert
        _mockChatUI.Verify(c => c.DisplaySystemMessage(It.IsRegex("Available commands")), Times.AtLeastOnce);
        _mockModelService.Verify(m => m.ExecuteSpecialCommandAsync("/clear"), Times.Once);
    }
    
    [Fact]
    public void Dispose_ShouldCleanupResources()
    {
        // Arrange
        var session = new ChatSession(_mockModelService.Object, _mockAdapterManager.Object, _mockChatUI.Object);
        
        // Act
        session.Dispose();
        
        // Additional dispose should not throw
        session.Dispose();
    }
}

public static class ChatSessionAsyncEnumerableExtensions
{
    public static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IEnumerable<T> source)
    {
        foreach (var item in source)
        {
            await Task.Delay(10); // Simulate async work
            yield return item;
        }
    }
} 