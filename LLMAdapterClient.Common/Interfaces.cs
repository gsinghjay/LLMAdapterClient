using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LLMAdapterClient.Common;

/// <summary>
/// Represents metadata for an LLM adapter
/// </summary>
public interface IAdapterInfo
{
    /// <summary>
    /// The name of the adapter
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// The file path where the adapter is stored
    /// </summary>
    string FilePath { get; }
    
    /// <summary>
    /// When the adapter was created
    /// </summary>
    DateTime Created { get; }
    
    /// <summary>
    /// Additional metadata associated with the adapter
    /// </summary>
    Dictionary<string, object> Metadata { get; }
}

/// <summary>
/// Event arguments for adapter-related events
/// </summary>
public class AdapterEventArgs : EventArgs
{
    /// <summary>
    /// The adapter information associated with the event
    /// </summary>
    public IAdapterInfo AdapterInfo { get; }

    /// <summary>
    /// Initializes a new instance of the AdapterEventArgs class
    /// </summary>
    /// <param name="adapterInfo">The adapter information</param>
    public AdapterEventArgs(IAdapterInfo adapterInfo)
    {
        AdapterInfo = adapterInfo ?? throw new ArgumentNullException(nameof(adapterInfo));
    }
}

/// <summary>
/// Interface for publishing adapters
/// </summary>
public interface IAdapterPublisher
{
    /// <summary>
    /// Event that is triggered when a new adapter is published
    /// </summary>
    event EventHandler<AdapterEventArgs> AdapterPublished;
    
    /// <summary>
    /// Gets a list of all available adapters
    /// </summary>
    /// <returns>A read-only list of available adapter information</returns>
    IReadOnlyList<IAdapterInfo> GetAvailableAdapters();
    
    /// <summary>
    /// Gets the latest adapter asynchronously
    /// </summary>
    /// <returns>A task that resolves to the latest adapter information</returns>
    Task<IAdapterInfo> GetLatestAdapterAsync();
}

/// <summary>
/// Interface for managing a Python process for interacting with LLM models
/// </summary>
public interface IPythonProcessManager
{
    /// <summary>
    /// Event that is triggered when the Python process outputs a line
    /// </summary>
    event EventHandler<string> OutputReceived;
    
    /// <summary>
    /// Event that is triggered when the Python process outputs an error
    /// </summary>
    event EventHandler<string> ErrorReceived;
    
    /// <summary>
    /// Starts the Python process with the specified parameters
    /// </summary>
    /// <param name="pythonPath">Path to the Python executable</param>
    /// <param name="scriptPath">Path to the Python script to execute</param>
    /// <param name="args">Additional command-line arguments</param>
    /// <returns>A task that completes when the process is started and ready</returns>
    Task StartAsync(string pythonPath, string scriptPath, string[] args);
    
    /// <summary>
    /// Sends a command to the Python process and waits for a complete response
    /// </summary>
    /// <param name="command">The command to send</param>
    /// <param name="token">Cancellation token</param>
    /// <returns>A task that resolves to the complete response from the Python process</returns>
    Task<string> SendCommandAsync(string command, CancellationToken token = default);
    
    /// <summary>
    /// Sends a command to the Python process and returns a stream of token responses
    /// </summary>
    /// <param name="command">The command to send</param>
    /// <param name="token">Cancellation token</param>
    /// <returns>An async enumerable of token responses from the Python process</returns>
    IAsyncEnumerable<string> SendCommandStreamingAsync(string command, CancellationToken token = default);
    
    /// <summary>
    /// Stops the Python process
    /// </summary>
    /// <returns>A task that completes when the process is stopped</returns>
    Task StopAsync();
    
    /// <summary>
    /// Gets a value indicating whether the Python process is running
    /// </summary>
    bool IsRunning { get; }
}

/// <summary>
/// Interface for interacting with the LLM model through a Python process
/// </summary>
public interface IModelService
{
    /// <summary>
    /// Event that is triggered when the model service outputs a message
    /// </summary>
    event EventHandler<string> MessageReceived;
    
    /// <summary>
    /// Event that is triggered when the model service encounters an error
    /// </summary>
    event EventHandler<string> ErrorReceived;
    
    /// <summary>
    /// Gets a value indicating whether the model service is initialized and ready
    /// </summary>
    bool IsInitialized { get; }
    
    /// <summary>
    /// Gets the current adapter being used by the model service
    /// </summary>
    IAdapterInfo? CurrentAdapter { get; }
    
    /// <summary>
    /// Initializes the model service with the specified adapter
    /// </summary>
    /// <param name="adapter">The adapter to use</param>
    /// <param name="configPath">Optional path to a configuration file</param>
    /// <param name="skipValidation">Whether to skip validation of adapter and config file paths (for testing)</param>
    /// <returns>A task that completes when the model is initialized</returns>
    Task InitializeAsync(IAdapterInfo adapter, string? configPath = null, bool skipValidation = false);
    
    /// <summary>
    /// Generates a response to the specified prompt
    /// </summary>
    /// <param name="prompt">The user's prompt</param>
    /// <param name="token">Cancellation token</param>
    /// <returns>A task that resolves to the complete model response</returns>
    Task<string> GenerateResponseAsync(string prompt, CancellationToken token = default);
    
    /// <summary>
    /// Generates a streaming response to the specified prompt
    /// </summary>
    /// <param name="prompt">The user's prompt</param>
    /// <param name="token">Cancellation token</param>
    /// <returns>An async enumerable of token responses from the model</returns>
    IAsyncEnumerable<string> GenerateStreamingResponseAsync(string prompt, CancellationToken token = default);
    
    /// <summary>
    /// Executes a special command
    /// </summary>
    /// <param name="command">The command to execute (e.g., /clear, /loadrag)</param>
    /// <returns>A task that completes when the command has been executed</returns>
    Task ExecuteSpecialCommandAsync(string command);
    
    /// <summary>
    /// Shuts down the model service and releases resources
    /// </summary>
    /// <returns>A task that completes when the service is shut down</returns>
    Task ShutdownAsync();
}

/// <summary>
/// Interface for managing adapters for the chat client
/// </summary>
public interface IAdapterManager
{
    /// <summary>
    /// Event that is triggered when a new adapter is announced
    /// </summary>
    event EventHandler<AdapterEventArgs> NewAdapterAnnounced;
    
    /// <summary>
    /// Loads an adapter from the specified path
    /// </summary>
    /// <param name="adapterPath">Path to the adapter</param>
    /// <returns>A task that resolves to the adapter information</returns>
    Task<IAdapterInfo> LoadAdapterAsync(string adapterPath);
    
    /// <summary>
    /// Gets the latest adapter from the publisher
    /// </summary>
    /// <param name="publisher">The adapter publisher</param>
    /// <returns>A task that resolves to the latest adapter information</returns>
    Task<IAdapterInfo> GetLatestAdapterAsync(IAdapterPublisher publisher);
    
    /// <summary>
    /// Initializes the model service with the specified adapter
    /// </summary>
    /// <param name="modelService">The model service to initialize</param>
    /// <param name="adapter">The adapter to use</param>
    /// <returns>A task that completes when the model service is initialized</returns>
    Task InitializeModelWithAdapterAsync(IModelService modelService, IAdapterInfo adapter);
    
    /// <summary>
    /// Monitors for new adapters from the publisher
    /// </summary>
    /// <param name="publisher">The adapter publisher to monitor</param>
    /// <param name="token">Cancellation token</param>
    /// <returns>A task that completes when monitoring is stopped</returns>
    Task MonitorForNewAdaptersAsync(IAdapterPublisher publisher, CancellationToken token = default);
}
