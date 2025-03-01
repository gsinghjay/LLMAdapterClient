using System;
using System.Threading;
using System.Threading.Tasks;
using LLMAdapterClient.Common;

namespace LLMAdapterClient.ChatClient.Services;

/// <summary>
/// Manages a chat session with an LLM model
/// </summary>
public class ChatSession : IDisposable
{
    private readonly IModelService _modelService;
    private readonly IAdapterManager _adapterManager;
    private readonly IChatUI _chatUI;
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;
    private bool _isRunning;
    
    /// <summary>
    /// Initializes a new instance of the ChatSession class
    /// </summary>
    /// <param name="modelService">The model service to use</param>
    /// <param name="adapterManager">The adapter manager to use</param>
    /// <param name="chatUI">The chat UI to use</param>
    public ChatSession(IModelService modelService, IAdapterManager adapterManager, IChatUI chatUI)
    {
        _modelService = modelService ?? throw new ArgumentNullException(nameof(modelService));
        _adapterManager = adapterManager ?? throw new ArgumentNullException(nameof(adapterManager));
        _chatUI = chatUI ?? throw new ArgumentNullException(nameof(chatUI));
        
        // Subscribe to model service events
        _modelService.MessageReceived += (sender, message) => _chatUI.DisplaySystemMessage(message);
        _modelService.ErrorReceived += (sender, error) => _chatUI.DisplayError(error);
        
        // Subscribe to adapter manager events
        _adapterManager.NewAdapterAnnounced += OnNewAdapterAnnounced;
    }
    
    /// <summary>
    /// Starts the chat session
    /// </summary>
    /// <param name="publisher">The adapter publisher to use</param>
    /// <returns>A task that completes when the session ends</returns>
    public async Task StartAsync(IAdapterPublisher publisher)
    {
        if (_isRunning)
        {
            _chatUI.DisplayError("Chat session is already running.");
            return;
        }
        
        _isRunning = true;
        
        try
        {
            // Load the latest adapter
            _chatUI.DisplaySystemMessage("Loading the latest adapter...");
            var latestAdapter = await _adapterManager.GetLatestAdapterAsync(publisher);
            
            // Initialize the model with the adapter
            _chatUI.DisplaySystemMessage($"Initializing model with adapter '{latestAdapter.Name}'...");
            await _adapterManager.InitializeModelWithAdapterAsync(_modelService, latestAdapter);
            
            // Start monitoring for new adapters
            _ = _adapterManager.MonitorForNewAdaptersAsync(publisher, _cts.Token);
            
            // Display welcome message
            _chatUI.DisplaySystemMessage("Welcome to the LLM Chat! Type /help for available commands.");
            
            // Main chat loop
            while (!_cts.Token.IsCancellationRequested)
            {
                var userInput = await _chatUI.GetUserInputAsync();
                
                if (string.IsNullOrEmpty(userInput))
                {
                    continue;
                }
                
                if (userInput.StartsWith('/'))
                {
                    await HandleSpecialCommandAsync(userInput);
                    continue;
                }
                
                await HandleUserMessageAsync(userInput);
            }
        }
        catch (OperationCanceledException)
        {
            _chatUI.DisplaySystemMessage("Chat session was canceled.");
        }
        catch (Exception ex)
        {
            _chatUI.DisplayError($"An error occurred in the chat session: {ex.Message}");
        }
        finally
        {
            _isRunning = false;
        }
    }
    
    /// <summary>
    /// Stops the chat session
    /// </summary>
    public void Stop()
    {
        if (!_isRunning)
        {
            return;
        }
        
        _cts.Cancel();
    }
    
