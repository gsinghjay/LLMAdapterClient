using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using LLMAdapterClient.Common;

namespace LLMAdapterClient.ChatClient.Services;

/// <summary>
/// Manages a Python process for interacting with LLM models
/// </summary>
public class PythonProcessManager : IPythonProcessManager, IDisposable
{
    private Process? _process;
    private StreamWriter? _inputWriter;
    private readonly SemaphoreSlim _sendSemaphore = new(1, 1);
    private readonly TaskCompletionSource<bool> _startTcs = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly Channel<string> _outputChannel = Channel.CreateUnbounded<string>();
    private readonly StringBuilder _outputBuffer = new();
    private bool _isDisposed;
    private bool _isInitialized;
    private bool _isReadyForCommands;
    private readonly TimeSpan _responseTimeout;
    
    // Patterns for detecting response completion
    private static readonly Regex[] PromptPatterns = new[]
    {
        new Regex(@"You:\s*$", RegexOptions.Compiled),
        new Regex(@">\s*$", RegexOptions.Compiled),
        new Regex(@"Human:\s*$", RegexOptions.Compiled),
        new Regex(@"User:\s*$", RegexOptions.Compiled)
    };
    
    /// <summary>
    /// Event that is triggered when the Python process outputs a line
    /// </summary>
    public event EventHandler<string>? OutputReceived;
    
    /// <summary>
    /// Event that is triggered when the Python process outputs an error
    /// </summary>
    public event EventHandler<string>? ErrorReceived;
    
    /// <summary>
    /// Gets a value indicating whether the Python process is running
    /// </summary>
    public bool IsRunning => _process != null && !_process.HasExited;
    
    /// <summary>
    /// Initializes a new instance of the PythonProcessManager class
    /// </summary>
    /// <param name="responseTimeoutSeconds">Timeout in seconds for waiting for a response completion</param>
    public PythonProcessManager(int responseTimeoutSeconds = 30)
    {
        _responseTimeout = TimeSpan.FromSeconds(responseTimeoutSeconds);
    }
    
