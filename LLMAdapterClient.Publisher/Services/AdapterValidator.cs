using System;
using System.IO;
using System.Linq;

namespace LLMAdapterClient.Publisher.Services;

/// <summary>
/// Service for validating adapter directories and their required files
/// </summary>
public class AdapterValidator
{
    private static readonly string[] RequiredFiles = new[]
    {
        "adapter_config.json",
        "adapter_model.safetensors",
        "metadata.pt"
    };

    /// <summary>
    /// Validates that an adapter directory contains all required files
    /// </summary>
    /// <param name="adapterPath">Path to the adapter directory</param>
    /// <returns>True if the adapter is valid, false otherwise</returns>
    public bool ValidateAdapter(string adapterPath)
    {
        if (string.IsNullOrEmpty(adapterPath))
        {
            throw new ArgumentNullException(nameof(adapterPath));
        }

        if (!Directory.Exists(adapterPath))
        {
            return false;
        }

        return RequiredFiles.All(file => File.Exists(Path.Combine(adapterPath, file)));
    }
} 