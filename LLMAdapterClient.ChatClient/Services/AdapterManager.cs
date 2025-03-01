using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LLMAdapterClient.Common;

namespace LLMAdapterClient.ChatClient.Services;

/// <summary>
/// Manages adapter loading, initialization, and monitoring
/// </summary>
public class AdapterManager : IAdapterManager
{
    private readonly SemaphoreSlim _monitorLock = new SemaphoreSlim(1, 1);
    private bool _lockTaken = false;
    
    /// <summary>
    /// Initializes a new instance of the AdapterManager class
    /// </summary>
    public AdapterManager()
    {
        // Initialize the event with an empty handler to avoid null reference exception
        NewAdapterAnnounced += (sender, args) => { };
    }
    
    /// <summary>
    /// Event that is triggered when a new adapter is announced
    /// </summary>
    public event EventHandler<AdapterEventArgs> NewAdapterAnnounced;
    
    /// <summary>
    /// Loads an adapter from the specified path
    /// </summary>
    /// <param name="adapterPath">Path to the adapter</param>
    /// <returns>A task that resolves to the adapter information</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown when the adapter path doesn't exist</exception>
    public async Task<IAdapterInfo> LoadAdapterAsync(string adapterPath)
    {
        if (!Directory.Exists(adapterPath))
        {
            throw new DirectoryNotFoundException($"Adapter directory not found: {adapterPath}");
        }
        
        // Create a mock adapter for now - in a real implementation, we would
        // likely use services like AdapterValidator and AdapterInfoExtractor
        // from the Publisher project to properly extract metadata
        return await Task.FromResult(new AdapterInfo(
            Path.GetFileName(adapterPath),
            adapterPath,
            File.GetCreationTime(adapterPath)));
    }
    
    /// <summary>
    /// Gets the latest adapter from the publisher
    /// </summary>
    /// <param name="publisher">The adapter publisher</param>
    /// <returns>A task that resolves to the latest adapter information</returns>
    public async Task<IAdapterInfo> GetLatestAdapterAsync(IAdapterPublisher publisher)
    {
        if (publisher == null)
        {
            throw new ArgumentNullException(nameof(publisher));
        }
        
        return await publisher.GetLatestAdapterAsync();
    }
    
    /// <summary>
    /// Initializes the model service with the specified adapter
    /// </summary>
    /// <param name="modelService">The model service to initialize</param>
    /// <param name="adapter">The adapter to use</param>
    /// <returns>A task that completes when the model service is initialized</returns>
    public async Task InitializeModelWithAdapterAsync(IModelService modelService, IAdapterInfo adapter)
    {
        if (modelService == null)
        {
            throw new ArgumentNullException(nameof(modelService));
        }
        
        if (adapter == null)
        {
            throw new ArgumentNullException(nameof(adapter));
        }
        
        await modelService.InitializeAsync(adapter);
    }
    
    /// <summary>
    /// Monitors for new adapters from the publisher
    /// </summary>
    /// <param name="publisher">The adapter publisher to monitor</param>
    /// <param name="token">Cancellation token</param>
    /// <returns>A task that completes when monitoring is stopped</returns>
    public async Task MonitorForNewAdaptersAsync(IAdapterPublisher publisher, CancellationToken token = default)
    {
        if (publisher == null)
        {
            throw new ArgumentNullException(nameof(publisher));
        }
        
        _lockTaken = false;
        try
        {
            await _monitorLock.WaitAsync(token);
            _lockTaken = true;
            
            // Handler to receive adapter published events
            EventHandler<AdapterEventArgs>? handler = null;
            
            handler = (sender, args) =>
            {
                // Forward the event to our own subscribers
                OnNewAdapterAnnounced(args.AdapterInfo);
            };
            
            // Register for events
            publisher.AdapterPublished += handler;
            
            try
            {
                // Wait until the token is cancelled
                await Task.Delay(-1, token);
            }
            catch (TaskCanceledException)
            {
                // Expected when token is cancelled
            }
            finally
            {
                // Always unregister events
                if (handler != null)
                {
                    publisher.AdapterPublished -= handler;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when token is cancelled
        }
        finally
        {
            // Only release if we acquired the lock
            if (_lockTaken)
            {
                _monitorLock.Release();
                _lockTaken = false;
            }
        }
    }
    
    /// <summary>
    /// Raises the NewAdapterAnnounced event
    /// </summary>
    /// <param name="adapter">The adapter that was announced</param>
    protected virtual void OnNewAdapterAnnounced(IAdapterInfo adapter)
    {
        NewAdapterAnnounced?.Invoke(this, new AdapterEventArgs(adapter));
    }
    
    /// <summary>
    /// A simple adapter info implementation for testing
    /// </summary>
    private class AdapterInfo : IAdapterInfo
    {
        public string Name { get; }
        public string FilePath { get; }
        public DateTime Created { get; }
        public System.Collections.Generic.Dictionary<string, object> Metadata { get; } = 
            new System.Collections.Generic.Dictionary<string, object>();
        
        public AdapterInfo(string name, string filePath, DateTime created)
        {
            Name = name;
            FilePath = filePath;
            Created = created;
        }
    }
} 