    /// <summary>
    /// Starts the Python process with the specified parameters
    /// </summary>
    /// <param name="pythonPath">Path to the Python executable</param>
    /// <param name="scriptPath">Path to the Python script to execute</param>
    /// <param name="args">Additional command-line arguments</param>
    /// <returns>A task that completes when the process is started and ready</returns>
    public async Task StartAsync(string pythonPath, string scriptPath, string[] args)
    {
        if (_process != null)
        {
            throw new InvalidOperationException("Python process is already running.");
        }
        
        // Validate paths
        if (!File.Exists(pythonPath))
        {
            throw new FileNotFoundException($"Python executable not found at {pythonPath}");
        }
        
        if (!File.Exists(scriptPath))
        {
            throw new FileNotFoundException($"Python script not found at {scriptPath}");
        }
        
        // Build command-line arguments
        string arguments = $"\"{scriptPath}\" --mode chat {string.Join(" ", args)}";
        
        // Create process start info
        var startInfo = new ProcessStartInfo
        {
            FileName = pythonPath,
            Arguments = arguments,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        
        // Start the process
        _process = new Process { StartInfo = startInfo };
        
        _process.OutputDataReceived += OnOutputDataReceived;
        _process.ErrorDataReceived += OnErrorDataReceived;
        
        try
        {
            if (!_process.Start())
            {
                throw new InvalidOperationException("Failed to start Python process.");
            }
            
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
            
            _inputWriter = _process.StandardInput;
            
            // Wait for process to initialize (looking for a specific message indicating readiness)
            await WaitForInitializationAsync();
            
            _isInitialized = true;
        }
        catch (Exception)
        {
            // Clean up resources on failure
            _process?.Dispose();
            _process = null;
            _inputWriter = null;
            throw;
        }
    }
    
    /// <summary>
    /// Sends a command to the Python process and waits for a response
    /// </summary>
    /// <param name="command">The command to send</param>
    /// <param name="token">Cancellation token</param>
    /// <returns>A task that resolves to the complete response from the Python process</returns>
    public async Task<string> SendCommandAsync(string command, CancellationToken token = default)
    {
        if (!IsRunning || !_isInitialized)
        {
            throw new InvalidOperationException("Python process is not running or not initialized.");
        }
        
        // Acquire semaphore to ensure only one command at a time
        await _sendSemaphore.WaitAsync(token);
        try
        {
            // Clear output buffer
            _outputBuffer.Clear();
            
            // Send command
            await (_inputWriter?.WriteLineAsync(command) ?? Task.CompletedTask);
            await (_inputWriter?.FlushAsync() ?? Task.CompletedTask);
            
            // Wait for response completion
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, _cancellationTokenSource.Token);
            
            // Create a timeout source for detecting when response should be considered complete
            using var timeoutCts = new CancellationTokenSource(_responseTimeout);
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
                linkedCts.Token, timeoutCts.Token);
            
            // Read from the output channel until we receive a signal that the response is complete
            var responseBuilder = new StringBuilder();
            bool responseStarted = false;
            DateTime lastOutputTime = DateTime.UtcNow;
            bool isComplete = false;
            
            try
            {
                while (await _outputChannel.Reader.WaitToReadAsync(combinedCts.Token))
                {
                    if (_outputChannel.Reader.TryRead(out var line))
                    {
                        // Reset timeout when we receive a line
                        lastOutputTime = DateTime.UtcNow;
                        
                        // Check for explicit completion marker if present
                        if (line == "COMMAND_COMPLETE")
                        {
                            isComplete = true;
                            break;
                        }
                        
                        // Check for prompt patterns that indicate completion
                        bool isPrompt = false;
                        foreach (var pattern in PromptPatterns)
                        {
                            if (pattern.IsMatch(line))
                            {
                                isPrompt = true;
                                isComplete = true;
                                break;
                            }
                        }
                        
                        // Don't add the prompt line to the response
                        if (isPrompt)
                        {
                            break;
                        }
                        
                        // Mark that we've started receiving a response
                        responseStarted = true;
                        
                        // Add this line to the response
                        responseBuilder.AppendLine(line);
                    }
                    
                    // Check if we should consider the response complete due to inactivity
                    if (responseStarted && DateTime.UtcNow - lastOutputTime > TimeSpan.FromSeconds(3))
                    {
                        // No output for 3 seconds after response started, consider it complete
                        isComplete = true;
                        break;
                    }
                }
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                // Timeout occurred, return what we have so far
                // If we haven't received any response, this is an error
                if (!responseStarted)
                {
                    throw new TimeoutException("Timed out waiting for response from Python process.");
                }
                
                // Otherwise, assume the response is complete but no prompt was detected
                isComplete = true;
            }
            
            if (isComplete || timeoutCts.IsCancellationRequested)
            {
                return responseBuilder.ToString().TrimEnd();
            }
            else
            {
                throw new OperationCanceledException("Command was canceled before completion.");
            }
        }
        finally
        {
            _sendSemaphore.Release();
        }
    }
    
    /// <summary>
    /// Sends a command to the Python process and returns a stream of token responses
    /// </summary>
    /// <param name="command">The command to send</param>
    /// <param name="token">Cancellation token</param>
    /// <returns>An async enumerable of token responses from the Python process</returns>
    public async IAsyncEnumerable<string> SendCommandStreamingAsync(
        string command,
        [EnumeratorCancellation] CancellationToken token = default)
    {
        if (!IsRunning || !_isInitialized)
        {
            throw new InvalidOperationException("Python process is not running or not initialized.");
        }
        
        // Acquire semaphore to ensure only one command at a time
        await _sendSemaphore.WaitAsync(token);
        
        // Create a channel to stream tokens
        var tokenChannel = Channel.CreateUnbounded<string>();
        
        // Start a task to read from the output channel and write to the token channel
        _ = Task.Run(async () =>
        {
            try
            {
                // Clear output buffer
                _outputBuffer.Clear();
                
                // Send command
                await (_inputWriter?.WriteLineAsync(command) ?? Task.CompletedTask);
                await (_inputWriter?.FlushAsync() ?? Task.CompletedTask);
                
                DateTime lastOutputTime = DateTime.UtcNow;
                bool responseStarted = false;
                
                using var timeoutCts = new CancellationTokenSource(_responseTimeout);
                using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token);
                
                try
                {
                    while (await _outputChannel.Reader.WaitToReadAsync(combinedCts.Token))
                    {
                        if (_outputChannel.Reader.TryRead(out var line))
                        {
                            // Reset timeout when we receive a line
                            lastOutputTime = DateTime.UtcNow;
                            
                            // Check for explicit completion marker if present
                            if (line == "COMMAND_COMPLETE")
                            {
                                break;
                            }
                            
                            // Check for prompt patterns that indicate completion
                            bool isPrompt = false;
                            foreach (var pattern in PromptPatterns)
                            {
                                if (pattern.IsMatch(line))
                                {
                                    isPrompt = true;
                                    break;
                                }
                            }
                            
                            // Don't stream the prompt line
                            if (isPrompt)
                            {
                                break;
                            }
                            
                            // Mark that we've started receiving a response
                            responseStarted = true;
                            
                            // Write tokens to the token channel
                            await tokenChannel.Writer.WriteAsync(line, token);
                        }
                        
                        // Check if we should consider the response complete due to inactivity
                        if (responseStarted && DateTime.UtcNow - lastOutputTime > TimeSpan.FromSeconds(3))
                        {
                            // No output for 3 seconds after response started, consider it complete
                            break;
                        }
                    }
                }
                catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
                {
                    // Timeout occurred, finish streaming what we have
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
            catch (Exception ex)
            {
                // Log the error
                Debug.WriteLine($"Error in streaming task: {ex}");
            }
            finally
            {
                tokenChannel.Writer.Complete();
                _sendSemaphore.Release();
            }
        });
        
        // Yield tokens from the token channel
        await foreach (var tokenValue in tokenChannel.Reader.ReadAllAsync(token))
        {
            yield return tokenValue;
        }
    }
    
    /// <summary>
    /// Stops the Python process
    /// </summary>
    /// <returns>A task that completes when the process is stopped</returns>
    public async Task StopAsync()
    {
        if (_process == null || _process.HasExited)
        {
            return;
        }
        
        try
        {
            // Send exit command to gracefully terminate the Python process
            if (_inputWriter != null)
            {
                try
                {
                    await _inputWriter.WriteLineAsync("exit");
                    await _inputWriter.FlushAsync();
                }
                catch (IOException)
                {
                    // The stream may be closed already
                }
            }
            
            // Give the process some time to exit gracefully
            bool exited = _process.WaitForExit(3000);
            
            if (!exited)
            {
                // If the process hasn't exited, kill it
                _process.Kill(true);
            }
        }
        catch (Exception ex)
        {
            // If we encounter an error, try to force kill the process
            try
            {
                if (!_process.HasExited)
                {
                    _process.Kill(true);
                }
            }
            catch
            {
                // Ignore any errors when trying to kill the process
            }
            
            throw new InvalidOperationException($"Failed to stop Python process: {ex.Message}", ex);
        }
        finally
        {
            _cancellationTokenSource.Cancel();
            _process.Dispose();
            _process = null;
            _inputWriter = null;
            _isInitialized = false;
            _isReadyForCommands = false;
        }
    }
    
    /// <summary>
    /// Disposes the resources used by the Python process manager
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
    /// <summary>
    /// Disposes the resources used by the Python process manager
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
            try
            {
                if (_process != null)
                {
                    if (!_process.HasExited)
                    {
                        try
                        {
                            // Try to stop the process gracefully first
                            StopAsync().GetAwaiter().GetResult();
                        }
                        catch
                        {
                            // If that fails, force kill it
                            try
                            {
                                _process.Kill(true);
                            }
                            catch
                            {
                                // Ignore any errors when trying to kill the process
                            }
                        }
                    }
                    
                    // Process might be null here if StopAsync was successful and set it to null
                    if (_process != null)
                    {
                        _process.Dispose();
                        _process = null;
                    }
                }
                
                _sendSemaphore?.Dispose();
                _cancellationTokenSource?.Dispose();
            }
            catch
            {
                // Ignore any errors during disposal
            }
        }
        
        _isDisposed = true;
    }
    
    private void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (e.Data == null)
        {
            return;
        }
        
        // Trigger event for external handlers
        OutputReceived?.Invoke(this, e.Data);
        
        // Check for initialization message
        if (!_isReadyForCommands && e.Data.Contains("Entering chat mode"))
        {
            _isReadyForCommands = true;
            _startTcs.TrySetResult(true);
            return;
        }
        
        // Check if this line is a prompt that indicates response completion
        foreach (var pattern in PromptPatterns)
        {
            if (pattern.IsMatch(e.Data))
            {
                // This is a prompt line, which means the previous response is complete
                // Add it to the output channel so it can be detected as a completion signal
                _outputChannel.Writer.TryWrite(e.Data);
                return;
            }
        }
        
        // Filter out log messages and other non-response output
        // Skip lines that are likely log messages or system output
        if (e.Data.StartsWith("[System]") || 
            e.Data.Contains("INFO") || 
            e.Data.Contains("WARNING") || 
            e.Data.Contains("ERROR") || 
            e.Data.Contains("DEBUG"))
        {
            // These are log messages, don't add them to the output channel
            return;
        }
        
        // Special handling for RAG-related output
        if (e.Data.StartsWith("[RAG]"))
        {
            // For RAG commands, we want to include the output but not the log prefix
            var cleanedLine = e.Data.Replace("[RAG] ", "");
            _outputChannel.Writer.TryWrite(cleanedLine);
            return;
        }
        
        // Special handling for ANSI color codes
        if (e.Data.Contains("\u001b["))
        {
            // Remove ANSI color codes but keep the content
            var cleanedLine = System.Text.RegularExpressions.Regex.Replace(
                e.Data,
                @"\u001b\[\d*(;\d*)*m",
                string.Empty);
            _outputChannel.Writer.TryWrite(cleanedLine);
            return;
        }
        
        // Add to the output channel for command responses
        _outputChannel.Writer.TryWrite(e.Data);
    }
    
    private void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (e.Data == null)
        {
            return;
        }
        
        // Trigger event for external handlers
        ErrorReceived?.Invoke(this, e.Data);
        
        // If we get an error during initialization, signal failure
        if (!_isReadyForCommands && e.Data.Contains("Error"))
        {
            _startTcs.TrySetException(new InvalidOperationException($"Python process initialization failed: {e.Data}"));
            return;
        }
    }
    
    private async Task WaitForInitializationAsync()
    {
        // Wait for the startup message with a timeout
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        
        try
        {
            await Task.WhenAny(_startTcs.Task, Task.Delay(Timeout.Infinite, cts.Token));
            
            if (!_startTcs.Task.IsCompleted)
            {
                throw new TimeoutException("Timed out waiting for Python process to initialize.");
            }
            
            await _startTcs.Task; // Propagate any exceptions
        }
        catch (TaskCanceledException)
        {
            throw new TimeoutException("Timed out waiting for Python process to initialize.");
        }
    }
} 