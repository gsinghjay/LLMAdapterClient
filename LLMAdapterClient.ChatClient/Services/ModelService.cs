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
            string rawResponse = await _processManager.SendCommandAsync(prompt, linkedCts.Token);
            return ProcessRawResponse(rawResponse);
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
    /// Processes a raw response from the Python process to extract the actual model response
    /// </summary>
    /// <param name="rawResponse">The raw response from the Python process</param>
    /// <returns>The cleaned and filtered model response</returns>
    private string ProcessRawResponse(string rawResponse)
    {
        if (string.IsNullOrWhiteSpace(rawResponse))
        {
            return string.Empty;
        }
        
        // Remove ANSI color codes
        string cleanResponse = RemoveAnsiCodes(rawResponse);
        
        // Split into lines for easier processing
        var lines = cleanResponse.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        
        // Filter out system messages, logs, and metadata
        var responseLines = new List<string>();
        bool inResponseSection = false;
        bool foundAssistantMarker = false;
        
        foreach (var line in lines)
        {
            // Skip log messages and system notifications
            if (line.Contains("| INFO |") ||
                line.Contains("| WARNING |") ||
                line.Contains("| ERROR |") ||
                line.Contains("| DEBUG |") ||
                line.StartsWith("System:") ||
                line.Contains("Bot is thinking..."))
            {
                continue;
            }
            
            // Detect the start of actual response
            if (line.Contains("Assistant:") || line.Contains("Bot:"))
            {
                foundAssistantMarker = true;
                inResponseSection = true;
                continue;
            }
            
            // Detect the end of response
            if (inResponseSection && 
                (line.Contains("User:") || 
                 line.Contains("Human:") || 
                 line.Contains("You:") || 
                 line.Trim() == ">"))
            {
                break;
            }
            
            // If we're in the response section, add this line
            if (inResponseSection)
            {
                responseLines.Add(line);
            }
            // If we haven't found an assistant marker yet but this isn't a system message,
            // it might be part of the response (in case markers are missing)
            else if (!foundAssistantMarker && 
                     !line.StartsWith("[") && 
                     !line.Contains("COMMAND_COMPLETE") &&
                     !string.IsNullOrWhiteSpace(line))
            {
                responseLines.Add(line);
            }
        }
        
        return string.Join("\n", responseLines);
    }
    
    /// <summary>
    /// Removes ANSI color codes from a string
    /// </summary>
    /// <param name="input">The input string with possible ANSI codes</param>
    /// <returns>The string with ANSI codes removed</returns>
    private string RemoveAnsiCodes(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }
        
        // ANSI escape sequence regex pattern
        // Matches escape sequences like: ESC[ ... m
        // where ESC is the escape character (ASCII 27, represented as \u001b or \x1B)
        return System.Text.RegularExpressions.Regex.Replace(
            input,
            @"\u001b\[\d*(;\d*)*m",
            string.Empty);
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
        bool shouldSkipLine = false;
        var responseTimeout = TimeSpan.FromSeconds(30);
        var lastTokenTime = DateTime.UtcNow;
        var tokenBuffer = new List<string>();
        
        await using var enumerator = responseStream.GetAsyncEnumerator(linkedCts.Token);
        
        try
        {
            while (await enumerator.MoveNextAsync())
            {
                var tokenValue = enumerator.Current;
                
                // Handle empty token
                if (string.IsNullOrEmpty(tokenValue))
                {
                    continue;
                }
                
                // Remove ANSI color codes
                tokenValue = RemoveAnsiCodes(tokenValue);
                
                // Check for timeout between tokens
                if (DateTime.UtcNow - lastTokenTime > responseTimeout)
                {
                    throw new TimeoutException("Response stream timed out between tokens.");
                }
                lastTokenTime = DateTime.UtcNow;
                
                // Process tokens line by line for better content filtering
                if (tokenValue.Contains('\n'))
                {
                    var lines = tokenValue.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        if (ProcessStreamingLine(line, ref isInResponse, ref shouldSkipLine))
                        {
                            tokenBuffer.Add(line);
                            
                            // If the buffer has accumulated enough, emit a token
                            if (tokenBuffer.Count >= 5 || tokenBuffer.Sum(t => t.Length) > 50)
                            {
                                yield return string.Join(" ", tokenBuffer);
                                tokenBuffer.Clear();
                            }
                        }
                    }
                    continue;
                }
                
                // Skip all system messages, logs, and metadata
                if (tokenValue.Contains("| INFO |") || 
                    tokenValue.Contains("| WARNING |") || 
                    tokenValue.Contains("| ERROR |") || 
                    tokenValue.Contains("| DEBUG |") ||
                    tokenValue.StartsWith("[") ||
                    tokenValue.StartsWith("System:") ||
                    tokenValue.Contains("COMMAND_COMPLETE"))
                {
                    continue;
                }
                
                // Mark that we're in a response if we see the assistant marker
                if (tokenValue.Contains("Assistant:") || tokenValue.Contains("Bot:"))
                {
                    isInResponse = true;
                    continue;
                }
                
                // Detect end of response
                if (isInResponse && 
                    (tokenValue.Contains("User:") || 
                     tokenValue.Contains("Human:") || 
                     tokenValue.Contains("You:") || 
                     tokenValue.Trim() == ">"))
                {
                    isInResponse = false;
                    
                    // Emit any remaining tokens in the buffer
                    if (tokenBuffer.Count > 0)
                    {
                        yield return string.Join(" ", tokenBuffer);
                        tokenBuffer.Clear();
                    }
                    
                    break;
                }
                
                // Only emit tokens if we're in the response section
                if (isInResponse && !shouldSkipLine && !string.IsNullOrWhiteSpace(tokenValue))
                {
                    // Add to buffer
                    tokenBuffer.Add(tokenValue);
                    
                    // If the buffer is reasonably sized, emit it
                    if (tokenBuffer.Count >= 3 || tokenBuffer.Sum(t => t.Length) > 20)
                    {
                        yield return string.Join("", tokenBuffer);
                        tokenBuffer.Clear();
                    }
                }
                
                linkedCts.Token.ThrowIfCancellationRequested();
            }
            
            // Emit any remaining tokens in the buffer
            if (tokenBuffer.Count > 0)
            {
                yield return string.Join("", tokenBuffer);
            }
        }
        finally
        {
            tokenBuffer.Clear();
        }
    }
    
    /// <summary>
    /// Processes a line from the streaming response to determine if it should be included
    /// </summary>
    /// <param name="line">The line to process</param>
    /// <param name="isInResponse">Reference to whether we're currently in a response section</param>
    /// <param name="shouldSkipLine">Reference to whether the current line should be skipped</param>
    /// <returns>True if the line should be included in the response, false otherwise</returns>
    private bool ProcessStreamingLine(string line, ref bool isInResponse, ref bool shouldSkipLine)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }
        
        // Skip log messages and system notifications
        if (line.Contains("| INFO |") ||
            line.Contains("| WARNING |") ||
            line.Contains("| ERROR |") ||
            line.Contains("| DEBUG |") ||
            line.StartsWith("[") ||
            line.Contains("COMMAND_COMPLETE") ||
            line.StartsWith("System:") ||
            line.Contains("Bot is thinking..."))
        {
            shouldSkipLine = true;
            return false;
        }
        
        // Detect the start of actual response
        if (line.Contains("Assistant:") || line.Contains("Bot:"))
        {
            isInResponse = true;
            shouldSkipLine = true;
            return false;
        }
        
        // Detect the end of response
        if (isInResponse && 
            (line.Contains("User:") || 
             line.Contains("Human:") || 
             line.Contains("You:") || 
             line.Trim() == ">"))
        {
            isInResponse = false;
            shouldSkipLine = true;
            return false;
        }
        
        shouldSkipLine = false;
        return isInResponse || (!line.StartsWith("User:") && !line.StartsWith("Human:") && !line.StartsWith("You:"));
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