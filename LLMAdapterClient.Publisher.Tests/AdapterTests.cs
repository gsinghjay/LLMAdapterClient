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

    private string FindSolutionDirectory(string startPath)
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
} 