    /// <summary>
    /// Handles a user message
    /// </summary>
    /// <param name="message">The user's message</param>
    private async Task HandleUserMessageAsync(string message)
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, timeoutCts.Token);
        
        try
        {
            // Display user message first
            _chatUI.DisplayMessage("user", message);
            
            // Display thinking message
            _chatUI.DisplaySystemMessage("Thinking...");
            
            // Use streaming response for more interactive experience
            var responseStream = _modelService.GenerateStreamingResponseAsync(message, combinedCts.Token);
            
            // Display the response with timeout
            await _chatUI.DisplayStreamingMessageAsync("assistant", responseStream)
                .WaitAsync(TimeSpan.FromMinutes(2), combinedCts.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            _chatUI.DisplayError("Response generation timed out.");
            // Try to recover the session
            await _modelService.ExecuteSpecialCommandAsync("/clear");
        }
        catch (OperationCanceledException)
        {
            _chatUI.DisplaySystemMessage("Response generation was canceled.");
        }
        catch (Exception ex)
        {
            _chatUI.DisplayError($"Error generating response: {ex.Message}");
            // Try to recover the session
            await _modelService.ExecuteSpecialCommandAsync("/clear");
        }
    }
    
    /// <summary>
    /// Handles a special command
    /// </summary>
    /// <param name="command">The command to handle</param>
    private async Task HandleSpecialCommandAsync(string command)
    {
        // Normalize and parse the command
        var normalizedCommand = command.ToLower().Trim();
        
        switch (normalizedCommand)
        {
            case "/help":
                DisplayHelpMessage();
                break;
                
            case "/exit":
            case "/quit":
                _chatUI.DisplaySystemMessage("Exiting chat session...");
                Stop();
                break;
                
            case "/clear":
                Console.Clear();
                _chatUI.DisplaySystemMessage("Console cleared.");
                await _modelService.ExecuteSpecialCommandAsync("/clear");
                break;
                
            default:
                // Pass unknown commands to the model service
                if (_modelService.IsInitialized)
                {
                    try
                    {
                        await _modelService.ExecuteSpecialCommandAsync(command);
                    }
                    catch (Exception ex)
                    {
                        _chatUI.DisplayError($"Error executing command: {ex.Message}");
                    }
                }
                else
                {
                    _chatUI.DisplayError($"Unknown command: {command}");
                    DisplayHelpMessage();
                }
                break;
        }
    }
    
    /// <summary>
    /// Displays the help message
    /// </summary>
    private void DisplayHelpMessage()
    {
        _chatUI.DisplaySystemMessage("Available commands:");
        _chatUI.DisplaySystemMessage("  /help   - Display this help message");
        _chatUI.DisplaySystemMessage("  /exit   - Exit the chat session");
        _chatUI.DisplaySystemMessage("  /quit   - Exit the chat session");
        _chatUI.DisplaySystemMessage("  /clear  - Clear the console and chat history");
        
        // Add model-specific commands if the model is initialized
        if (_modelService.IsInitialized)
        {
            _chatUI.DisplaySystemMessage("Model-specific commands:");
            _chatUI.DisplaySystemMessage("  /loadrag <path>  - Load RAG from the specified path");
            _chatUI.DisplaySystemMessage("  /ragstatus       - Show the status of the RAG system");
        }
    }
    
    /// <summary>
    /// Handles the NewAdapterAnnounced event
    /// </summary>
    private void OnNewAdapterAnnounced(object? sender, AdapterEventArgs e)
    {
        _chatUI.AnnounceNewAdapter(e.AdapterInfo);
        
        if (_modelService.IsInitialized)
        {
            _chatUI.DisplaySystemMessage("Type /reload to reload with the new adapter.");
        }
    }
    
    /// <summary>
    /// Disposes the chat session resources
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
    /// <summary>
    /// Disposes the chat session resources
    /// </summary>
    /// <param name="disposing">Whether this is being called from Dispose() or a finalizer</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }
        
        if (disposing)
        {
            // Unsubscribe from events
            _adapterManager.NewAdapterAnnounced -= OnNewAdapterAnnounced;
            
            // Stop the session
            Stop();
            
            // Dispose cancellation token source
            _cts.Dispose();
        }
        
        _disposed = true;
    }
} 