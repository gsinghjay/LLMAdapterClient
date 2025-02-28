using Xunit;
using LLMAdapterClient.Common;
using LLMAdapterClient.Publisher.Services;
using System.IO;
using System.Threading.Tasks;
using System;

namespace LLMAdapterClient.Publisher.Tests;

public class AdapterTests
{
    private readonly string _testCheckpointsPath;

    public AdapterTests()
    {
        // Get the solution directory path by finding the .sln file
        var currentDir = Directory.GetCurrentDirectory();
        var solutionDir = FindSolutionDirectory(currentDir);
        if (solutionDir == null)
        {
            throw new DirectoryNotFoundException("Could not find solution directory");
        }
        _testCheckpointsPath = Path.Combine(solutionDir, "llm_training-main", "checkpoints");
        
        if (!Directory.Exists(_testCheckpointsPath))
        {
            throw new DirectoryNotFoundException($"Checkpoints directory not found at {_testCheckpointsPath}");
        }
    }

    private static string? FindSolutionDirectory(string startPath)
    {
        var dir = new DirectoryInfo(startPath);
        while (dir != null)
        {
            if (dir.GetFiles("*.sln").Length > 0)
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        return null;
    }
    
    [Fact]
    public void AdapterSelector_ShouldFindValidAdapterDirectories()
    {
        // Arrange
        var selector = new AdapterSelector(_testCheckpointsPath);

        // Act
        var adapterDirs = selector.GetAvailableAdapterDirectories();

        // Assert
        Assert.Contains(adapterDirs, d => Path.GetFileName(d).Equals("best_model_adapter"));
        Assert.Contains(adapterDirs, d => Path.GetFileName(d).Equals("checkpoint_epoch_2_adapter"));
        Assert.Contains(adapterDirs, d => Path.GetFileName(d).Equals("checkpoint_epoch_4_adapter"));
    }

    [Theory]
    [InlineData("best_model_adapter")]
    [InlineData("checkpoint_epoch_2_adapter")]
    [InlineData("checkpoint_epoch_4_adapter")]
    public void AdapterValidator_ShouldValidateRequiredFiles(string adapterDir)
    {
        // Arrange
        var validator = new AdapterValidator();
        var fullPath = Path.Combine(_testCheckpointsPath, adapterDir);

        // Act
        var isValid = validator.ValidateAdapter(fullPath);

        // Assert
        Assert.True(isValid, $"Adapter directory {adapterDir} should be valid");
        Assert.True(File.Exists(Path.Combine(fullPath, "adapter_config.json")));
        Assert.True(File.Exists(Path.Combine(fullPath, "adapter_model.safetensors")));
        Assert.True(File.Exists(Path.Combine(fullPath, "metadata.pt")));
    }

    [Fact]
    public async Task AdapterInfoExtractor_ShouldExtractMetadata()
    {
        // Arrange
        var extractor = new AdapterInfoExtractor();
        var adapterPath = Path.Combine(_testCheckpointsPath, "best_model_adapter");

        // Act
        var adapterInfo = await extractor.ExtractAdapterInfoAsync(adapterPath);

        // Assert
        Assert.NotNull(adapterInfo);
        Assert.Equal("best_model_adapter", adapterInfo.Name);
        Assert.Equal(adapterPath, adapterInfo.FilePath);
        Assert.NotNull(adapterInfo.Metadata);
        Assert.True(adapterInfo.Created <= DateTime.UtcNow);
    }

    [Fact]
    public async Task AdapterUploader_ShouldUploadAdapterToStorage()
    {
        // Arrange
        var sourceAdapter = Path.Combine(_testCheckpointsPath, "best_model_adapter");
        var targetStorage = Path.Combine(Path.GetTempPath(), "adapter_storage", Guid.NewGuid().ToString());
        Directory.CreateDirectory(targetStorage);
        
        try
        {
            var uploader = new AdapterUploader();

            // Act
            var uploadPath = await uploader.UploadAdapterAsync(sourceAdapter, targetStorage);

            // Assert
            Assert.NotNull(uploadPath);
            Assert.True(Directory.Exists(uploadPath));
            Assert.True(File.Exists(Path.Combine(uploadPath, "adapter_config.json")));
            Assert.True(File.Exists(Path.Combine(uploadPath, "adapter_model.safetensors")));
            Assert.True(File.Exists(Path.Combine(uploadPath, "metadata.pt")));
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(targetStorage))
            {
                Directory.Delete(targetStorage, true);
            }
        }
    }

