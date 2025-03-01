## TDD Implementation Plan for LLM Adapter Client

### Phase 1: Set Up Project Structure and Common Components ✅

**Step 1: Initial Project Setup and Basic Tests** ✅
1. Create the solution structure with the three projects:
   - Common (Class Library) ✅
   - Publisher (Console App) ✅
   - ChatClient (Console App) ✅
2. Write tests for basic interfaces in Common:
   - Test for IAdapterInfo model ✅
   - Test for IAdapterPublisher interface ✅

**Step 2: Define Core Interfaces and Models** ✅
1. Implement the IAdapterInfo model to represent adapter metadata:
   ```csharp
   // Test: Should load adapter metadata correctly
   public interface IAdapterInfo
   {
       string Name { get; }
       string FilePath { get; }
       DateTime Created { get; }
       Dictionary<string, object> Metadata { get; }
   }
   ```
2. Implement the IAdapterPublisher interface:
   ```csharp
   // Test: Publisher should notify when new adapters are available
   public interface IAdapterPublisher
   {
       event EventHandler<AdapterEventArgs> AdapterPublished;
       IReadOnlyList<IAdapterInfo> GetAvailableAdapters();
       Task<IAdapterInfo> GetLatestAdapterAsync();
   }
   ```

**Implementation Details:**
- Created solution structure with proper project references ✅
- Implemented core interfaces in `LLMAdapterClient.Common/Interfaces.cs` ✅
- Added comprehensive XML documentation to all interfaces and classes ✅
- Created test project `LLMAdapterClient.Common.Tests` with xUnit ✅
- Implemented tests for all interfaces using Moq for mocking ✅
- Set up Cursor project rules (.mdc files) for better code organization ✅

### Phase 2: Implement Manual Publisher ✅

**Step 3: Adapter Upload Tests** ✅
1. Write tests for manual adapter uploading:
   - Test for selecting adapter files from the checkpoints directory structure ✅
   - Test for reading adapter metadata from .pt and .json files ✅
   - Test for validating adapter file integrity (all required files present) ✅
   - Test for uploading complete adapter package to shared storage ✅
   - Use existing adapters from the `llm_training-main/checkpoints` folder for realistic testing ✅

**Step 4: Implement Adapter Selector** ✅
1. Create adapter selection functionality:
   ```csharp
   // Test: Should allow selecting an adapter file
   public class AdapterSelector
   {
       public IEnumerable<string> GetAvailableAdapterDirectories();
       // Implementation to select an adapter file manually
   }
   ```

**Step 5: Implement Adapter Information Extraction** ✅
1. Create class to extract metadata from adapter files:
   ```csharp
   // Test: Should correctly extract metadata from adapter files
   public class AdapterInfoExtractor
   {
       public Task<IAdapterInfo> ExtractAdapterInfoAsync(string filePath);
       // Implementation to read metadata from adapter files
   }
   ```

**Step 6: Implement Adapter Upload System** ✅
1. Create adapter upload system:
   ```csharp
   // Test: Should upload adapter files to shared storage
   public class AdapterUploader
   {
       public Task<string> UploadAdapterAsync(string sourcePath, string targetStorage);
   }
   ```

**Step 7: Implement Publisher Service** ✅
1. Combine components into a Publisher service:
   ```csharp
   // Test: Should publish adapters when triggered manually
   public class AdapterPublisherService : IAdapterPublisher
   {
       // Implementation combining Selector, InfoExtractor, and Uploader
   }
   ```

### Phase 3: Implement Chat Client with Python Integration 🔄

**Step 8: Python Process Management Tests** ✅
1. Write tests for launching and managing the Python process:
   - Test for starting the Python chat mode with proper arguments ✅
   - Test for sending commands to the Python process ✅
   - Test for reading responses from the Python process, including streaming token responses ✅
   - Test for handling special commands (/clear, /loadrag, etc.) ✅
   - Test for graceful process termination ✅

2. Implement Python process manager:
   ```csharp
   // Test: Should start and communicate with Python process
   public interface IPythonProcessManager
   {
       Task StartAsync(string pythonPath, string scriptPath, string[] args);
       Task<string> SendCommandAsync(string command, CancellationToken token = default);
       Task<IAsyncEnumerable<string>> SendCommandStreamingAsync(string command, CancellationToken token = default);
       Task StopAsync();
   }

   public class PythonProcessManager : IPythonProcessManager
   {
       private Process _process;
       private StreamWriter _inputWriter;
       private StreamReader _outputReader;
       private TaskCompletionSource<bool> _startTcs;
       
       // Implementation using Process class to manage Python process
       // Must handle streaming token-by-token responses
   }
   ```

