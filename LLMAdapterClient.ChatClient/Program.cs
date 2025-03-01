using System;
using System.IO;
using System.Threading.Tasks;
using LLMAdapterClient.ChatClient.Services;
using LLMAdapterClient.Common;
using LLMAdapterClient.Publisher.Services;

namespace LLMAdapterClient.ChatClient;

public class Program
{
    private const string DefaultPythonPath = "llm_training-main/venv/bin/python";
    private const string DefaultScriptPath = "llm_training-main/main.py";
    private const string DefaultCheckpointsPath = "llm_training-main/checkpoints";
    private const string DefaultSharedStoragePath = "~/.local/share/LLMAdapterClient/shared_storage";
    
    public static async Task Main(string[] args)
    {
        Console.WriteLine("LLM Adapter Chat Client");
        Console.WriteLine("======================");
        
        try
        {
            // Create services
            var pythonProcessManager = new PythonProcessManager();
            var modelService = new PythonModelService(pythonProcessManager);
            var adapterManager = new AdapterManager();
            var chatUI = new ConsoleChatUI();
            
            // Create adapter publisher for accessing adapters
            var adapterPublisher = CreateAdapterPublisher();
            
            // Create and start the chat session
            using var chatSession = new ChatSession(modelService, adapterManager, chatUI);
            await chatSession.StartAsync(adapterPublisher);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: {ex.Message}");
            Console.ResetColor();
            
            // Print additional details in debug mode
            #if DEBUG
            Console.WriteLine(ex.ToString());
            #endif
        }
        
        Console.WriteLine("Chat session ended. Press any key to exit...");
        Console.ReadKey();
    }
    
    private static IAdapterPublisher CreateAdapterPublisher()
    {
        // Expand the paths
        var expandedCheckpointsPath = Path.GetFullPath(DefaultCheckpointsPath);
        var expandedStoragePath = DefaultSharedStoragePath.Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        var sharedStoragePath = Path.GetFullPath(expandedStoragePath);
        
        // Ensure the shared storage directory exists
        Directory.CreateDirectory(sharedStoragePath);
        
        // Create the services with proper constructor arguments
        var selector = new AdapterSelector(expandedCheckpointsPath);
        var validator = new AdapterValidator();
        var extractor = new AdapterInfoExtractor();
        var uploader = new AdapterUploader();
        
        return new AdapterPublisherService(
            selector,
            validator,
            extractor,
            uploader,
            sharedStoragePath
        );
    }
}
