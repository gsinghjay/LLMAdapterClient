using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LLMAdapterClient.ChatClient.Services;
using LLMAdapterClient.Common;
using Moq;
using Xunit;

namespace LLMAdapterClient.ChatClient.Tests;

/// <summary>
/// Tests for the PythonProcessManager class
/// </summary>
public class PythonProcessManagerTests
{
    // Path to a simple Python test script that simulates the behavior of main.py
    private const string TestPythonScript = "test_python_script.py";
    
    /// <summary>
    /// Creates a simple Python test script for testing
    /// </summary>
    /// <returns>The path to the created test script</returns>
    private string CreateTestPythonScript()
    {
        var scriptPath = Path.Combine(Directory.GetCurrentDirectory(), TestPythonScript);
        
        var scriptContent = @"
import sys
import time

def main():
    print('Entering chat mode. Type \'exit\' to quit; \'/help\' for commands.')
    sys.stdout.flush()
    
    while True:
        line = sys.stdin.readline().strip()
        
        if line.lower() == 'exit':
            break
            
        if line.startswith('/'):
            # Handle special commands
            if line == '/help':
                print('Available commands:')
                print('  /clear      - Clear the chat history')
                print('  /help       - Show this help message')
                print('  exit        - Exit chat mode')
                sys.stdout.flush()
            elif line == '/clear':
                print('Chat history cleared.')
                sys.stdout.flush()
            else:
                print(f'Unknown command: {line}')
                sys.stdout.flush()
        else:
            # Simulate streaming response
            response = f'You said: {line}'
            for char in response:
                print(char, end='')
                sys.stdout.flush()
                time.sleep(0.01)
            print()
            sys.stdout.flush()
            print('COMMAND_COMPLETE')
            sys.stdout.flush()
    
    print('Exiting chat mode...')
    sys.stdout.flush()

if __name__ == '__main__':
    # Check if --mode chat is present in args
    if '--mode' in sys.argv and 'chat' in sys.argv:
        main()
    else:
        print('Error: Script must be run with --mode chat')
        sys.stdout.flush()
        sys.exit(1)
";
        
        File.WriteAllText(scriptPath, scriptContent);
        return scriptPath;
    }
    
    /// <summary>
    /// Creates a test Python script that simulates responses without completion markers
    /// </summary>
    /// <returns>The path to the created test script</returns>
    private string CreateTestPythonScriptWithoutMarkers()
    {
        var scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "test_python_script_no_markers.py");
        
        var scriptContent = @"
import sys
import time

def main():
    print('Entering chat mode. Type \'exit\' to quit; \'/help\' for commands.')
    sys.stdout.flush()
    
    while True:
        line = sys.stdin.readline().strip()
        
        if line.lower() == 'exit':
            break
            
        if line.startswith('/'):
            # Handle special commands
            if line == '/help':
                print('Available commands:')
                print('  /clear      - Clear the chat history')
                print('  /help       - Show this help message')
                print('  exit        - Exit chat mode')
                print('You: ')
                sys.stdout.flush()
            elif line == '/clear':
                print('Chat history cleared.')
                print('You: ')
                sys.stdout.flush()
            else:
                print(f'Unknown command: {line}')
                print('You: ')
                sys.stdout.flush()
        elif line == 'prompt_with_newlines':
            # Test scenario with multiple newlines before prompt
            print('This is a response')
            print('with multiple lines')
            print('')
            print('')
            print('You: ')
            sys.stdout.flush()
        elif line == 'no_prompt_response':
            # Test scenario where no prompt appears, should timeout
            print('This response does not have a prompt afterwards')
            sys.stdout.flush()
        elif line == 'delayed_prompt':
            # Test scenario with a delayed prompt
            print('This is a response with a delayed prompt')
            sys.stdout.flush()
            time.sleep(2)
            print('You: ')
            sys.stdout.flush()
        else:
            # Simulate normal response with prompt at end
            response = f'You said: {line}'
            for char in response:
                print(char, end='')
                sys.stdout.flush()
                time.sleep(0.01)
            print()
            print('You: ')
            sys.stdout.flush()
    
    print('Exiting chat mode...')
    sys.stdout.flush()

if __name__ == '__main__':
    # Check if --mode chat is present in args
    if '--mode' in sys.argv and 'chat' in sys.argv:
        main()
    else:
        print('Error: Script must be run with --mode chat')
        sys.stdout.flush()
        sys.exit(1)
";
        
