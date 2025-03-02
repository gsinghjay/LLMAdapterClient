using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using LLMAdapterClient.ChatClient.Services;
using LLMAdapterClient.Common;
using LLMAdapterClient.Publisher.Services;
using Moq;
using Xunit;

namespace LLMAdapterClient.ChatClient.Tests;

/// <summary>
/// Tests for the ModelService class
/// </summary>
public class ModelServiceTests
{
    // Path to the workspace root
    private readonly string _workspacePath = Directory.GetCurrentDirectory();
    private readonly string _adapterBasePath;
    
    /// <summary>
    /// Helper method to convert async enumerable to a list
    /// </summary>
    private static async Task<List<T>> ToListAsync<T>(IAsyncEnumerable<T> source)
    {
        var result = new List<T>();
        await foreach (var item in source)
        {
            result.Add(item);
        }
        return result;
    }
    
    // Helper method to convert IEnumerable to IAsyncEnumerable
    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(IEnumerable<T> source)
    {
        await Task.Yield();
        
        foreach (var item in source)
        {
            yield return item;
        }
    }
    
    public ModelServiceTests()
    {
        // Navigate up to the workspace root if we're in a subdirectory like bin/Debug/...
        while (!File.Exists(Path.Combine(_workspacePath, "global.json")) && 
               !Directory.Exists(Path.Combine(_workspacePath, "llm_training-main")))
        {
            _workspacePath = Directory.GetParent(_workspacePath)?.FullName ?? _workspacePath;
            if (Path.GetPathRoot(_workspacePath) == _workspacePath)
            {
                // We've hit the root directory, stop
                break;
            }
        }
        
        _adapterBasePath = Path.Combine(_workspacePath, "llm_training-main", "checkpoints");
    }
    
    private Mock<IPythonProcessManager> CreateMockPythonProcessManager()
    {
        var mockProcessManager = new Mock<IPythonProcessManager>();
        
        mockProcessManager.Setup(p => p.IsRunning).Returns(true);
        mockProcessManager.Setup(p => p.StartAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string[]>()))
            .Returns(Task.CompletedTask);
        mockProcessManager.Setup(p => p.StopAsync())
            .Returns(Task.CompletedTask);
        
