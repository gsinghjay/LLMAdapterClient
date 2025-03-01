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
