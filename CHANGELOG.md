# CHANGELOG


## v0.2.0 (2025-03-01)

### Bug Fixes

- Enhance streaming response processing in PythonModelService
  ([`c9a57e7`](https://github.com/gsinghjay/LLMAdapterClient/commit/c9a57e768c51f08dfa4deb4de1f89789590b7d53))

- Ensure config.yaml is copied to test output directory
  ([`674f5d1`](https://github.com/gsinghjay/LLMAdapterClient/commit/674f5d1267619fcc01c0d356f7429baae9412c85))

- Improve error handling in chat session and UI components
  ([`56c54f7`](https://github.com/gsinghjay/LLMAdapterClient/commit/56c54f7f95dbeedd88801f1bc6ee9b58c3ff5588))

- Improve token handling in PythonProcessManager for streaming responses
  ([`851981a`](https://github.com/gsinghjay/LLMAdapterClient/commit/851981ae77596b6e826b7ab8dff2955bef29c6b4))

### Build System

- Add dotnet installation script
  ([`7cec9e4`](https://github.com/gsinghjay/LLMAdapterClient/commit/7cec9e492685f8379c4458c494f531dfa6e413ae))

- **chatclient**: Add Publisher project reference to ChatClient.Tests
  ([`6f72b3b`](https://github.com/gsinghjay/LLMAdapterClient/commit/6f72b3b6bb491ba1387ae6e1d4c3040368abbde5))

- **chatclient**: Upgrade ChatClient project to .NET 9
  ([`32b2ea3`](https://github.com/gsinghjay/LLMAdapterClient/commit/32b2ea3d036d2371fbbd323ae8ed4797c30ff4f4))

- **common**: Upgrade Common project to .NET 9
  ([`fa967da`](https://github.com/gsinghjay/LLMAdapterClient/commit/fa967dab0b5be27afc48f0aaacc916f94c29fba8))

- **publisher**: Upgrade Publisher project to .NET 9
  ([`c11af1c`](https://github.com/gsinghjay/LLMAdapterClient/commit/c11af1ca78d4ecace7888c81151ea0cda134a0de))

- **sdk**: Add global.json to specify .NET 9 SDK version
  ([`370d6a3`](https://github.com/gsinghjay/LLMAdapterClient/commit/370d6a34a33e0c2ef598347a6b1f516af0e9bcd9))

- **tests**: Upgrade Common.Tests project to .NET 9 with updated package versions
  ([`9f9ff4a`](https://github.com/gsinghjay/LLMAdapterClient/commit/9f9ff4a3a911ec094f2ae11b8c94759ce5c1d6ab))

- **tests**: Upgrade Publisher.Tests project to .NET 9 with updated package versions
  ([`e68d24b`](https://github.com/gsinghjay/LLMAdapterClient/commit/e68d24b81c07711072057d5418d701c82d7c8f4b))

### Chores

- Update solution file to include new test projects
  ([`14e4c3d`](https://github.com/gsinghjay/LLMAdapterClient/commit/14e4c3d6c9f9145e09d4b24bdb5faf745dcd63ce))

- Uploaded generated checkpoints
  ([`ddc1474`](https://github.com/gsinghjay/LLMAdapterClient/commit/ddc1474c2d6ddffa8f8f8cd79362c3c04b587f5c))

### Documentation

- Update documentation for adapter management implementation
  ([`872c5f1`](https://github.com/gsinghjay/LLMAdapterClient/commit/872c5f198e7d65bfcb1894858fc26773f98b4890))

- Update documentation to reflect completed ModelService implementation
  ([`29fe409`](https://github.com/gsinghjay/LLMAdapterClient/commit/29fe409512fa56b39919d34dde43ec81fc6ba31c))

- Update implementation plan to reflect completed Python process manager
  ([`176bed4`](https://github.com/gsinghjay/LLMAdapterClient/commit/176bed46288da6a6136b5e14e03fb350730f378b))

- **implementation**: Update TDD plan to reflect completed Chat UI and Session components
  ([`92b9d6d`](https://github.com/gsinghjay/LLMAdapterClient/commit/92b9d6de448981378b8912b32cc5416c4db34a17))

- **readme**: Update documentation to include Python Process Manager implementation
  ([`439f8d6`](https://github.com/gsinghjay/LLMAdapterClient/commit/439f8d662548935d3bd532c950f52acba1a900ed))

- **readme**: Update implementation status and component documentation
  ([`8e30260`](https://github.com/gsinghjay/LLMAdapterClient/commit/8e30260fed2428c342b09ff1b0fa31415289f2cf))

- **story**: Update project narrative to include Python Process Manager
  ([`7c67684`](https://github.com/gsinghjay/LLMAdapterClient/commit/7c6768407d35bf068446482aec69772ce4844ea1))

- **story**: Update project narrative with completed Chat UI and Session components
  ([`fc61d9e`](https://github.com/gsinghjay/LLMAdapterClient/commit/fc61d9eba8af62d597670634bc193273d2a12a30))

### Features

- Implement PythonProcessManager in ChatClient
  ([`ccbb7cb`](https://github.com/gsinghjay/LLMAdapterClient/commit/ccbb7cb3c3f0669175d58d128669993a43b99a6c))

- Update interfaces.cs with IPythonProcessManager interface
  ([`b2df66a`](https://github.com/gsinghjay/LLMAdapterClient/commit/b2df66a69e6ee440c5690b6de34bca2810e9728a))

- **chat**: Implement AdapterManager service with event handling
  ([`dfdd401`](https://github.com/gsinghjay/LLMAdapterClient/commit/dfdd40127ddf8cd2cde7efca2f4a3e1b989e9200))

- **chatclient**: Implement Chat UI and Chat Session components
  ([`bb36a7f`](https://github.com/gsinghjay/LLMAdapterClient/commit/bb36a7feb8b8e1108cbd5f7351de015f94b61172))

- **chatclient**: Implement ModelService for LLM interaction
  ([`7788c52`](https://github.com/gsinghjay/LLMAdapterClient/commit/7788c52a497037c8c952d9234f4c984bc9e1f6ca))

- **common**: Update IModelService interface with skipValidation parameter
  ([`11d7162`](https://github.com/gsinghjay/LLMAdapterClient/commit/11d71621c1c9218833ba47214f112cca581fcdfd))

### Refactoring

- **common**: Update interfaces for adapter management
  ([`db52241`](https://github.com/gsinghjay/LLMAdapterClient/commit/db52241b70a39aec747a4ad6d377ad4cb219d00e))

### Testing

- Add tests for PythonProcessManager
  ([`ff38356`](https://github.com/gsinghjay/LLMAdapterClient/commit/ff38356324fe8de1ccc93a8bbc23ed4ca1b90627))

- Update model service tests to properly validate config path usage
  ([`a4fe0a2`](https://github.com/gsinghjay/LLMAdapterClient/commit/a4fe0a22078a4967dce7f60ef031846f1beff6b0))

- **chat**: Add comprehensive tests for AdapterManager
  ([`29a3b56`](https://github.com/gsinghjay/LLMAdapterClient/commit/29a3b567114b66f2e69f5c8cb38a4158ba15f1b3))

- **chatclient**: Add comprehensive tests for ModelService
  ([`6d32cd8`](https://github.com/gsinghjay/LLMAdapterClient/commit/6d32cd848fea89a15422933b37b994444dfa25cf))


## v0.1.0 (2025-02-28)

### Bug Fixes

- **tests**: Add nullable reference type to FindSolutionDirectory method
  ([`3e1f331`](https://github.com/gsinghjay/LLMAdapterClient/commit/3e1f331f00f21167eaeabda14748b8b04d985dce))

### Chores

- Ignore archive folder
  ([`661d1a1`](https://github.com/gsinghjay/LLMAdapterClient/commit/661d1a1933c7399e069afa1c3900c252ea33096c))

- Update gitignore for training system
  ([`adeefba`](https://github.com/gsinghjay/LLMAdapterClient/commit/adeefbaca3a20c7d821b2d9c2e92200e1ab33cb4))

- Update solution configuration and gitignore
  ([`3e0b90a`](https://github.com/gsinghjay/LLMAdapterClient/commit/3e0b90ae56b571ca7a54d83f924bbb701a04e62e))

- **git**: Add comprehensive .NET and C# ignore patterns
  ([`981c55c`](https://github.com/gsinghjay/LLMAdapterClient/commit/981c55ca71ecd20e21aa7c751ca72bc90b212a38))

- **test**: Add publisher test project configuration
  ([`821a3d4`](https://github.com/gsinghjay/LLMAdapterClient/commit/821a3d43ace4d4ac31ffee630cff0913fa0a41e8))

### Continuous Integration

- Added semantic release to adhere to keep-a-changelog
  ([`097113c`](https://github.com/gsinghjay/LLMAdapterClient/commit/097113c795e9f6be04818aa860fb44a105dac1e5))

### Documentation

- Add implementation plan and project story documentation
  ([`c9498b2`](https://github.com/gsinghjay/LLMAdapterClient/commit/c9498b2a25ecfdc42212e236b7e064a3d06dcfcd))

- Add implementation plan and project story documentation
  ([`48f772b`](https://github.com/gsinghjay/LLMAdapterClient/commit/48f772bfdd370b6f9ee1e7cd2cb6f0dd02a852d2))

- Update implementation status and progress tracking
  ([`bfb6a5b`](https://github.com/gsinghjay/LLMAdapterClient/commit/bfb6a5b565aa2a5449e810ec969c344aa4126cf8))

- Update implementation status for Phase 2 completion
  ([`8347531`](https://github.com/gsinghjay/LLMAdapterClient/commit/8347531bcb905db8707032e44c1d75625c7cbba2))

- **readme**: Update implementation status and add comprehensive testing guide
  ([`b91a4fb`](https://github.com/gsinghjay/LLMAdapterClient/commit/b91a4fb830a8a44cd4269aad94be1638b8ba9d28))

- **story**: Update implementation status and add technical debt section
  ([`18347d1`](https://github.com/gsinghjay/LLMAdapterClient/commit/18347d12d8a234e9737e660e5b6cac6e585dc1c8))

### Features

- Initialize solution structure with core projects
  ([`7766c72`](https://github.com/gsinghjay/LLMAdapterClient/commit/7766c72b5ee2fbe65b3d8d366b314fa4e4d5aae0))

- Integrate Python training system for adapter generation
  ([`731de41`](https://github.com/gsinghjay/LLMAdapterClient/commit/731de41518e281170e4a8a4b2c1c8da8baed5395))

- **publisher**: Add manual adapter publishing functionality
  ([`bbbb3eb`](https://github.com/gsinghjay/LLMAdapterClient/commit/bbbb3eb94ee04d8344c557c12e57a14c3eb39513))

- **publisher**: Implement core adapter services
  ([`0b252a9`](https://github.com/gsinghjay/LLMAdapterClient/commit/0b252a9305750fa7720e35c1ae8e1811a78d5b2b))

- **publisher**: Implement core adapter services for selection and validation
  ([`9ad4b82`](https://github.com/gsinghjay/LLMAdapterClient/commit/9ad4b827f486307056915ef806739aaac9064d97))

### Testing

- Add unit tests for common interfaces
  ([`4709cfd`](https://github.com/gsinghjay/LLMAdapterClient/commit/4709cfd9d64d619e0e8242ed8c202b9399a3d3b1))

- **publisher**: Add comprehensive tests for adapter services
  ([`db8b83c`](https://github.com/gsinghjay/LLMAdapterClient/commit/db8b83cef7b703322a28328e7356fb9593df6af9))

- **publisher**: Add comprehensive tests for adapter services
  ([`36956c4`](https://github.com/gsinghjay/LLMAdapterClient/commit/36956c4743e25411edbde32e02806160de5afe6f))