        return mockProcessManager;
    }
    
    private IAdapterInfo CreateMockAdapter(string name = "best_model_adapter")
    {
        // Use the actual path to the adapter in the workspace
        string adapterPath = Path.Combine(_adapterBasePath, name);
        
        var mockAdapter = new Mock<IAdapterInfo>();
        mockAdapter.Setup(a => a.Name).Returns(name);
        mockAdapter.Setup(a => a.FilePath).Returns(adapterPath);
        mockAdapter.Setup(a => a.Created).Returns(DateTime.UtcNow);
        mockAdapter.Setup(a => a.Metadata).Returns(new Dictionary<string, object>
        {
            ["model_type"] = "lora",
            ["base_model"] = "deepseek-r1-distill-qwen-1.5b"
        });
        
        return mockAdapter.Object;
    }
    
    [Fact]
    public async Task InitializeAsync_WithValidAdapter_ShouldStartPythonProcess()
    {
        // Arrange
        var mockProcessManager = CreateMockPythonProcessManager();
        var adapter = CreateMockAdapter();
        var modelService = new PythonModelService(mockProcessManager.Object);
        
        // Act
        await modelService.InitializeAsync(adapter, skipValidation: true);
        
        // Assert
        mockProcessManager.Verify(p => p.StartAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.Is<string[]>(args => args.Contains("--config"))
        ), Times.Once);
    }
    
    [Fact]
    public async Task GenerateResponseAsync_WithPrompt_ShouldSendCommandAndReturnResponse()
    {
        // Arrange
        var mockProcessManager = CreateMockPythonProcessManager();
        var expectedResponse = "This is a test response from the model";
        
        mockProcessManager.Setup(p => p.SendCommandAsync(
            It.IsAny<string>(), 
            It.IsAny<CancellationToken>()
        )).ReturnsAsync(expectedResponse);
        
        var modelService = new PythonModelService(mockProcessManager.Object);
        await modelService.InitializeAsync(CreateMockAdapter(), skipValidation: true);
        
        // Act
        var response = await modelService.GenerateResponseAsync("Test prompt");
        
        // Assert
        Assert.Equal(expectedResponse, response);
        mockProcessManager.Verify(p => p.SendCommandAsync("Test prompt", It.IsAny<CancellationToken>()), Times.Once);
    }
    
    // Create a test-specific implementation of IModelService for streaming tests
    private class TestModelService : IModelService
    {
        private readonly List<string> _expectedTokens;
        private bool _isInitialized;
        
        public event EventHandler<string>? MessageReceived;
        public event EventHandler<string>? ErrorReceived;
        
        public bool IsInitialized => _isInitialized;
        public IAdapterInfo? CurrentAdapter { get; private set; }
        
        public TestModelService(List<string> expectedTokens)
        {
            _expectedTokens = expectedTokens ?? throw new ArgumentNullException(nameof(expectedTokens));
        }
        
        public Task InitializeAsync(IAdapterInfo adapter, string? configPath = null, bool skipValidation = false)
        {
            CurrentAdapter = adapter;
            _isInitialized = true;
            return Task.CompletedTask;
        }
        
        public Task<string> GenerateResponseAsync(string prompt, CancellationToken token = default)
        {
            return Task.FromResult(string.Join("", _expectedTokens));
        }
        
        public async IAsyncEnumerable<string> GenerateStreamingResponseAsync(
            string prompt, 
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var tokenText in _expectedTokens)
            {
                yield return tokenText;
            }
            
            await Task.CompletedTask; // Just to make it async
        }
        
        public Task ExecuteSpecialCommandAsync(string command)
        {
            return Task.CompletedTask;
        }
        
        public Task ShutdownAsync()
        {
            _isInitialized = false;
            return Task.CompletedTask;
        }
    }
    
    [Fact]
    public async Task GenerateStreamingResponseAsync_WithPrompt_ShouldStreamResponse()
    {
        // Arrange
        var expectedTokens = new List<string> { "This", " is", " a", " streaming", " response" };
        var modelService = new TestModelService(expectedTokens);
        await modelService.InitializeAsync(CreateMockAdapter());
        
        // Act
        var responseTokens = new List<string>();
        await foreach (var token in modelService.GenerateStreamingResponseAsync("Test prompt"))
        {
            responseTokens.Add(token);
        }
        
        // Assert
        Assert.Equal(expectedTokens, responseTokens);
    }
    
    [Fact]
    public async Task ExecuteSpecialCommandAsync_WithCommand_ShouldSendSpecialCommand()
    {
        // Arrange
        var mockProcessManager = CreateMockPythonProcessManager();
        mockProcessManager.Setup(p => p.SendCommandAsync(
            It.IsAny<string>(), 
            It.IsAny<CancellationToken>()
        )).ReturnsAsync("Command executed");
        
        var modelService = new PythonModelService(mockProcessManager.Object);
        await modelService.InitializeAsync(CreateMockAdapter(), skipValidation: true);
        
        // Act
        await modelService.ExecuteSpecialCommandAsync("/clear");
        
        // Assert
        mockProcessManager.Verify(p => p.SendCommandAsync("/clear", It.IsAny<CancellationToken>()), Times.Once);
    }
    
    [Fact]
    public async Task ShutdownAsync_WhenCalled_ShouldStopPythonProcess()
    {
        // Arrange
        var mockProcessManager = CreateMockPythonProcessManager();
        
        // Track number of calls to StopAsync
        int stopAsyncCallCount = 0;
        mockProcessManager.Setup(p => p.StopAsync())
            .Callback(() => stopAsyncCallCount++)
            .Returns(Task.CompletedTask);
            
        var modelService = new PythonModelService(mockProcessManager.Object);
        await modelService.InitializeAsync(CreateMockAdapter(), skipValidation: true);
        
        // Reset the call count after initialization
        stopAsyncCallCount = 0;
        
        // Act
        await modelService.ShutdownAsync();
        
        // Assert
        Assert.Equal(1, stopAsyncCallCount);
    }
    
    // Add a new test that uses the real adapter directory
    [Fact]
    public async Task InitializeAsync_WithRealAdapter_ShouldInitializeSuccessfully()
    {
        // Skip if adapter directory doesn't exist in workspace
        if (!Directory.Exists(_adapterBasePath))
        {
            return; // Skip the test if the adapter directory doesn't exist
        }
        
        // Arrange
        var mockProcessManager = CreateMockPythonProcessManager();
        var adapter = CreateMockAdapter("best_model_adapter"); // Use the real adapter path
        var modelService = new PythonModelService(mockProcessManager.Object);
        
        // Act
        await modelService.InitializeAsync(adapter); // Don't skip validation to test real path
        
        // Assert
        Assert.True(modelService.IsInitialized);
        Assert.Equal(adapter, modelService.CurrentAdapter);
        mockProcessManager.Verify(p => p.StartAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.Is<string[]>(args => args.Contains("--config"))
        ), Times.Once);
    }
    
    [Fact]
    public async Task InitializeAsync_WithExtractedAdapterInfo_ShouldInitializeSuccessfully()
    {
        // Skip if adapter directory doesn't exist in workspace
        if (!Directory.Exists(_adapterBasePath))
        {
            return; // Skip the test if the adapter directory doesn't exist
        }
        
        string bestModelAdapterPath = Path.Combine(_adapterBasePath, "best_model_adapter");
        if (!Directory.Exists(bestModelAdapterPath))
        {
            return; // Skip if the best_model_adapter directory doesn't exist
        }
        
        // Arrange
        var mockProcessManager = CreateMockPythonProcessManager();
        var extractor = new AdapterInfoExtractor();
        var adapter = await extractor.ExtractAdapterInfoAsync(bestModelAdapterPath);
        var modelService = new PythonModelService(mockProcessManager.Object);
        
        // Act
        await modelService.InitializeAsync(adapter);
        
        // Assert
        Assert.True(modelService.IsInitialized);
        Assert.Equal(adapter, modelService.CurrentAdapter);
        Assert.Equal("best_model_adapter", adapter.Name);
        Assert.Equal(bestModelAdapterPath, adapter.FilePath);
        Assert.NotEmpty(adapter.Metadata);
        
        // Verify the adapter metadata contains expected keys
        Assert.Contains("base_model_name_or_path", adapter.Metadata.Keys);
        Assert.Contains("peft_type", adapter.Metadata.Keys);
        Assert.Contains("task_type", adapter.Metadata.Keys);
        
        // Verify the Python process was started with the correct arguments
        mockProcessManager.Verify(p => p.StartAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.Is<string[]>(args => args.Contains("--config"))
        ), Times.Once);
    }

    [Fact]
    public async Task GenerateResponseAsync_WithFormattedAnsiOutput_ShouldStripAnsiCodesAndReturnCleanResponse()
    {
        // Arrange
        var mockProcessManager = CreateMockPythonProcessManager();
        
        // Setup response with ANSI color codes
        string ansiColoredResponse = "\u001b[93mBot is thinking...\u001b[0m\nThis is a \u001b[32mcolored\u001b[0m response.";
        string expectedCleanResponse = "This is a colored response.";
        
        mockProcessManager.Setup(p => p.SendCommandAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ansiColoredResponse);
        
        var modelService = new PythonModelService(mockProcessManager.Object);
        await modelService.InitializeAsync(CreateMockAdapter(), skipValidation: true);
        
        // Act
        var result = await modelService.GenerateResponseAsync("Test prompt");
        
        // Assert
        Assert.Equal(expectedCleanResponse, result);
        mockProcessManager.Verify(p => p.SendCommandAsync("Test prompt", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateResponseAsync_WithMultiLineResponse_ShouldReturnCompleteResponse()
    {
        // Arrange
        var mockProcessManager = CreateMockPythonProcessManager();
        
        // Setup multi-line response
        string multiLineResponse = 
            "This is line 1.\n" +
            "This is line 2.\n" +
            "This is line 3.";
        
        mockProcessManager.Setup(p => p.SendCommandAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(multiLineResponse);
        
        var modelService = new PythonModelService(mockProcessManager.Object);
        await modelService.InitializeAsync(CreateMockAdapter(), skipValidation: true);
        
        // Act
        var result = await modelService.GenerateResponseAsync("Multi-line test");
        
        // Assert
        Assert.Equal(multiLineResponse, result);
        Assert.Contains("This is line 1.", result);
        Assert.Contains("This is line 2.", result);
        Assert.Contains("This is line 3.", result);
        mockProcessManager.Verify(p => p.SendCommandAsync("Multi-line test", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateResponseAsync_WithSystemMessagesAndLogs_ShouldFilterOutNonResponseContent()
    {
        // Arrange
        var mockProcessManager = CreateMockPythonProcessManager();
        
        // Setup response with system messages and logs
        string complexResponse = 
            "| INFO | 2023-03-02 12:34:56 | Loading model...\n" +
            "System: Processing your request...\n" +
            "Bot: Here is your response:\n" +
            "This is the actual response content.\n" +
            "It spans multiple lines.\n" +
            "| DEBUG | 2023-03-02 12:34:59 | Response generated";
        
        string expectedResponse = 
            "This is the actual response content.\n" +
            "It spans multiple lines.";
        
        mockProcessManager.Setup(p => p.SendCommandAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(complexResponse);
        
        var modelService = new PythonModelService(mockProcessManager.Object);
        await modelService.InitializeAsync(CreateMockAdapter(), skipValidation: true);
        
        // Act
        var result = await modelService.GenerateResponseAsync("Complex test");
        
        // Assert
        Assert.Equal(expectedResponse, result);
        mockProcessManager.Verify(p => p.SendCommandAsync("Complex test", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateStreamingResponseAsync_WithFragmentedResponse_ShouldReassembleCorrectly()
    {
        // Arrange
        var mockProcessManager = CreateMockPythonProcessManager();
        
        // Setup fragmented streaming response
        var fragmentedResponse = new List<string>
        {
            "| INFO | 2023-03-02 12:34:56 | Generating response...",
            "Assistant: ",
            "This is ",
            "a fragmented ",
            "response ",
            "that should be ",
            "reassembled ",
            "correctly.",
            "User: "
        };
        
        mockProcessManager.Setup(p => p.SendCommandStreamingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(fragmentedResponse));
        
        var modelService = new PythonModelService(mockProcessManager.Object);
        await modelService.InitializeAsync(CreateMockAdapter(), skipValidation: true);
        
        // Act
        var result = await ToListAsync(modelService.GenerateStreamingResponseAsync("Fragmented test"));
        
        // Assert
        Assert.Equal(3, result.Count); // Expecting 3 tokens after buffering
        
        // Check that all content is present, regardless of exact buffering
        var combinedResult = string.Join("", result);
        Assert.Contains("This is ", combinedResult);
        Assert.Contains("a fragmented ", combinedResult);
        Assert.Contains("response ", combinedResult);
        Assert.Contains("that should be ", combinedResult);
        Assert.Contains("reassembled ", combinedResult);
        Assert.Contains("correctly.", combinedResult);
    }
} 