        File.WriteAllText(scriptPath, scriptContent);
        return scriptPath;
    }
    
    /// <summary>
    /// Test method for starting the Python process
    /// </summary>
    /// <returns>A task representing the asynchronous test operation</returns>
    [Fact]
    public async Task StartAsync_WithValidPaths_ShouldStartProcess()
    {
        // Arrange
        var scriptPath = CreateTestPythonScript();
        var pythonPath = "/home/jay/.pyenv/shims/python"; // Using full path to Python
        
        using var processManager = new PythonProcessManager();
        
        // Store output messages
        var outputMessages = new List<string>();
        processManager.OutputReceived += (sender, message) => outputMessages.Add(message);
        
        try
        {
            // Act
            await processManager.StartAsync(pythonPath, scriptPath, new[] { "--mode", "chat" });
            
            // Assert
            Assert.True(processManager.IsRunning);
            Assert.Contains(outputMessages, m => m.Contains("Entering chat mode"));
            
            // Cleanup
            await processManager.StopAsync();
        }
        catch (FileNotFoundException)
        {
            // Skip test if python is not found in PATH
            return;
        }
    }
    
    /// <summary>
    /// Test method for sending a command and getting a response
    /// </summary>
    /// <returns>A task representing the asynchronous test operation</returns>
    [Fact]
    public async Task SendCommandAsync_WithValidCommand_ShouldReturnResponse()
    {
        // Arrange
        var scriptPath = CreateTestPythonScript();
        var pythonPath = "/home/jay/.pyenv/shims/python"; // Using full path to Python
        
        using var processManager = new PythonProcessManager();
        
        try
        {
            await processManager.StartAsync(pythonPath, scriptPath, new[] { "--mode", "chat" });
            
            // Act
            var response = await processManager.SendCommandAsync("Hello, Python!");
            
            // Assert
            Assert.Contains("You said: Hello, Python!", response);
            
            // Cleanup
            await processManager.StopAsync();
        }
        catch (FileNotFoundException)
        {
            // Skip test if python is not found in PATH
            return;
        }
    }
    
    /// <summary>
    /// Test method for sending a command and getting a streaming response
    /// </summary>
    /// <returns>A task representing the asynchronous test operation</returns>
    [Fact]
    public async Task SendCommandStreamingAsync_WithValidCommand_ShouldStreamResponse()
    {
        // Arrange
        var scriptPath = CreateTestPythonScript();
        var pythonPath = "/home/jay/.pyenv/shims/python"; // Using full path to Python
        
        using var processManager = new PythonProcessManager();
        
        try
        {
            await processManager.StartAsync(pythonPath, scriptPath, new[] { "--mode", "chat" });
            
            // Act
            var tokens = new List<string>();
            await foreach (var token in processManager.SendCommandStreamingAsync("Hello, Streaming!"))
            {
                tokens.Add(token);
            }
            
            // Assert
            var fullResponse = string.Join("", tokens);
            Assert.Contains("You said: Hello, Streaming!", fullResponse);
            
            // Cleanup
            await processManager.StopAsync();
        }
        catch (FileNotFoundException)
        {
            // Skip test if python is not found in PATH
            return;
        }
    }
    
    /// <summary>
    /// Test method for executing a special command
    /// </summary>
    /// <returns>A task representing the asynchronous test operation</returns>
    [Fact]
    public async Task SendCommandAsync_WithSpecialCommand_ShouldHandleSpecialCommand()
    {
        // Arrange
        var scriptPath = CreateTestPythonScript();
        var pythonPath = "/home/jay/.pyenv/shims/python"; // Using full path to Python
        
        using var processManager = new PythonProcessManager();
        
        try
        {
            await processManager.StartAsync(pythonPath, scriptPath, new[] { "--mode", "chat" });
            
            // Act
            var response = await processManager.SendCommandAsync("/help");
            
            // Assert
            Assert.Contains("Available commands:", response);
            
            // Cleanup
            await processManager.StopAsync();
        }
        catch (FileNotFoundException)
        {
            // Skip test if python is not found in PATH
            return;
        }
    }
    
    /// <summary>
    /// Test method for stopping the Python process
    /// </summary>
    /// <returns>A task representing the asynchronous test operation</returns>
    [Fact]
    public async Task StopAsync_WhenProcessIsRunning_ShouldStopProcess()
    {
        // Arrange
        var scriptPath = CreateTestPythonScript();
        var pythonPath = "/home/jay/.pyenv/shims/python"; // Using full path to Python
        
        using var processManager = new PythonProcessManager();
        
        try
        {
            await processManager.StartAsync(pythonPath, scriptPath, new[] { "--mode", "chat" });
            Assert.True(processManager.IsRunning);
            
            // Act
            await processManager.StopAsync();
            
            // Assert
            Assert.False(processManager.IsRunning);
        }
        catch (FileNotFoundException)
        {
            // Skip test if python is not found in PATH
            return;
        }
    }
    
    /// <summary>
    /// Test method for detecting completion based on prompt reappearance
    /// </summary>
    /// <returns>A task representing the asynchronous test operation</returns>
    [Fact]
    public async Task SendCommandAsync_WithPromptReappearance_ShouldDetectCompletion()
    {
        // Arrange
        var scriptPath = CreateTestPythonScriptWithoutMarkers();
        var pythonPath = "/home/jay/.pyenv/shims/python"; // Using full path to Python
        
        using var processManager = new PythonProcessManager();
        
        // Store output messages
        var outputMessages = new List<string>();
        processManager.OutputReceived += (sender, message) => outputMessages.Add(message);
        
        await processManager.StartAsync(pythonPath, scriptPath, new[] { "--mode", "chat" });
        
        // Act
        var response = await processManager.SendCommandAsync("hello");
        
        // Assert
        Assert.NotNull(response);
        Assert.Contains("You said: hello", response);
        Assert.DoesNotContain("You: ", response); // Prompt should not be included in response
    }
    
    /// <summary>
    /// Test method for handling responses with multiple newlines before prompt
    /// </summary>
    /// <returns>A task representing the asynchronous test operation</returns>
    [Fact]
    public async Task SendCommandAsync_WithMultipleNewlinesBeforePrompt_ShouldDetectCompletion()
    {
        // Arrange
        var scriptPath = CreateTestPythonScriptWithoutMarkers();
        var pythonPath = "/home/jay/.pyenv/shims/python"; // Using full path to Python
        
        using var processManager = new PythonProcessManager();
        
        // Store output messages
        var outputMessages = new List<string>();
        processManager.OutputReceived += (sender, message) => outputMessages.Add(message);
        
        await processManager.StartAsync(pythonPath, scriptPath, new[] { "--mode", "chat" });
        
        // Act
        var response = await processManager.SendCommandAsync("prompt_with_newlines");
        
        // Assert
        Assert.NotNull(response);
        Assert.Contains("This is a response", response);
        Assert.Contains("with multiple lines", response);
        Assert.DoesNotContain("You: ", response); // Prompt should not be included in response
    }
    
    /// <summary>
    /// Test method for handling responses with delayed prompt appearance
    /// </summary>
    /// <returns>A task representing the asynchronous test operation</returns>
    [Fact]
    public async Task SendCommandAsync_WithDelayedPrompt_ShouldDetectCompletion()
    {
        // Arrange
        var scriptPath = CreateTestPythonScriptWithoutMarkers();
        var pythonPath = "/home/jay/.pyenv/shims/python"; // Using full path to Python
        
        using var processManager = new PythonProcessManager();
        
        // Store output messages
        var outputMessages = new List<string>();
        processManager.OutputReceived += (sender, message) => outputMessages.Add(message);
        
        await processManager.StartAsync(pythonPath, scriptPath, new[] { "--mode", "chat" });
        
        // Act
        var response = await processManager.SendCommandAsync("delayed_prompt");
        
        // Assert
        Assert.NotNull(response);
        Assert.Contains("This is a response with a delayed prompt", response);
        Assert.DoesNotContain("You: ", response); // Prompt should not be included in response
    }
    
    /// <summary>
    /// Test method for handling timeout when no prompt appears
    /// </summary>
    /// <returns>A task representing the asynchronous test operation</returns>
    [Fact]
    public async Task SendCommandAsync_WithNoPromptResponse_ShouldTimeoutAndReturnPartialResponse()
    {
        // Arrange
        var scriptPath = CreateTestPythonScriptWithoutMarkers();
        var pythonPath = "/home/jay/.pyenv/shims/python"; // Using full path to Python
        
        using var processManager = new PythonProcessManager(responseTimeoutSeconds: 3); // Short timeout for testing
        
        // Store output messages
        var outputMessages = new List<string>();
        processManager.OutputReceived += (sender, message) => outputMessages.Add(message);
        
        await processManager.StartAsync(pythonPath, scriptPath, new[] { "--mode", "chat" });
        
        // Act
        var response = await processManager.SendCommandAsync("no_prompt_response");
        
        // Assert
        Assert.NotNull(response);
        Assert.Contains("This response does not have a prompt afterwards", response);
    }
    
    /// <summary>
    /// Test method for streaming responses without completion markers
    /// </summary>
    /// <returns>A task representing the asynchronous test operation</returns>
    [Fact]
    public async Task SendCommandStreamingAsync_WithoutCompletionMarkers_ShouldStreamResponse()
    {
        // Arrange
        var scriptPath = CreateTestPythonScriptWithoutMarkers();
        var pythonPath = "/home/jay/.pyenv/shims/python"; // Using full path to Python
        
        using var processManager = new PythonProcessManager();
        
        // Store output messages
        var outputMessages = new List<string>();
        processManager.OutputReceived += (sender, message) => outputMessages.Add(message);
        
        await processManager.StartAsync(pythonPath, scriptPath, new[] { "--mode", "chat" });
        
        // Act
        var tokens = new List<string>();
        await foreach (var token in processManager.SendCommandStreamingAsync("hello"))
        {
            tokens.Add(token);
        }
        
        // Assert
        Assert.NotEmpty(tokens);
        var fullResponse = string.Join("", tokens);
        Assert.Contains("You said: hello", fullResponse);
        Assert.DoesNotContain("You: ", fullResponse); // Prompt should not be included in response
    }
} 