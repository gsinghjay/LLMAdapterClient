using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LLMAdapterClient.Common;

namespace LLMAdapterClient.Publisher.Services;

/// <summary>
/// Service that combines adapter management functionality to publish adapters
/// </summary>
public class AdapterPublisherService : IAdapterPublisher
{
    private readonly AdapterSelector _selector;
    private readonly AdapterValidator _validator;
    private readonly AdapterInfoExtractor _extractor;
    private readonly AdapterUploader _uploader;
    private readonly string _targetStorage;
    private readonly List<IAdapterInfo> _publishedAdapters;

    public event EventHandler<AdapterEventArgs>? AdapterPublished;

    /// <summary>
    /// Initializes a new instance of the AdapterPublisherService class
    /// </summary>
    /// <param name="selector">Service for selecting adapter directories</param>
    /// <param name="validator">Service for validating adapter files</param>
    /// <param name="extractor">Service for extracting adapter metadata</param>
    /// <param name="uploader">Service for uploading adapters</param>
    /// <param name="targetStorage">Path to the target storage directory</param>
    public AdapterPublisherService(
        AdapterSelector selector,
        AdapterValidator validator,
        AdapterInfoExtractor extractor,
        AdapterUploader uploader,
        string targetStorage)
    {
        _selector = selector ?? throw new ArgumentNullException(nameof(selector));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _extractor = extractor ?? throw new ArgumentNullException(nameof(extractor));
        _uploader = uploader ?? throw new ArgumentNullException(nameof(uploader));
        
        if (string.IsNullOrEmpty(targetStorage))
            throw new ArgumentNullException(nameof(targetStorage));
        
        if (!Directory.Exists(targetStorage))
            throw new DirectoryNotFoundException($"Target storage directory not found: {targetStorage}");
        
        _targetStorage = targetStorage;
        _publishedAdapters = new List<IAdapterInfo>();
    }

    /// <summary>
    /// Gets a list of all available adapters asynchronously
    /// </summary>
    /// <returns>A task that resolves to a read-only list of available adapter information</returns>
    public async Task<IReadOnlyList<IAdapterInfo>> GetAvailableAdaptersAsync()
    {
        var validAdapters = _selector.GetAvailableAdapterDirectories()
            .Where(dir => _validator.ValidateAdapter(dir))
            .ToList();

        var tasks = validAdapters.Select(dir => _extractor.ExtractAdapterInfoAsync(dir));
        var adapters = await Task.WhenAll(tasks);
        return adapters.ToList().AsReadOnly();
    }

    /// <summary>
    /// Gets a list of all available adapters
    /// </summary>
    /// <returns>A read-only list of available adapter information</returns>
    [Obsolete("Use GetAvailableAdaptersAsync instead")]
    public IReadOnlyList<IAdapterInfo> GetAvailableAdapters()
    {
        return GetAvailableAdaptersAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Gets the latest adapter asynchronously, prioritizing best_model_adapter if available
    /// </summary>
    /// <returns>A task that resolves to the latest adapter information</returns>
    public async Task<IAdapterInfo> GetLatestAdapterAsync()
    {
        var adapters = await GetAvailableAdaptersAsync();
        if (!adapters.Any())
            throw new InvalidOperationException("No valid adapters found");

        // First try to find best_model_adapter
        var bestModelAdapter = adapters.FirstOrDefault(a => a.Name.Equals("best_model_adapter", StringComparison.OrdinalIgnoreCase));
        
        // If best_model_adapter is not found, fall back to the latest by creation date
        var selectedAdapter = bestModelAdapter ?? adapters.OrderByDescending(a => a.Created).First();

        // Upload the adapter to storage if it hasn't been published yet
        if (!_publishedAdapters.Any(a => a.Name == selectedAdapter.Name))
        {
            var uploadedPath = await _uploader.UploadAdapterAsync(selectedAdapter.FilePath, _targetStorage);
            var publishedAdapter = await _extractor.ExtractAdapterInfoAsync(uploadedPath);
            
            _publishedAdapters.Add(publishedAdapter);
            OnAdapterPublished(publishedAdapter);
            
            return publishedAdapter;
        }

        return selectedAdapter;
    }

    /// <summary>
    /// Raises the AdapterPublished event
    /// </summary>
    /// <param name="adapterInfo">Information about the published adapter</param>
    protected virtual void OnAdapterPublished(IAdapterInfo adapterInfo)
    {
        AdapterPublished?.Invoke(this, new AdapterEventArgs(adapterInfo));
    }
} 