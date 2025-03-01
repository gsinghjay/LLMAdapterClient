# LLM Adapter Client

A .NET solution that integrates with a Python training system to distribute and use LoRA adapters for Large Language Models.

## Project Overview

The LLM Adapter Client provides a bridge between Python-based LoRA adapter training and .NET applications. It enables automatic distribution and usage of trained adapters in a chat interface.

### Key Features

- **Automatic Adapter Detection**: Monitors for newly trained adapters from the Python system
- **Adapter Distribution**: Publishes adapters to a shared location for client applications
- **Chat Interface**: Provides an interactive console for chatting with adapter-enhanced LLMs
- **Real-time Updates**: Automatically detects and applies new adapters during chat sessions
- **Robust Python Integration**: Manages Python processes for stable communication with LLM models
- **Streaming Responses**: Provides token-by-token streaming for responsive chat experience
- **Command System**: Supports special commands for chat management and RAG capabilities
- **Event-Based Architecture**: Uses events for adapter announcements and model messages

## Project Structure

```
LLMAdapterClient/
├── LLMAdapterClient.Common/           # Core interfaces and models
│   └── Interfaces.cs                  # Contains IAdapterInfo, IAdapterPublisher, and IPythonProcessManager
├── LLMAdapterClient.Publisher/        # Adapter publishing application
│   ├── Program.cs                     # Publisher entry point
│   └── Services/                      # Publisher services
│       ├── AdapterSelector.cs         # Discovers adapter directories
│       ├── AdapterValidator.cs        # Validates adapter file structure
│       └── AdapterInfoExtractor.cs    # Extracts adapter metadata
├── LLMAdapterClient.ChatClient/       # Chat interface application
│   ├── Program.cs                     # ChatClient entry point
│   └── Services/                      # ChatClient services
│       ├── PythonProcessManager.cs    # Manages Python process communication
│       ├── ModelService.cs            # Interfaces with the LLM model
│       ├── AdapterManager.cs          # Manages adapter loading and monitoring
│       ├── ConsoleChatUI.cs           # Provides console UI for chat interactions
│       └── ChatSession.cs             # Manages conversation flow and commands
├── LLMAdapterClient.Common.Tests/     # Tests for common interfaces
│   └── InterfaceTests.cs              # Tests for IAdapterInfo and IAdapterPublisher
├── LLMAdapterClient.Publisher.Tests/  # Tests for publisher services
│   └── AdapterTests.cs                # Tests for adapter services
├── LLMAdapterClient.ChatClient.Tests/ # Tests for ChatClient services
│   ├── PythonProcessManagerTests.cs   # Tests for Python process management
│   ├── ModelServiceTests.cs           # Tests for model service functionality
│   ├── AdapterManagerTests.cs         # Tests for adapter manager
│   ├── ChatUITests.cs                 # Tests for chat UI components
│   └── ChatSessionTests.cs            # Tests for chat session management
├── llm_training-main/                 # Python training system
│   ├── main.py                        # Training script
│   ├── config.yaml                    # Training configuration
│   └── checkpoints/                   # Generated adapter files
│       ├── best_model_adapter/        # Best performing adapter
│       └── checkpoint_epoch_*_adapter/# Checkpoint adapters
├── IMPLEMENTATION.md                  # TDD implementation plan
├── STORY.md                          # Project narrative
├── global.json                        # .NET SDK version configuration
└── README.md                          # This file
```

## Solution Structure
The solution consists of three main projects:

- **LLMAdapterClient.Common**: Core interfaces and models shared across projects
- **LLMAdapterClient.Publisher**: Console application that monitors and distributes adapters
  - **Services**: Core adapter management services
    - `AdapterSelector`: Discovers and selects valid adapter directories
    - `AdapterValidator`: Validates adapter file structure and integrity
    - `AdapterInfoExtractor`: Extracts metadata from adapter configuration files
