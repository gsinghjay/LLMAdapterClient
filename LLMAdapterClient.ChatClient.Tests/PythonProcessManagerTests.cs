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
    /// Test method for starting the Python process
    /// </summary>
    /// <returns>A task representing the asynchronous test operation</returns>
    [Fact]
    public async Task StartAsync_WithValidPaths_ShouldStartProcess()
    {
        // Arrange
        var scriptPath = CreateTestPythonScript();
        var pythonPath = "python"; // Assuming Python is in PATH
        
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
        var pythonPath = "python"; // Assuming Python is in PATH
        
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
        var pythonPath = "python"; // Assuming Python is in PATH
        
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
        var pythonPath = "python"; // Assuming Python is in PATH
        
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
        var pythonPath = "python"; // Assuming Python is in PATH
        
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
} 