using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using LLMAdapterClient.Common;

namespace LLMAdapterClient.ChatClient.Services;

/// <summary>
/// Implementation of the IModelService interface that interacts with a Python-powered LLM through the PythonProcessManager
/// </summary>
public class PythonModelService : IModelService, IDisposable
{
    private readonly IPythonProcessManager _processManager;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private readonly CancellationTokenSource _disposalTokenSource = new();
    private bool _isDisposed;
    
    // Default paths - these could be moved to configuration
    private const string DefaultPythonPath = "python";
    private const string DefaultScriptPath = "llm_training-main/main.py";
    
    // Special command prefixes
    private const string ClearCommand = "/clear";
    private const string LoadRagCommand = "/loadrag";
    private const string RagStatusCommand = "/ragstatus";
    private const string HelpCommand = "/help";
    
    /// <summary>
    /// Event that is triggered when the model service outputs a message
    /// </summary>
    public event EventHandler<string>? MessageReceived;
    
    /// <summary>
    /// Event that is triggered when the model service encounters an error
    /// </summary>
    public event EventHandler<string>? ErrorReceived;
    
    /// <summary>
    /// Gets a value indicating whether the model service is initialized and ready
    /// </summary>
    public bool IsInitialized { get; private set; }
    
    /// <summary>
    /// Gets the current adapter being used by the model service
    /// </summary>
    public IAdapterInfo? CurrentAdapter { get; private set; }
    
    /// <summary>
    /// Initializes a new instance of the PythonModelService class
    /// </summary>
    /// <param name="processManager">The Python process manager to use</param>
    public PythonModelService(IPythonProcessManager processManager)
    {
        _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
        
        // Hook up event handlers
        _processManager.OutputReceived += (sender, message) => MessageReceived?.Invoke(this, message);
        _processManager.ErrorReceived += (sender, message) => ErrorReceived?.Invoke(this, message);
    }
    
    /// <summary>
    /// Initializes the model service with the specified adapter
    /// </summary>
    /// <param name="adapter">The adapter to use</param>
    /// <param name="configPath">Optional path to a configuration file</param>
    /// <param name="skipValidation">Whether to skip validation of adapter and config file paths (for testing)</param>
    /// <returns>A task that completes when the model is initialized</returns>
    public async Task InitializeAsync(IAdapterInfo adapter, string? configPath = null, bool skipValidation = false)
    {
        if (adapter == null)
        {
            throw new ArgumentNullException(nameof(adapter));
        }
        
        await _initializationLock.WaitAsync();
        try
        {
            // Check if we need to shut down existing process
            if (_processManager.IsRunning)
            {
                await _processManager.StopAsync();
            }
            
            // Validate adapter path if validation is not skipped
            if (!skipValidation && !Directory.Exists(adapter.FilePath))
            {
                throw new DirectoryNotFoundException($"Adapter directory not found: {adapter.FilePath}");
            }
            
            // Build arguments list
            var args = new List<string> { "--mode", "chat", "--adapter_path", adapter.FilePath };
            
            // Add config path if specified
            if (!string.IsNullOrEmpty(configPath))
            {
                if (!skipValidation && !File.Exists(configPath))
                {
                    throw new FileNotFoundException($"Config file not found: {configPath}");
                }
                
                args.Add("--config");
                args.Add(configPath);
            }
            
            // Start the Python process
            await _processManager.StartAsync(DefaultPythonPath, DefaultScriptPath, args.ToArray());
            
            // Update state
            CurrentAdapter = adapter;
            IsInitialized = true;
            
            // Log initialization
            OnMessageReceived($"Model initialized with adapter: {adapter.Name}");
        }
        catch (Exception ex)
        {
            OnErrorReceived($"Failed to initialize model: {ex.Message}");
            throw;
        }
        finally
        {
            _initializationLock.Release();
        }
    }
    
    /// <summary>
    /// Generates a response to the specified prompt
    /// </summary>
    /// <param name="prompt">The user's prompt</param>
    /// <param name="token">Cancellation token</param>
    /// <returns>A task that resolves to the complete model response</returns>
    public async Task<string> GenerateResponseAsync(string prompt, CancellationToken token = default)
    {
        EnsureInitialized();
        
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, _disposalTokenSource.Token);
        
