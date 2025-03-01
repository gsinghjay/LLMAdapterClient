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
    private const string DefaultPythonPath = "llm_training-main/venv/bin/python";
    private const string DefaultScriptPath = "llm_training-main/main.py";
    private const string DefaultConfigPath = "llm_training-main/config.yaml";
    
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
    /// Creates a temporary config file with the adapter path
    /// </summary>
    /// <param name="baseConfigPath">Path to the base config file</param>
    /// <param name="adapterPath">Path to the adapter</param>
    /// <returns>Path to the temporary config file</returns>
    private async Task<string> CreateTemporaryConfigAsync(string baseConfigPath, string adapterPath)
    {
        // Read the base config file
        string configContent = await File.ReadAllTextAsync(baseConfigPath);
        
        // Add or update the adapter path
        if (configContent.Contains("adapter_path:"))
        {
            // Replace existing adapter path
            configContent = System.Text.RegularExpressions.Regex.Replace(
                configContent,
                @"adapter_path:\s*.*",
                $"adapter_path: {adapterPath}");
        }
        else
        {
            // Add adapter path at the end
            configContent += $"\nadapter_path: {adapterPath}";
        }
        
        // Create a temporary file
        string tempPath = Path.Combine(
            Path.GetDirectoryName(baseConfigPath) ?? ".",
            $"config_{Path.GetFileNameWithoutExtension(Path.GetRandomFileName())}.yaml");
        
        // Write the modified config
        await File.WriteAllTextAsync(tempPath, configContent);
        
        return tempPath;
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
        string? tempConfigPath = null;
        
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
            var args = new List<string> { "--mode", "chat" };
            
            // Create a temporary config file with the adapter path
            string baseConfigPath = configPath ?? DefaultConfigPath;
            if (!skipValidation && !File.Exists(baseConfigPath))
            {
                throw new FileNotFoundException($"Config file not found: {baseConfigPath}");
            }
            
            tempConfigPath = await CreateTemporaryConfigAsync(baseConfigPath, adapter.FilePath);
            
            // Add config path
            args.Add("--config");
            args.Add(tempConfigPath);
            
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
            // Clean up temporary config file
            if (tempConfigPath != null && File.Exists(tempConfigPath))
            {
                try
                {
                    File.Delete(tempConfigPath);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
            
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
        linkedCts.Token.ThrowIfCancellationRequested();
        
        IAsyncEnumerable<string> responseStream;
        
        try
        {
            responseStream = _processManager.SendCommandStreamingAsync(prompt, linkedCts.Token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            OnErrorReceived($"Error generating streaming response: {ex.Message}");
            throw;
        }
        
        bool isInResponse = false;
        string? pendingToken = null;
        var responseTimeout = TimeSpan.FromSeconds(30);
        var lastTokenTime = DateTime.UtcNow;
        
        await using var enumerator = responseStream.GetAsyncEnumerator(linkedCts.Token);
        
        try
        {
            while (await enumerator.MoveNextAsync())
            {
                var tokenValue = enumerator.Current;
                
                // Check for timeout between tokens
                if (DateTime.UtcNow - lastTokenTime > responseTimeout)
                {
                    throw new TimeoutException("Response stream timed out between tokens.");
                }
                lastTokenTime = DateTime.UtcNow;
                
                // Skip empty or whitespace-only tokens
                if (string.IsNullOrWhiteSpace(tokenValue))
                {
                    continue;
                }
                
                // Mark that we're in a response if we see the assistant marker
                if (tokenValue.Contains("Assistant:"))
                {
                    isInResponse = true;
                    continue;
                }
                
                // Skip all system messages, logs, and metadata
                if (!isInResponse || 
                    tokenValue.Contains("| INFO |") || 
                    tokenValue.Contains("| WARNING |") || 
                    tokenValue.Contains("| ERROR |") || 
                    tokenValue.Contains("| DEBUG |") ||
                    tokenValue.StartsWith("[") ||
                    tokenValue.StartsWith("System:") ||
                    tokenValue.StartsWith("Bot:") ||
                    tokenValue.StartsWith("User:") ||
                    tokenValue.StartsWith("Human:") ||
                    tokenValue.Contains("Bot is thinking...") ||
                    tokenValue.Contains("You:") ||
                    tokenValue.Contains("Wait,") ||
                    tokenValue.Contains("Therefore,") ||
                    tokenValue.Contains("So, the bot") ||
                    tokenValue.Contains("Maybe I should") ||
                    tokenValue.Contains("Alright,") ||
                    tokenValue.Contains("No relevant RAG context found"))
                {
                    continue;
                }
                
                linkedCts.Token.ThrowIfCancellationRequested();
                
                // Clean up any remaining role prefixes or system text
                var cleanToken = tokenValue
                    .Replace("Assistant:", "")
                    .Replace("Human:", "")
                    .Replace("Bot:", "")
                    .Trim();
                
                if (string.IsNullOrWhiteSpace(cleanToken))
                {
                    continue;
                }
                
                // For test tokens, yield them directly if they don't need cleaning
                if (!tokenValue.Contains("Assistant:") && 
                    !tokenValue.Contains("Human:") && 
                    !tokenValue.Contains("Bot:"))
                {
                    yield return cleanToken;
                    continue;
                }
                
                // Accumulate tokens until we have a complete word or punctuation
                if (pendingToken == null)
                {
                    pendingToken = cleanToken;
                }
                else
                {
                    pendingToken += cleanToken;
                }
                
                // Output complete words or when we have punctuation
                if (pendingToken.EndsWith(" ") || 
                    pendingToken.EndsWith(".") || 
                    pendingToken.EndsWith("!") || 
                    pendingToken.EndsWith("?") ||
                    pendingToken.EndsWith(",") ||
                    pendingToken.EndsWith(":") ||
                    pendingToken.EndsWith(";"))
                {
                    // Clean up any double spaces that might have been created
                    var finalToken = pendingToken.Replace("  ", " ").Trim();
                    if (!string.IsNullOrWhiteSpace(finalToken))
                    {
                        yield return finalToken + (pendingToken.EndsWith(" ") ? " " : "");
                    }
                    pendingToken = null;
                }
            }
            
            // Output any remaining pending token
            if (pendingToken != null)
            {
                var finalToken = pendingToken.Replace("  ", " ").Trim();
                if (!string.IsNullOrWhiteSpace(finalToken))
                {
                    yield return finalToken;
                }
            }
        }
        finally
        {
            // Ensure we dispose of the enumerator
            await enumerator.DisposeAsync();
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