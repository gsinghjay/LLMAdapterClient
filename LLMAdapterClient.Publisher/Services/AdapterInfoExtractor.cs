using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using LLMAdapterClient.Common;

namespace LLMAdapterClient.Publisher.Services;

/// <summary>
/// Service for extracting metadata from adapter files
/// </summary>
public class AdapterInfoExtractor
{
    /// <summary>
    /// Extracts adapter information from the adapter directory
    /// </summary>
    /// <param name="adapterPath">Path to the adapter directory</param>
    /// <returns>Adapter information</returns>
    public async Task<IAdapterInfo> ExtractAdapterInfoAsync(string adapterPath)
    {
        if (string.IsNullOrEmpty(adapterPath))
        {
            throw new ArgumentNullException(nameof(adapterPath));
        }

        if (!Directory.Exists(adapterPath))
        {
            throw new DirectoryNotFoundException($"Adapter directory not found: {adapterPath}");
        }

        var configPath = Path.Combine(adapterPath, "adapter_config.json");
        var metadata = await ExtractMetadataAsync(configPath);
        
        return new AdapterInfo(
            name: Path.GetFileName(adapterPath),
            filePath: adapterPath,
            created: File.GetCreationTimeUtc(adapterPath),
            metadata: metadata
        );
    }

    private async Task<Dictionary<string, object>> ExtractMetadataAsync(string configPath)
    {
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException($"Config file not found: {configPath}");
        }

        var jsonString = await File.ReadAllTextAsync(configPath);
        var metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonString);
        return metadata ?? new Dictionary<string, object>();
    }
}

/// <summary>
/// Implementation of IAdapterInfo
/// </summary>
internal class AdapterInfo : IAdapterInfo
{
    public string Name { get; }
    public string FilePath { get; }
    public DateTime Created { get; }
    public Dictionary<string, object> Metadata { get; }

    public AdapterInfo(string name, string filePath, DateTime created, Dictionary<string, object> metadata)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        Created = created;
        Metadata = metadata ?? new Dictionary<string, object>();
    }
} 