    [Fact]
    public async Task AdapterUploader_ShouldThrowOnInvalidSource()
    {
        // Arrange
        var invalidSource = Path.Combine(_testCheckpointsPath, "nonexistent_adapter");
        var targetStorage = Path.Combine(Path.GetTempPath(), "adapter_storage");
        var uploader = new AdapterUploader();

        // Act & Assert
        await Assert.ThrowsAsync<DirectoryNotFoundException>(() => 
            uploader.UploadAdapterAsync(invalidSource, targetStorage));
    }

    [Fact]
    public async Task AdapterUploader_ShouldThrowOnInvalidTarget()
    {
        // Arrange
        var sourceAdapter = Path.Combine(_testCheckpointsPath, "best_model_adapter");
        var invalidTarget = Path.Combine("Z:", "invalid_storage"); // Using an invalid drive letter
        var uploader = new AdapterUploader();

        // Act & Assert
        await Assert.ThrowsAsync<DirectoryNotFoundException>(() => 
            uploader.UploadAdapterAsync(sourceAdapter, invalidTarget));
    }

    [Fact]
    public async Task AdapterPublisherService_ShouldPublishAdapter()
    {
        // Arrange
        var targetStorage = Path.Combine(Path.GetTempPath(), "publisher_storage", Guid.NewGuid().ToString());
        Directory.CreateDirectory(targetStorage);
        
        try
        {
            var publisher = new AdapterPublisherService(
                new AdapterSelector(_testCheckpointsPath),
                new AdapterValidator(),
                new AdapterInfoExtractor(),
                new AdapterUploader(),
                targetStorage
            );

            var eventRaised = false;
            IAdapterInfo? publishedAdapter = null;
            publisher.AdapterPublished += (sender, args) =>
            {
                eventRaised = true;
                publishedAdapter = args.AdapterInfo;
            };

            // Act
            var adapters = await publisher.GetAvailableAdaptersAsync();
            var latestAdapter = await publisher.GetLatestAdapterAsync();

            // Assert
            Assert.NotEmpty(adapters);
            Assert.NotNull(latestAdapter);
            Assert.True(eventRaised);
            Assert.NotNull(publishedAdapter);
            Assert.Equal(latestAdapter.Name, publishedAdapter.Name);
            Assert.True(Directory.Exists(publishedAdapter.FilePath));
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(targetStorage))
            {
                Directory.Delete(targetStorage, true);
            }
        }
    }

    [Fact]
    public async Task AdapterPublisherService_ShouldHandleConcurrentRequests()
    {
        // Arrange
        var targetStorage = Path.Combine(Path.GetTempPath(), "publisher_storage", Guid.NewGuid().ToString());
        Directory.CreateDirectory(targetStorage);
        
        try
        {
            var publisher = new AdapterPublisherService(
                new AdapterSelector(_testCheckpointsPath),
                new AdapterValidator(),
                new AdapterInfoExtractor(),
                new AdapterUploader(),
                targetStorage
            );

            // Act
            var tasks = new Task[] 
            {
                publisher.GetAvailableAdaptersAsync(),
                publisher.GetAvailableAdaptersAsync(),
                publisher.GetLatestAdapterAsync(),
                publisher.GetLatestAdapterAsync()
            };

            await Task.WhenAll(tasks);

            // Assert
            foreach (var task in tasks)
            {
                Assert.True(task.IsCompletedSuccessfully);
            }
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(targetStorage))
            {
                Directory.Delete(targetStorage, true);
            }
        }
    }

    [Fact]
    public void AdapterPublisherService_ShouldThrowOnInvalidPaths()
    {
        // Arrange
        var invalidCheckpoints = Path.Combine(_testCheckpointsPath, "nonexistent");
        var invalidStorage = Path.Combine("Z:", "invalid_storage");

        // Act & Assert
        Assert.Throws<DirectoryNotFoundException>(() => new AdapterPublisherService(
            new AdapterSelector(invalidCheckpoints),
            new AdapterValidator(),
            new AdapterInfoExtractor(),
            new AdapterUploader(),
            invalidStorage
        ));
    }
} 