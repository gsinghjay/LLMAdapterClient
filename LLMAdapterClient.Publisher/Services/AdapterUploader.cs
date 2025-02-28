using System;
using System.IO;
using System.Threading.Tasks;

namespace LLMAdapterClient.Publisher.Services;

/// <summary>
/// Service for uploading adapter files to shared storage
/// </summary>
public class AdapterUploader
{
    /// <summary>
    /// Uploads an adapter directory to the target storage location
    /// </summary>
    /// <param name="sourcePath">Path to the source adapter directory</param>
    /// <param name="targetStorage">Path to the target storage directory</param>
    /// <returns>The path where the adapter was uploaded</returns>
    /// <exception cref="ArgumentNullException">Thrown when sourcePath or targetStorage is null</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown when source or target directory doesn't exist</exception>
    public async Task<string> UploadAdapterAsync(string sourcePath, string targetStorage)
    {
        if (string.IsNullOrEmpty(sourcePath))
            throw new ArgumentNullException(nameof(sourcePath));
        
        if (string.IsNullOrEmpty(targetStorage))
            throw new ArgumentNullException(nameof(targetStorage));

        if (!Directory.Exists(sourcePath))
            throw new DirectoryNotFoundException($"Source adapter directory not found: {sourcePath}");

        if (!Directory.Exists(targetStorage))
            throw new DirectoryNotFoundException($"Target storage directory not found: {targetStorage}");

        // Create a unique directory for this adapter
        var adapterName = Path.GetFileName(sourcePath);
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var targetPath = Path.Combine(targetStorage, $"{adapterName}_{timestamp}");
        
        // Ensure target directory exists
        Directory.CreateDirectory(targetPath);

        try
        {
            // Copy all files from source to target
            foreach (var file in Directory.GetFiles(sourcePath))
            {
                var fileName = Path.GetFileName(file);
                var targetFile = Path.Combine(targetPath, fileName);
                
                // Use async file copy for large files
                using (var sourceStream = new FileStream(file, FileMode.Open, FileAccess.Read))
                using (var targetStream = new FileStream(targetFile, FileMode.Create, FileAccess.Write))
                {
                    await sourceStream.CopyToAsync(targetStream);
                }
            }

            return targetPath;
        }
        catch (Exception)
        {
            // Clean up on failure
            if (Directory.Exists(targetPath))
            {
                Directory.Delete(targetPath, true);
            }
            throw;
        }
    }
} 