        try
        {
            return await _processManager.SendCommandAsync(prompt, linkedCts.Token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Rethrow if it was the caller's token that was canceled
            throw;
        }
        catch (Exception ex)
        {
            OnErrorReceived($"Error generating response: {ex.Message}");
            throw;
        }
    }
    
    /// <summary>
    /// Generates a streaming response to the specified prompt
    /// </summary>
    /// <param name="prompt">The user's prompt</param>
    /// <param name="token">Cancellation token</param>
    /// <returns>An async enumerable of token responses from the model</returns>
    public async IAsyncEnumerable<string> GenerateStreamingResponseAsync(
        string prompt,
        [EnumeratorCancellation] CancellationToken token = default)
    {
        EnsureInitialized();
        
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, _disposalTokenSource.Token);
        
        IAsyncEnumerable<string> responseStream;
        
        try
        {
            responseStream = _processManager.SendCommandStreamingAsync(prompt, linkedCts.Token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Rethrow if it was the caller's token that was canceled
            throw;
        }
        catch (Exception ex)
        {
            OnErrorReceived($"Error generating streaming response: {ex.Message}");
            throw;
        }
        
        await foreach (var tokenValue in responseStream)
        {
            yield return tokenValue;
        }
    }
    
    /// <summary>
    /// Executes a special command
    /// </summary>
    /// <param name="command">The command to execute (e.g., /clear, /loadrag)</param>
    /// <returns>A task that completes when the command has been executed</returns>
    public async Task ExecuteSpecialCommandAsync(string command)
    {
        EnsureInitialized();
        
        if (string.IsNullOrEmpty(command))
        {
            throw new ArgumentNullException(nameof(command));
        }
        
        try
        {
            // Handle specific commands differently if needed
            if (command.StartsWith(LoadRagCommand, StringComparison.OrdinalIgnoreCase))
            {
                // Might need special handling for RAG setup
                OnMessageReceived("Loading RAG documents...");
            }
            
            // Send the command to the Python process
            var response = await _processManager.SendCommandAsync(command);
            
            // Log the response
            OnMessageReceived(response);
        }
        catch (Exception ex)
        {
            OnErrorReceived($"Error executing command {command}: {ex.Message}");
            throw;
        }
    }
    
    /// <summary>
    /// Shuts down the model service and releases resources
    /// </summary>
    /// <returns>A task that completes when the service is shut down</returns>
    public async Task ShutdownAsync()
    {
        if (!_processManager.IsRunning)
        {
            return;
        }
        
        try
        {
            await _processManager.StopAsync();
            IsInitialized = false;
            CurrentAdapter = null;
        }
        catch (Exception ex)
        {
            OnErrorReceived($"Error shutting down model: {ex.Message}");
            throw;
        }
    }
    
    /// <summary>
    /// Disposes the model service and releases resources
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
    /// <summary>
    /// Disposes the model service and releases resources
    /// </summary>
    /// <param name="disposing">Whether to dispose managed resources</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_isDisposed)
        {
            return;
        }
        
        if (disposing)
        {
            _disposalTokenSource.Cancel();
            _disposalTokenSource.Dispose();
            _initializationLock.Dispose();
            
            // Shut down process manager asynchronously
            if (_processManager.IsRunning)
            {
                _ = _processManager.StopAsync();
            }
            
            if (_processManager is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        
        _isDisposed = true;
    }
    
    /// <summary>
    /// Ensures that the model service is initialized
    /// </summary>
    private void EnsureInitialized()
    {
        if (!IsInitialized || !_processManager.IsRunning)
        {
            throw new InvalidOperationException(
                "Model service is not initialized. Call InitializeAsync before sending commands.");
        }
    }
    
    /// <summary>
    /// Raises the MessageReceived event
    /// </summary>
    /// <param name="message">The message to send</param>
    private void OnMessageReceived(string message)
    {
        MessageReceived?.Invoke(this, message);
    }
    
    /// <summary>
    /// Raises the ErrorReceived event
    /// </summary>
    /// <param name="message">The error message to send</param>
    private void OnErrorReceived(string message)
    {
        ErrorReceived?.Invoke(this, message);
    }
} 