- **LLMAdapterClient.ChatClient**: Console application for interacting with enhanced LLMs
  - **Services**: Core chat and model integration services
    - `PythonProcessManager`: Manages Python process lifecycle for LLM communication
      - Starts Python processes with appropriate arguments
      - Handles command sending and response parsing
      - Provides streaming token-by-token response capabilities
      - Ensures graceful process termination
    - `PythonModelService`: Manages model service with adapter support
      - Initializes model with appropriate adapter
      - Provides complete and streaming response generation
      - Handles special commands
      - Manages resources properly
    - `AdapterManager`: Manages adapter loading and monitoring
      - Loads adapters from shared storage
      - Monitors for new adapters with proper event notifications
      - Initializes model service with adapters
    - `ConsoleChatUI`: Provides user interface for chat interactions
      - Displays messages with role-based coloring
      - Streams token-by-token responses
      - Handles user input with proper prompting
      - Displays system messages and errors
    - `ChatSession`: Manages the conversation flow
      - Coordinates between model, adapter manager, and UI
      - Handles user messages and special commands
      - Provides timeout protection for model responses
      - Ensures proper resource cleanup

### Core Interfaces

The Common library defines these key interfaces:

```csharp
public interface IAdapterInfo
{
    string Name { get; }
    string FilePath { get; }
    DateTime Created { get; }
    Dictionary<string, object> Metadata { get; }
}

public interface IAdapterPublisher
{
    event EventHandler<AdapterEventArgs> AdapterPublished;
    IReadOnlyList<IAdapterInfo> GetAvailableAdapters();
    Task<IAdapterInfo> GetLatestAdapterAsync();
}

public interface IPythonProcessManager
{
    event EventHandler<string> OutputReceived;
    event EventHandler<string> ErrorReceived;
    Task StartAsync(string pythonPath, string scriptPath, string[] args);
    Task<string> SendCommandAsync(string command, CancellationToken token = default);
    IAsyncEnumerable<string> SendCommandStreamingAsync(string command, CancellationToken token = default);
    Task StopAsync();
    bool IsRunning { get; }
}

public interface IModelService
{
    event EventHandler<string> MessageReceived;
    event EventHandler<string> ErrorReceived;
    bool IsInitialized { get; }
    IAdapterInfo? CurrentAdapter { get; }
    
    Task InitializeAsync(IAdapterInfo adapter, string? configPath = null, bool skipValidation = false);
    Task<string> GenerateResponseAsync(string prompt, CancellationToken token = default);
    IAsyncEnumerable<string> GenerateStreamingResponseAsync(string prompt, CancellationToken token = default);
    Task ExecuteSpecialCommandAsync(string command);
    Task ShutdownAsync();
}

public interface IAdapterManager
{
    event EventHandler<AdapterEventArgs> NewAdapterAnnounced;
    Task<IAdapterInfo> LoadAdapterAsync(string adapterPath);
    Task<IAdapterInfo> GetLatestAdapterAsync(IAdapterPublisher publisher);
    Task InitializeModelWithAdapterAsync(IModelService modelService, IAdapterInfo adapter);
    Task MonitorForNewAdaptersAsync(IAdapterPublisher publisher, CancellationToken token = default);
}

public interface IChatUI
{
    void DisplayMessage(string role, string message);
    Task DisplayStreamingMessageAsync(string role, IAsyncEnumerable<string> messageTokens);
    void AnnounceNewAdapter(IAdapterInfo adapter);
    Task<string> GetUserInputAsync();
    void DisplayError(string message);
    void DisplaySystemMessage(string message);
}
```

### Publisher Services

The Publisher project implements these key services:

```csharp
public class AdapterSelector
{
    public IEnumerable<string> GetAvailableAdapterDirectories();
}

public class AdapterValidator
{
    public bool ValidateAdapter(string adapterPath);
}

public class AdapterInfoExtractor
{
    public Task<IAdapterInfo> ExtractAdapterInfoAsync(string adapterPath);
}
```

### Chat Client Services

The ChatClient project implements these key services:

```csharp
public class PythonProcessManager : IPythonProcessManager, IDisposable
{
    public event EventHandler<string> OutputReceived;
    public event EventHandler<string> ErrorReceived;
    public bool IsRunning { get; }
    
    public Task StartAsync(string pythonPath, string scriptPath, string[] args);
    public Task<string> SendCommandAsync(string command, CancellationToken token = default);
    public IAsyncEnumerable<string> SendCommandStreamingAsync(string command, CancellationToken token = default);
    public Task StopAsync();
    public void Dispose();
}

public class PythonModelService : IModelService, IDisposable
{
    public event EventHandler<string> MessageReceived;
    public event EventHandler<string> ErrorReceived;
    public bool IsInitialized { get; }
    public IAdapterInfo? CurrentAdapter { get; }
    
    public Task InitializeAsync(IAdapterInfo adapter, string? configPath = null, bool skipValidation = false);
    public Task<string> GenerateResponseAsync(string prompt, CancellationToken token = default);
    public IAsyncEnumerable<string> GenerateStreamingResponseAsync(string prompt, CancellationToken token = default);
    public Task ExecuteSpecialCommandAsync(string command);
    public Task ShutdownAsync();
    public void Dispose();
}

public class AdapterManager : IAdapterManager
{
    public event EventHandler<AdapterEventArgs> NewAdapterAnnounced;
    
    public Task<IAdapterInfo> LoadAdapterAsync(string adapterPath);
    public Task<IAdapterInfo> GetLatestAdapterAsync(IAdapterPublisher publisher);
    public Task InitializeModelWithAdapterAsync(IModelService modelService, IAdapterInfo adapter);
    public Task MonitorForNewAdaptersAsync(IAdapterPublisher publisher, CancellationToken token = default);
}

public class ConsoleChatUI : IChatUI
{
    public void DisplayMessage(string role, string message);
    public Task DisplayStreamingMessageAsync(string role, IAsyncEnumerable<string> messageTokens);
    public void AnnounceNewAdapter(IAdapterInfo adapter);
    public Task<string> GetUserInputAsync();
    public void DisplayError(string message);
    public void DisplaySystemMessage(string message);
}

public class ChatSession : IDisposable
{
    public Task StartAsync(IAdapterPublisher publisher);
    public void Stop();
    public void Dispose();
    
    // Private methods for handling messages and commands
    private Task HandleUserMessageAsync(string message);
    private Task HandleSpecialCommandAsync(string command);
}
```

## Getting Started

### Prerequisites

- .NET 9.0 SDK or later
- Python 3.8+ (for the training system)
- Git

### Installation

1. Clone the repository:
   ```bash
   git clone https://github.com/yourusername/LLMAdapterClient.git
   cd LLMAdapterClient
   ```

2. Build the solution:
   ```bash
   dotnet build
   ```

3. Set up the Python training environment (optional):
   ```bash
   cd llm_training-main
   pip install -r requirements.txt
   ```

## Usage

### Training Adapters (Python)

1. Configure the training parameters in `llm_training-main/config.yaml`
2. Run the training script:
   ```bash
   python main.py --mode train --config config.yaml
   ```
3. Adapters will be generated in the `llm_training-main/checkpoints` directory

### Publishing Adapters (.NET)

1. Run the Publisher application:
   ```bash
   dotnet run --project LLMAdapterClient.Publisher
   ```
2. The Publisher will monitor the adapter directory and distribute new adapters

### Chat Interface (.NET)

1. Run the ChatClient application:
   ```bash
   dotnet run --project LLMAdapterClient.ChatClient
   ```
2. Chat with the enhanced LLM through the console interface
3. The client will automatically update when new adapters are available
4. Use special commands to control the chat session:
   - `/help` - Display help message
   - `/clear` - Clear the chat history
   - `/exit` or `/quit` - Exit the chat session
   - `/loadrag <path>` - Load RAG from specified path
   - `/ragstatus` - Show RAG system status

## Development

### Project Architecture

```mermaid
graph TD
    A[LLMAdapterClient Solution] --> B[ChatClient]
    A --> C[Publisher]
    A --> D[Common]
    
    C --> E[AdapterSelector]
    C --> F[AdapterValidator]
    C --> G[AdapterInfoExtractor]
    
    B --> H[PythonProcessManager]
    B --> I[ModelService]
    B --> J[AdapterManager]
    B --> K[ConsoleChatUI]
    B --> L[ChatSession]
    
    B --> D
    C --> D
    E & F & G --> D
    H & I & J & K & L --> D

    P[Python Training System] -.-> C
    P -.-> H
    P -.-> I
    C -.-> B
```

