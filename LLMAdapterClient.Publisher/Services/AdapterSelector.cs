using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LLMAdapterClient.Publisher.Services;

/// <summary>
/// Service for selecting adapter directories from the checkpoints folder
/// </summary>
public class AdapterSelector
{
    private readonly string _checkpointsPath;

    /// <summary>
    /// Initializes a new instance of the AdapterSelector class
    /// </summary>
    /// <param name="checkpointsPath">Path to the checkpoints directory</param>
    public AdapterSelector(string checkpointsPath)
    {
        _checkpointsPath = checkpointsPath ?? throw new ArgumentNullException(nameof(checkpointsPath));
        
        if (!Directory.Exists(checkpointsPath))
        {
            throw new DirectoryNotFoundException($"Checkpoints directory not found: {checkpointsPath}");
        }
    }

    /// <summary>
    /// Gets all available adapter directories in the checkpoints folder
    /// </summary>
    /// <returns>A list of adapter directory paths</returns>
    public IEnumerable<string> GetAvailableAdapterDirectories()
    {
        return Directory.GetDirectories(_checkpointsPath)
            .Where(dir => Path.GetFileName(dir).Contains("adapter", StringComparison.OrdinalIgnoreCase));
    }
} 