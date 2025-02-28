using System;
using System.IO;
using System.Threading.Tasks;
using LLMAdapterClient.Common;
using LLMAdapterClient.Publisher.Services;

namespace LLMAdapterClient.Publisher;

class Program
{
    static async Task Main(string[] args)
    {
        try
        {
            // Get the solution directory path
            var currentDir = Directory.GetCurrentDirectory();
            var solutionDir = FindSolutionDirectory(currentDir) 
                ?? throw new DirectoryNotFoundException("Could not find solution directory");

            // Set up paths
            var checkpointsPath = Path.Combine(solutionDir, "llm_training-main", "checkpoints");
            
            // Use a shared storage location that persists across sessions
            var sharedStoragePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LLMAdapterClient",
                "shared_storage"
            );
            
            Console.WriteLine($"Checkpoints path: {checkpointsPath}");
            Console.WriteLine($"Shared storage: {sharedStoragePath}");

            // Ensure shared storage exists
            Directory.CreateDirectory(sharedStoragePath);

            // Create publisher service
            var publisher = new AdapterPublisherService(
                new AdapterSelector(checkpointsPath),
                new AdapterValidator(),
                new AdapterInfoExtractor(),
                new AdapterUploader(),
                sharedStoragePath
            );

            // Subscribe to adapter published events
            publisher.AdapterPublished += (sender, args) =>
            {
                Console.WriteLine($"\nAdapter published:");
                Console.WriteLine($"  Name: {args.AdapterInfo.Name}");
                Console.WriteLine($"  Path: {args.AdapterInfo.FilePath}");
                Console.WriteLine($"  Created: {args.AdapterInfo.Created}");
                Console.WriteLine("  Metadata:");
                foreach (var (key, value) in args.AdapterInfo.Metadata)
                {
                    Console.WriteLine($"    {key}: {value}");
                }
            };

            // List available adapters
            Console.WriteLine("\nAvailable adapters:");
            var adapters = await publisher.GetAvailableAdaptersAsync();
            foreach (var adapter in adapters)
            {
                var isBest = adapter.Name.Equals("best_model_adapter", StringComparison.OrdinalIgnoreCase);
                Console.WriteLine($"- {adapter.Name} (Created: {adapter.Created}){(isBest ? " [BEST MODEL]" : "")}");
            }

            // Get and publish latest adapter
            Console.WriteLine("\nSelecting best model adapter (or latest if not found)...");
            var selectedAdapter = await publisher.GetLatestAdapterAsync();
            Console.WriteLine($"Selected adapter: {selectedAdapter.Name}");

            Console.WriteLine($"\nAdapters are published to: {sharedStoragePath}");
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
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
}
