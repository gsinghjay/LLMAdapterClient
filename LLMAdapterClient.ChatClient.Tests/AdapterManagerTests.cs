using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LLMAdapterClient.ChatClient.Services;
using LLMAdapterClient.Common;
using Moq;
using Xunit;

namespace LLMAdapterClient.ChatClient.Tests;

public class AdapterManagerTests : IDisposable
{
    private readonly Mock<IModelService> _mockModelService;
    private readonly Mock<IAdapterPublisher> _mockPublisher;
    private readonly string _testAdapterPath;
    
    public AdapterManagerTests()
    {
        _mockModelService = new Mock<IModelService>();
        _mockPublisher = new Mock<IAdapterPublisher>();
        _testAdapterPath = Path.Combine(Path.GetTempPath(), "test_adapter");
        
        // Create test adapter directory if it doesn't exist
        if (!Directory.Exists(_testAdapterPath))
        {
            Directory.CreateDirectory(_testAdapterPath);
        }
    }
    
    [Fact]
    public async Task LoadAdapterAsync_WithValidPath_ShouldReturnAdapterInfo()
    {
        // Arrange
        var adapterManager = new AdapterManager();
        var adapter = CreateTestAdapter();
        
        // Act
        var result = await adapterManager.LoadAdapterAsync(_testAdapterPath);
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal(_testAdapterPath, result.FilePath);
    }
    
    [Fact]
    public async Task LoadAdapterAsync_WithInvalidPath_ShouldThrowException()
    {
        // Arrange
        var adapterManager = new AdapterManager();
        var invalidPath = Path.Combine(Path.GetTempPath(), "invalid_adapter_path");
        
        // Act & Assert
        await Assert.ThrowsAsync<DirectoryNotFoundException>(() => 
            adapterManager.LoadAdapterAsync(invalidPath));
    }
    
    [Fact]
    public async Task GetLatestAdapterAsync_ShouldReturnLatestAdapter()
    {
        // Arrange
        var adapterManager = new AdapterManager();
        var latestAdapter = CreateTestAdapter();
        _mockPublisher.Setup(p => p.GetLatestAdapterAsync())
            .ReturnsAsync(latestAdapter);
        
        // Act
        var result = await adapterManager.GetLatestAdapterAsync(_mockPublisher.Object);
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal(latestAdapter, result);
        _mockPublisher.Verify(p => p.GetLatestAdapterAsync(), Times.Once);
    }
    
    [Fact]
    public async Task InitializeModelWithAdapterAsync_ShouldCallModelServiceInitialize()
    {
        // Arrange
        var adapterManager = new AdapterManager();
        var adapter = CreateTestAdapter();
        _mockModelService.Setup(m => m.InitializeAsync(
                It.IsAny<IAdapterInfo>(), 
                It.IsAny<string>(), 
                It.IsAny<bool>()))
            .Returns(Task.CompletedTask);
        
        // Act
        await adapterManager.InitializeModelWithAdapterAsync(_mockModelService.Object, adapter);
        
        // Assert
        _mockModelService.Verify(m => m.InitializeAsync(
            adapter, 
            null, 
            false), Times.Once);
    }
    
    [Fact]
    public async Task MonitorForNewAdaptersAsync_WhenPublisherAnnouncesAdapter_ShouldRaiseEvent()
    {
        // Arrange
        var adapterManager = new AdapterManager();
        var adapter = CreateTestAdapter();
        var eventRaised = false;
        AdapterEventArgs capturedArgs = null;
        
        // Create a custom publisher that we can trigger events with
        var publisher = new TestAdapterPublisher();
        
        // Subscribe to the adapter manager's event
        adapterManager.NewAdapterAnnounced += (sender, args) => 
        {
            eventRaised = true;
            capturedArgs = args;
        };
        
        // Start monitoring in a background task
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var monitorTask = Task.Run(() => adapterManager.MonitorForNewAdaptersAsync(publisher, cts.Token));
        
        // Give the monitoring task a moment to start
        await Task.Delay(100);
        
        // Raise the event on the publisher
        publisher.RaiseAdapterPublishedEvent(new AdapterEventArgs(adapter));
        
        // Wait for the monitoring to finish
        try 
        {
            await monitorTask;
        }
        catch (OperationCanceledException)
        {
            // Expected when the token is cancelled
        }
        
        // Assert
        Assert.True(eventRaised);
        Assert.NotNull(capturedArgs);
        Assert.Equal(adapter, capturedArgs.AdapterInfo);
    }
    
    [Fact]
    public async Task MonitorForNewAdaptersAsync_WhenCancelled_ShouldReturnWithoutException()
    {
        // Arrange
        var adapterManager = new AdapterManager();
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately
        
        // Act & Assert
        await adapterManager.MonitorForNewAdaptersAsync(_mockPublisher.Object, cts.Token);
        // If we get here without an exception, the test passed
    }
    
    [Fact]
    public async Task MonitorForNewAdaptersAsync_ShouldBeThreadSafe()
    {
        // Arrange
        var adapterManager = new AdapterManager();
        var adapter = CreateTestAdapter();
        var announcedAdapters = new List<IAdapterInfo>();
        var sync = new object();
        
        // Create a custom publisher that we can trigger events with
        var publisher = new TestAdapterPublisher();
        
        // Subscribe to the adapter manager's event
        adapterManager.NewAdapterAnnounced += (sender, args) => 
        {
            lock (sync)
            {
                announcedAdapters.Add(args.AdapterInfo);
            }
        };
        
        // Start monitoring in a background task
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var monitorTask = Task.Run(() => adapterManager.MonitorForNewAdaptersAsync(publisher, cts.Token));
        
        // Give the monitoring task a moment to start
        await Task.Delay(100);
        
        // Raise multiple events on the publisher
        for (int i = 0; i < 5; i++)
        {
            publisher.RaiseAdapterPublishedEvent(new AdapterEventArgs(adapter));
            await Task.Delay(10); // Small delay to ensure events are processed
        }
        
        // Wait for the monitoring to finish
        try 
        {
            await monitorTask;
        }
        catch (OperationCanceledException)
        {
            // Expected when the token is cancelled
        }
        
        // Assert
        Assert.Equal(5, announcedAdapters.Count);
    }
    
    private IAdapterInfo CreateTestAdapter()
    {
        var mockAdapter = new Mock<IAdapterInfo>();
        mockAdapter.Setup(a => a.Name).Returns("TestAdapter");
        mockAdapter.Setup(a => a.FilePath).Returns(_testAdapterPath);
        mockAdapter.Setup(a => a.Created).Returns(DateTime.Now);
        mockAdapter.Setup(a => a.Metadata).Returns(new Dictionary<string, object>
        {
            { "test_key", "test_value" }
        });
        return mockAdapter.Object;
    }
    
    /// <summary>
    /// Test implementation of IAdapterPublisher that allows us to trigger events.
    /// </summary>
    private class TestAdapterPublisher : IAdapterPublisher
    {
        public event EventHandler<AdapterEventArgs> AdapterPublished;
        
        public IReadOnlyList<IAdapterInfo> GetAvailableAdapters()
        {
            return new List<IAdapterInfo>();
        }
        
        public Task<IAdapterInfo> GetLatestAdapterAsync()
        {
            return Task.FromResult<IAdapterInfo>(null);
        }
        
        public void RaiseAdapterPublishedEvent(AdapterEventArgs args)
        {
            AdapterPublished?.Invoke(this, args);
        }
    }
    
    public void Dispose()
    {
        // Clean up test directory if needed
        if (Directory.Exists(_testAdapterPath))
        {
            try
            {
                Directory.Delete(_testAdapterPath, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
} 