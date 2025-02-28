using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Moq;

namespace LLMAdapterClient.Common.Tests;

public class AdapterInfoTests
{
    [Fact]
    public void AdapterInfo_Implementation_HasRequiredProperties()
    {
        // Arrange
        var mockAdapter = new Mock<IAdapterInfo>();
        mockAdapter.Setup(a => a.Name).Returns("TestAdapter");
        mockAdapter.Setup(a => a.FilePath).Returns("/path/to/adapter.bin");
        mockAdapter.Setup(a => a.Created).Returns(DateTime.Now);
        mockAdapter.Setup(a => a.Metadata).Returns(new Dictionary<string, object>());

        // Act
        var adapter = mockAdapter.Object;

        // Assert
        Assert.Equal("TestAdapter", adapter.Name);
        Assert.Equal("/path/to/adapter.bin", adapter.FilePath);
        Assert.NotEqual(default(DateTime), adapter.Created);
        Assert.NotNull(adapter.Metadata);
    }
}

public class AdapterEventArgsTests
{
    [Fact]
    public void AdapterEventArgs_Constructor_SetsAdapterInfo()
    {
        // Arrange
        var mockAdapter = new Mock<IAdapterInfo>();

        // Act
        var args = new AdapterEventArgs(mockAdapter.Object);

        // Assert
        Assert.Same(mockAdapter.Object, args.AdapterInfo);
    }

    [Fact]
    public void AdapterEventArgs_Constructor_ThrowsOnNullAdapter()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new AdapterEventArgs(null!));
    }
}

public class AdapterPublisherTests
{
    [Fact]
    public void AdapterPublisher_GetAvailableAdapters_ReturnsNonNullList()
    {
        // Arrange
        var mockPublisher = new Mock<IAdapterPublisher>();
        mockPublisher.Setup(p => p.GetAvailableAdapters())
            .Returns(new List<IAdapterInfo>().AsReadOnly());

        // Act
        var adapters = mockPublisher.Object.GetAvailableAdapters();

        // Assert
        Assert.NotNull(adapters);
    }

    [Fact]
    public async Task AdapterPublisher_GetLatestAdapterAsync_ReturnsAdapter()
    {
        // Arrange
        var mockAdapter = new Mock<IAdapterInfo>();
        var mockPublisher = new Mock<IAdapterPublisher>();
        mockPublisher.Setup(p => p.GetLatestAdapterAsync())
            .ReturnsAsync(mockAdapter.Object);

        // Act
        var adapter = await mockPublisher.Object.GetLatestAdapterAsync();

        // Assert
        Assert.NotNull(adapter);
        Assert.Same(mockAdapter.Object, adapter);
    }

    [Fact]
    public void AdapterPublisher_AdapterPublishedEvent_CanSubscribeAndRaise()
    {
        // Arrange
        var mockAdapter = new Mock<IAdapterInfo>();
        var mockPublisher = new Mock<IAdapterPublisher>();
        
        bool eventRaised = false;
        IAdapterInfo? raisedAdapter = null;
        
        mockPublisher.Object.AdapterPublished += (sender, args) => {
            eventRaised = true;
            raisedAdapter = args.AdapterInfo;
        };

        // Act
        mockPublisher.Raise(p => p.AdapterPublished += null, EventArgs.Empty, new AdapterEventArgs(mockAdapter.Object));

        // Assert
        Assert.True(eventRaised);
        Assert.Same(mockAdapter.Object, raisedAdapter);
    }
}