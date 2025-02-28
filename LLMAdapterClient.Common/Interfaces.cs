using System;
using System.Collections.Generic;
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