**Step 9: Model Service Implementation** 🔄
1. Create model service to interface with Python-powered LLM:
   ```csharp
   // Test: Should communicate with Python process for model responses
   public interface IModelService
   {
       Task InitializeAsync(IAdapterInfo adapter, string configPath = null);
       Task<string> GenerateResponseAsync(string prompt, CancellationToken token = default);
       Task<IAsyncEnumerable<string>> GenerateStreamingResponseAsync(string prompt, CancellationToken token = default);
       Task ExecuteSpecialCommandAsync(string command);
       Task ShutdownAsync();
   }

   public class PythonModelService : IModelService
   {
       private readonly IPythonProcessManager _processManager;
       
       // Implementation using Python process manager
       // Must handle the specific command formats used by main.py
       // Support for special commands like /clear, /loadrag, /ragstatus
   }
   ```

**Step 10: Adapter Manager Implementation** ⏳
1. Create adapter manager to handle loading adapters:
   ```csharp
   // Test: Should load the correct adapter and announce new adapters
   public class AdapterManager
   {
       public event EventHandler<AdapterEventArgs> NewAdapterAnnounced;
       public Task<IAdapterInfo> LoadAdapterAsync(string adapterPath);
       public Task<IAdapterInfo> GetLatestAdapterAsync(IAdapterPublisher publisher);
       public Task InitializeModelWithAdapterAsync(IModelService modelService, IAdapterInfo adapter);
       public Task MonitorForNewAdaptersAsync(IAdapterPublisher publisher, CancellationToken token = default);
   }
   ```

**Step 11: Implement Chat UI** ⏳
1. Create basic UI for chat interaction:
   ```csharp
   // Test: Should display messages, handle user input, and announce adapters
   public interface IChatUI
   {
       void DisplayMessage(string role, string message);
       void DisplayStreamingMessage(string role, IAsyncEnumerable<string> messageTokens);
       void AnnounceNewAdapter(IAdapterInfo adapter);
       Task<string> GetUserInputAsync();
       void DisplayError(string message);
       void DisplaySystemMessage(string message);
   }

   public class ConsoleChatUI : IChatUI
   {
       // Implementation using Console for UI interaction
       // Handle colored output for different message types
       // Support token-by-token streaming display
   }
   ```

**Step 12: Implement Chat Session** ⏳
1. Create chat session to manage the conversation:
   ```csharp
   // Test: Should maintain chat context and handle messages
   public class ChatSession
   {
       private readonly IModelService _modelService;
       private readonly IAdapterManager _adapterManager;
       private readonly IChatUI _chatUI;
       
       public Task StartAsync();
       public Task HandleMessageAsync(string message);
       public Task HandleSpecialCommandAsync(string command);
       public Task AnnounceAndLoadAdapterAsync(IAdapterInfo adapter);
   }
   ```

**Implementation Details for Phase 3:**
- Python Process Manager must launch main.py with --mode chat and appropriate config ✅
- Support parsing and handling token-by-token streaming responses from the Python process ✅
- Implement proper error handling for Python process crashes or communication issues ✅
- Support special commands that interact with the underlying Python script (/clear, /loadrag, etc.) ✅
- Create mechanisms to pass adapter paths from shared storage to the Python script 🔄
- Ensure graceful shutdown of the Python process on application exit ✅

### Phase 4: Integration and System Tests ⏳

**Step 13: Integration Tests**
1. Write tests for Python process communication:
   - Test for proper command serialization and deserialization
   - Test for error handling during communication
   - Test for process restart capabilities
   - Use the actual Python script for integration testing

**Step 14: System Configuration**
1. Implement configuration system:
   ```csharp
   // Test: Should load configuration correctly
   public class AdapterClientConfig
   {
       public string PythonPath { get; set; }
       public string ScriptPath { get; set; }
       public string AdapterSourceDirectory { get; set; }
       public string SharedStorage { get; set; }
       public string ConfigFilePath { get; set; }
       // Other configuration options
   }
   ```

**Step 15: End-to-End Tests**
1. Create tests that verify the entire workflow:
   - Test adapter uploading to shared storage
   - Test ChatClient loading and using the adapter via Python
   - Test adapter announcements and switching
   - Test error recovery scenarios

### Phase 5: IPFS Integration ⏳

**Step 16: IPFS Client Implementation**
1. Design and implement IPFS client for distributed storage:
   ```csharp
   // Test: Should interact with IPFS network
   public interface IIpfsClient
   {
       Task<string> AddFileAsync(string filePath);
       Task<string> AddDirectoryAsync(string directoryPath);
       Task<bool> GetFileAsync(string cid, string outputPath);
       Task<IEnumerable<string>> ListPinsAsync();
   }

   public class IpfsClient : IIpfsClient
   {
       // Implementation using IPFS HTTP API
   }
   ```

