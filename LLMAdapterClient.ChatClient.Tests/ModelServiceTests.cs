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
/// Extension method to convert IEnumerable to IAsyncEnumerable
/// </summary>
internal static class ModelServiceAsyncEnumerableExtensions
{
    /// <summary>
    /// Converts an IEnumerable to an IAsyncEnumerable
    /// </summary>
    /// <typeparam name="T">The type of elements in the enumerable</typeparam>
    /// <param name="source">The source enumerable</param>
    /// <returns>An async enumerable representation of the source</returns>
    public static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IEnumerable<T> source)
    {
        await Task.Yield();
        
        foreach (var item in source)
        {
            yield return item;
        }
    }
}

/// <summary>
/// Tests for the ModelService class
/// </summary>
public class ModelServiceTests
{
    // Path to the workspace root
    private readonly string _workspacePath = Directory.GetCurrentDirectory();
    private readonly string _adapterBasePath;
    
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
            It.Is<string[]>(args => args.Contains("--adapter_path") && args.Contains(adapter.FilePath))
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
        var adapter = CreateMockAdapter();
        await modelService.InitializeAsync(adapter, skipValidation: true);
        
        // Act
        var response = await modelService.GenerateResponseAsync("Test prompt");
        
        // Assert
        Assert.Equal(expectedResponse, response);
        mockProcessManager.Verify(p => p.SendCommandAsync("Test prompt", It.IsAny<CancellationToken>()), Times.Once);
    }
    
    [Fact]
    public async Task GenerateStreamingResponseAsync_WithPrompt_ShouldStreamResponse()
    {
        // Arrange
        var mockProcessManager = CreateMockPythonProcessManager();
        var tokens = new List<string> { "This", " is", " a", " streaming", " response" };
        
        mockProcessManager.Setup(p => p.SendCommandStreamingAsync(
            It.IsAny<string>(), 
            It.IsAny<CancellationToken>()
        )).Returns(ModelServiceAsyncEnumerableExtensions.ToAsyncEnumerable(tokens));
        
        var modelService = new PythonModelService(mockProcessManager.Object);
        var adapter = CreateMockAdapter();
        await modelService.InitializeAsync(adapter, skipValidation: true);
        
        // Act
        var responseTokens = new List<string>();
        await foreach (var token in modelService.GenerateStreamingResponseAsync("Test prompt"))
        {
            responseTokens.Add(token);
        }
        
        // Assert
        Assert.Equal(tokens, responseTokens);
        mockProcessManager.Verify(p => p.SendCommandStreamingAsync("Test prompt", It.IsAny<CancellationToken>()), Times.Once);
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
        var adapter = CreateMockAdapter();
        await modelService.InitializeAsync(adapter, skipValidation: true);
        
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
        var adapter = CreateMockAdapter();
        await modelService.InitializeAsync(adapter, skipValidation: true);
        
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
            It.Is<string[]>(args => args.Contains("--adapter_path") && args.Contains(adapter.FilePath))
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
            It.Is<string[]>(args => args.Contains("--adapter_path") && args.Contains(adapter.FilePath))
        ), Times.Once);
    }
} 