### Data Flow

```mermaid
sequenceDiagram
    participant User
    participant Training as Python Training
    participant Publisher
    participant Services as Publisher Services
    participant ChatClient
    participant ChatSession
    participant ChatUI
    participant AdapterManager
    participant ModelService
    participant PythonMgr as Python Process Manager
    
    User->>Training: Train Model
    Training->>Publisher: Create Adapter File
    Publisher->>Services: Discover & Validate
    Services->>Services: Extract Metadata
    Publisher->>Publisher: Monitor & Sync
    
    User->>ChatClient: Start Chat
    ChatClient->>Publisher: Check for Updates
    Publisher-->>ChatClient: Sync New Adapters
    ChatClient->>ChatSession: Initialize Session
    ChatSession->>AdapterManager: Get Latest Adapter
    AdapterManager->>ModelService: Initialize Model
    ModelService->>PythonMgr: Initialize Python Process
    PythonMgr->>Training: Interact with Python Script
    
    User->>ChatUI: Enter Message
    ChatUI->>ChatSession: Process Message
    ChatSession->>ModelService: Generate Response
    ModelService->>PythonMgr: Send Prompt
    PythonMgr-->>ModelService: Stream Response Tokens
    ModelService-->>ChatSession: Forward Response Tokens
    ChatSession-->>ChatUI: Display Streaming Response
    ChatUI-->>User: Show Response
    
    Publisher-->>AdapterManager: Announce New Adapter
    AdapterManager-->>ChatSession: Notify New Adapter
    ChatSession-->>ChatUI: Announce New Adapter
    ChatUI-->>User: Display Adapter Notification
```

### Testing

The project includes comprehensive test suites and procedures for verifying functionality.

#### Running Tests

1. **Unit Tests**
```bash
# Run all tests with detailed output
dotnet test --logger "console;verbosity=detailed"

# Run specific test project
dotnet test LLMAdapterClient.Publisher.Tests --logger "console;verbosity=detailed"

# Run specific test class
dotnet test --filter "FullyQualifiedName~LLMAdapterClient.Publisher.Tests.AdapterTests"

# Run specific test method
dotnet test --filter "FullyQualifiedName=LLMAdapterClient.Publisher.Tests.AdapterTests.AdapterSelector_ShouldFindValidAdapterDirectories"
```

2. **Test Coverage**
```bash
# Generate test coverage report
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=lcov /p:CoverletOutput=./lcov.info
```

3. **Integration Testing**

Build and run the Publisher in debug mode:
```bash
dotnet build LLMAdapterClient.Publisher -c Debug
dotnet run --project LLMAdapterClient.Publisher -c Debug -- --verbose
```

Verify the output shows:
- Successful adapter discovery
- Proper file validation
- Metadata extraction
- Successful publishing to shared storage

4. **Manual Testing**

a. Test Adapter Discovery:
```bash
cd LLMAdapterClient.Publisher
dotnet run
```

Expected output:
```
Available adapters:
- best_model_adapter
- checkpoint_epoch_2_adapter
- checkpoint_epoch_4_adapter
```

b. Test Chat Client:
```bash
cd LLMAdapterClient.ChatClient
dotnet run
```

Expected output:
```
LLM Adapter Chat Client
======================
[System] Loading the latest adapter...
[System] Initializing model with adapter 'best_model_adapter'...
[System] Welcome to the LLM Chat! Type /help for available commands.
>
```

c. Test Chat Commands:
```
> /help
[System] Available commands:
[System]   /help   - Display this help message
[System]   /exit   - Exit the chat session
[System]   /quit   - Exit the chat session
[System]   /clear  - Clear the console and chat history
```

d. Test Chat Interaction:
```
> Hello, how are you today?
[System] Thinking...
[Assistant] I'm doing well, thank you for asking! As an AI assistant, I don't have feelings in the human sense, but I'm functioning properly and ready to help you with any questions or tasks you might have. How can I assist you today?

>
```

5. **Debugging**

Run with debugger:
```bash
dotnet run --project LLMAdapterClient.Publisher --launch-profile "Publisher.Debug"
```