**Step 17: IPFS Adapter Publisher**
1. Extend the publisher to use IPFS:
   ```csharp
   // Test: Should publish adapters to IPFS network
   public class IpfsAdapterPublisher : IAdapterPublisher
   {
       private readonly IIpfsClient _ipfsClient;
       private readonly AdapterSelector _selector;
       private readonly AdapterInfoExtractor _extractor;
       
       // Implementation for publishing adapters to IPFS
   }
   ```

**Step 18: IPFS Adapter Subscriber**
1. Create subscriber for retrieving adapters from IPFS:
   ```csharp
   // Test: Should retrieve adapters from IPFS network
   public class IpfsAdapterSubscriber
   {
       private readonly IIpfsClient _ipfsClient;
       private readonly string _downloadDirectory;
       
       public Task<IAdapterInfo> SubscribeToAdapterAsync(string cid);
       public Task<IEnumerable<IAdapterInfo>> ListAvailableAdaptersAsync();
   }
   ```

**Step 19: IPFS Integration Tests**
1. Create tests for IPFS functionality:
   - Test for adding files to IPFS
   - Test for retrieving files from IPFS
   - Test for pinning and unpinning content
   - Test for adapter publishing and subscribing via IPFS

### Phase 6: Future Website Integration Preparations ⏳

**Step 20: Web API Design**
1. Design interfaces to prepare for eventual website implementation:
   ```csharp
   // Test: Should have interfaces that can be implemented by a web frontend
   public interface IAdapterUploadService
   {
       Task<IAdapterInfo> UploadAdapterAsync(Stream adapterStream, string filename);
       // Implementation can be used by both console and web apps
   }
   
   public interface IChatService
   {
       Task<ChatSession> CreateSessionAsync();
       Task<string> SendMessageAsync(Guid sessionId, string message);
       Task EndSessionAsync(Guid sessionId);
   }
   ```

## Implementation Order and Milestones

### Milestone 1: Basic Functionality ✅
- Complete Steps 1-7 (Common interfaces and Manual Publisher)
- Test with existing Python-generated adapters from checkpoints folder
- Verify Publisher can upload adapter files manually

### Milestone 2: Chat Integration with Python 🔄
- Complete Steps 8-12 (ChatClient implementation with Python process integration)
- Implement Python communication protocol for main.py
- Create robust process management with streaming response handling
- Build chat UI with adapter switching capabilities
- Support special commands (/clear, /loadrag, etc.)
- Test ChatClient with real adapter files from the checkpoints folder

### Milestone 3: Complete System Integration ⏳
- Complete Steps 13-15 (Integration and system tests)
- Test end-to-end workflow with real Python inference
- Implement error handling and recovery
- Create comprehensive configuration system

### Milestone 4: IPFS Enhancement ⏳
- Complete Steps 16-19 (IPFS integration)
- Implement distributed storage for adapters
- Create peer-to-peer adapter sharing
- Ensure backward compatibility with local storage

### Milestone 5: Web API Preparation ⏳
- Complete Step 20 (Web API design)
- Create service interfaces for web integration
- Implement serialization protocols
- Design for scalability with multiple users

## Testing Strategy
1. **Unit Tests**: For individual components (AdapterSelector, ModelService, etc.)
2. **Integration Tests**: For component interactions (Publisher with Uploader, Python process communication, etc.)
3. **System Tests**: For end-to-end functionality with manual operations
4. **Real Adapter Tests**: Using existing adapters from the checkpoints folder (like best_model_adapter)
5. **IPFS Tests**: For distributed storage functionality

## Technical Debt and Future Improvements

### Logging Infrastructure
- [ ] Add structured logging
- [ ] Add telemetry for operations
- [ ] Add performance metrics

### Configuration System
- [ ] Add configuration for Python path and script
- [ ] Add settings for IPFS node connection
- [ ] Add retry policy configuration
- [ ] Add adapter versioning policies

### Additional Features
- [ ] Add progress tracking for large files
- [ ] Add retry logic for failed uploads
- [ ] Add background monitoring for new adapters
- [ ] Add metadata caching
- [ ] Add adapter validation with checksums
- [ ] Add adapter quality metrics

This TDD plan follows SOLID principles, keeps the code DRY, maintains backward compatibility with the Python system, and ensures proper documentation. Each step builds incrementally on the previous ones, allowing for continuous testing and validation.