using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LLMAdapterClient.Common;

namespace LLMAdapterClient.ChatClient.Services;

/// <summary>
/// Console-based implementation of the IChatUI interface
/// </summary>
public class ConsoleChatUI : IChatUI
{
    private const string UserPrompt = "> ";
    
    private readonly ConsoleColor _userColor = ConsoleColor.Green;
    private readonly ConsoleColor _assistantColor = ConsoleColor.Cyan;
    private readonly ConsoleColor _systemColor = ConsoleColor.Yellow;
    private readonly ConsoleColor _errorColor = ConsoleColor.Red;
    private readonly ConsoleColor _defaultColor;
    
    /// <summary>
    /// Initializes a new instance of the ConsoleChatUI class
    /// </summary>
    public ConsoleChatUI()
    {
        _defaultColor = Console.ForegroundColor;
    }
    
    /// <summary>
    /// Displays a message from a specific role
    /// </summary>
    /// <param name="role">The role (e.g., "user", "system", "assistant")</param>
    /// <param name="message">The message content</param>
    public void DisplayMessage(string role, string message)
    {
        Console.ForegroundColor = GetColorForRole(role);
        Console.WriteLine($"{FormatRole(role)}{message}");
        Console.ForegroundColor = _defaultColor;
    }
    
    /// <summary>
    /// Displays a streaming message token by token
    /// </summary>
    /// <param name="role">The role (e.g., "user", "system", "assistant")</param>
    /// <param name="messageTokens">An async enumerable of message tokens</param>
    /// <returns>A task that completes when all tokens have been displayed</returns>
    public async Task DisplayStreamingMessageAsync(string role, IAsyncEnumerable<string> messageTokens)
    {
        Console.ForegroundColor = GetColorForRole(role);
        Console.Write(FormatRole(role));
        
        var isFirstToken = true;
        await foreach (var token in messageTokens)
        {
            if (isFirstToken)
            {
                // Skip any leading whitespace on first token
                Console.Write(token.TrimStart());
                isFirstToken = false;
            }
            else
            {
                Console.Write(token);
            }
        }
        
        // Ensure we end with a newline and reset colors
        Console.WriteLine();
        Console.ForegroundColor = _defaultColor;
        
        // If this was an assistant response, add an extra newline for readability
        // and write the user prompt for the next input
        if (role.Equals("assistant", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine();
            Console.ForegroundColor = _userColor;
            Console.Write(UserPrompt);
            Console.ForegroundColor = _defaultColor;
        }
    }
    
    /// <summary>
    /// Announces a new adapter
    /// </summary>
    /// <param name="adapter">The adapter information</param>
    public void AnnounceNewAdapter(IAdapterInfo adapter)
    {
        Console.ForegroundColor = _systemColor;
        Console.WriteLine($"[System] New adapter available: {adapter.Name}");
        Console.WriteLine($"[System] Created: {adapter.Created}");
        Console.WriteLine($"[System] Path: {adapter.FilePath}");
        Console.WriteLine("[System] Metadata:");
        
        foreach (var entry in adapter.Metadata)
        {
            Console.WriteLine($"[System]   {entry.Key}: {entry.Value}");
        }
        
        Console.ForegroundColor = _defaultColor;
    }
    
    /// <summary>
    /// Gets user input asynchronously
    /// </summary>
    /// <returns>A task that resolves to the user's input</returns>
    public Task<string> GetUserInputAsync()
    {
        Console.ForegroundColor = _userColor;
        Console.Write(UserPrompt);
        Console.ForegroundColor = _defaultColor;
        
        return Task.FromResult(Console.ReadLine() ?? string.Empty);
    }
    
    /// <summary>
    /// Displays an error message
    /// </summary>
    /// <param name="message">The error message</param>
    public void DisplayError(string message)
    {
        var previousColor = Console.ForegroundColor;
        Console.ForegroundColor = _errorColor;
        Console.WriteLine($"[Error] {message}");
        Console.ForegroundColor = previousColor;
    }
    
    /// <summary>
    /// Displays a system message
    /// </summary>
    /// <param name="message">The system message</param>
    public void DisplaySystemMessage(string message)
    {
        var previousColor = Console.ForegroundColor;
        Console.ForegroundColor = _systemColor;
        Console.WriteLine($"[System] {message}");
        Console.ForegroundColor = previousColor;
    }
    
    private ConsoleColor GetColorForRole(string role)
    {
        return role.ToLower() switch
        {
            "user" => _userColor,
            "assistant" => _assistantColor,
            "system" => _systemColor,
            "error" => _errorColor,
            _ => _defaultColor
        };
    }
    
    private string FormatRole(string role)
    {
        return role.ToLower() switch
        {
            "user" => $"{UserPrompt}",
            "assistant" => "[Assistant] ",
            "system" => "[System] ",
            "error" => "[Error] ",
            _ => $"[{role}] "
        };
    }
} 