Key breakpoint locations:
- AdapterSelector.GetAvailableAdapterDirectories()
- AdapterValidator.ValidateAdapter()
- AdapterInfoExtractor.ExtractAdapterInfoAsync()
- AdapterUploader.UploadAdapterAsync()
- PythonProcessManager.StartAsync()
- PythonProcessManager.SendCommandAsync()
- PythonProcessManager.SendCommandStreamingAsync()
- ModelService.InitializeAsync()
- ModelService.GenerateResponseAsync()
- ModelService.GenerateStreamingResponseAsync()
- ModelService.ExecuteSpecialCommandAsync()
- ConsoleChatUI.DisplayMessage()
- ConsoleChatUI.DisplayStreamingMessageAsync()
- ChatSession.StartAsync()
- ChatSession.HandleUserMessageAsync()
- ChatSession.HandleSpecialCommandAsync()

6. **Error Handling Tests**

Test Python process errors:
```bash
# Create an invalid Python script
echo 'import sys; sys.exit(1)' > invalid_script.py

# Test PythonProcessManager error handling
dotnet run --project LLMAdapterClient.ChatClient.Tests -- --filter "PythonProcessManagerTests_ShouldHandleErrors"
```

Test timeout protection:
```bash
# Using the chat client
> /timeout_test

# The chat session should recover after timeout (2 minutes)
[System] Response generation timed out.
```

7. **Cleanup**
```bash
# Remove test artifacts
rm -rf ~/.local/share/LLMAdapterClient/shared_storage/*
rm -rf llm_training-main/checkpoints/invalid_adapter
rm -f test_script.py invalid_script.py
```

Current test results:
```
Passed!  - Failed: 0, Passed: 6, Skipped: 0, Total: 6, Duration: 12 ms - LLMAdapterClient.Common.Tests.dll
Passed!  - Failed: 0, Passed: 5, Skipped: 0, Total: 5, Duration: 24 ms - LLMAdapterClient.Publisher.Tests.dll
Passed!  - Failed: 0, Passed: 28, Skipped: 0, Total: 28, Duration: 1.2s - LLMAdapterClient.ChatClient.Tests.dll
```

The test suite includes:
- Unit tests for core interfaces (IAdapterInfo, IAdapterPublisher, IPythonProcessManager, etc.)
- Tests for event handling and adapter metadata
- Tests for adapter selection and validation
- Tests for metadata extraction
- Tests for publisher functionality
- Tests for Python process management and communication
- Tests for model service initialization and response generation
- Tests for adapter manager loading and monitoring
- Tests for chat UI rendering and user input
- Tests for chat session management and command processing

## Implementation Status

- ✅ Phase 1: Project Structure and Common Components
  - Created solution structure with three projects
  - Implemented core interfaces (IAdapterInfo, IAdapterPublisher)
  - Added comprehensive tests for interfaces
- ✅ Phase 2: Publisher Implementation
  - ✅ Implemented adapter selection service
  - ✅ Implemented adapter validation service
  - ✅ Implemented metadata extraction service
  - ✅ Implemented adapter upload system
  - ✅ Implemented publisher service with event handling
- ✅ Phase 3: Chat Client Implementation
  - ✅ Implemented Python process management
  - ✅ Created tests for Python process interaction
  - ✅ Implemented model service with adapter support
  - ✅ Implemented adapter manager for loading and monitoring adapters
  - ✅ Implemented chat UI with colored output and token streaming
  - ✅ Implemented chat session with command handling and conversation flow
- 🔄 Phase 4: Integration and System Tests
  - ⏳ Implementing integration tests for system components
  - ⏳ Creating system configuration management
  - ⏳ Developing end-to-end tests for complete workflow validation

## Python Adapter Structure

The Python training system generates adapter files with the following structure:

```
best_model_adapter/
├── README.md                # Usage instructions
├── adapter_config.json      # Configuration parameters
├── adapter_model.safetensors # Model weights
└── metadata.pt              # Training metadata
```

## Documentation

- [Implementation Plan](IMPLEMENTATION.md): Detailed TDD implementation plan
- [Project Story](STORY.md): Narrative description of how the system works
