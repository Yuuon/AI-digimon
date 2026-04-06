# Architecture

**Analysis Date:** 2026-04-06

## Pattern Overview

**Overall:** Clean Architecture with Modular Domain Design

**Key Characteristics:**
- Layered architecture with clear separation of concerns
- Dependency Injection throughout for loose coupling
- Repository Pattern for data abstraction
- Command Pattern for user interactions
- Event-driven communication for decoupled features
- Factory Pattern for AI client abstraction
- Configuration-driven behavior

## Layer Architecture

The solution follows a **5-Project Layered Architecture** with clear dependency rules:

### 1. DigimonBot.Core (Domain Layer)
- **Purpose:** Domain models, service interfaces, and business logic abstractions
- **Location:** `src/DigimonBot.Core/`
- **Contains:** Entity definitions, value objects, service interfaces, events
- **Dependencies:** None (pure domain)
- **Used by:** All other projects

**Key Components:**
- Models: `UserDigimon`, `DigimonDefinition`, `EmotionValues`, `ChatMessage`
- Events: `EvolutionEventArgs`, `EmotionChangedEventArgs`, `TavernAutoSpeakEventArgs`
- Service Interfaces: `IDigimonManager`, `IEvolutionEngine`, `IEmotionTracker`, `IEventPublisher`

### 2. DigimonBot.AI (AI Service Layer)
- **Purpose:** AI client implementations and personality processing
- **Location:** `src/DigimonBot.AI/`
- **Contains:** AI provider clients, emotion analysis, personality engine
- **Dependencies:** DigimonBot.Core
- **Used by:** DigimonBot.Data, DigimonBot.Host

**Key Components:**
- AI Clients: `DeepSeekClient`, `GLMClient` (via `IAIClient`)
- Factory: `AIClientFactory` - creates appropriate client based on configuration
- Services: `PersonalityEngine`, `TavernService`, `BattleService`
- Vision: `VisionService` for image recognition

### 3. DigimonBot.Data (Data Layer)
- **Purpose:** Data persistence and repository implementations
- **Location:** `src/DigimonBot.Data/`
- **Contains:** Repositories, database initialization, data access
- **Dependencies:** DigimonBot.Core, DigimonBot.AI
- **Used by:** DigimonBot.Host

**Key Components:**
- Repository Interfaces: `IDigimonRepository`, `IDigimonStateRepository`, `IUserDataRepository`
- Implementations: `JsonDigimonRepository`, `SqliteDigimonStateRepository`
- Manager: `PersistentDigimonManager` - coordinates state persistence
- Database: `DatabaseInitializer` - SQLite schema management

### 4. DigimonBot.Messaging (Application Layer)
- **Purpose:** Message handling and command processing
- **Location:** `src/DigimonBot.Messaging/`
- **Contains:** Command handlers, message processors, command registry
- **Dependencies:** DigimonBot.Core, DigimonBot.AI, DigimonBot.Data
- **Used by:** DigimonBot.Host

**Key Components:**
- Handler: `DigimonMessageHandler` - main message processing orchestrator
- Commands: 20+ command implementations (Status, Evolution, Shop, Battle, etc.)
- Registry: `CommandRegistry` - command routing and discovery

### 5. DigimonBot.Host (Infrastructure Layer)
- **Purpose:** Application entry point and service composition
- **Location:** `src/DigimonBot.Host/`
- **Contains:** `Program.cs`, `BotService`, configuration, DI setup
- **Dependencies:** All other projects
- **Used by:** None (top-level)

**Key Components:**
- Entry Point: `Program.cs` - service registration and configuration
- Bot Service: `BotService` - WebSocket connection to NapCatQQ
- Config: `AppSettings`, `GroupModeConfig` - configuration models

## Project Dependency Graph

```
                    DigimonBot.Host
                           │
        ┌──────────────────┼──────────────────┐
        │                  │                  │
DigimonBot.Core ←── DigimonBot.AI ←── DigimonBot.Data
        │                  │                  │
        └──────────────────┴──────────────────┘
                           │
                  DigimonBot.Messaging
```

**Dependency Rule:** Dependencies point inward toward Core. Core has no external dependencies.

## Data Flow: QQ Message → AI Response

**Complete Flow:**

```
1. NapCatQQ (QQ Platform)
   ↓ WebSocket
2. BotService (src/DigimonBot.Host/BotService.cs)
   - Receives OneBot event via WebSocket
   - Parses message content, extracts @mentions, images
   - Builds MessageContext
   ↓
3. DigimonMessageHandler (src/DigimonBot.Messaging/Handlers/DigimonMessageHandler.cs)
   - Determines message type (command vs conversation)
   - Routes to appropriate handler
   ↓
4a. Command Path:
    CommandRegistry → ICommand.ExecuteAsync → CommandResult
    ↓
4b. Conversation Path:
    IDigimonManager.GetOrCreateAsync → UserDigimon
    ↓
    IAIClient.ChatAsync(systemPrompt, history) → AIResponse
    ↓
    IEmotionTracker.ApplyEmotionAnalysisAsync (async background)
    ↓
    IEvolutionEngine.CheckAndEvolveAsync → EvolutionResult?
    ↓
5. BotService.SendGroupMessageAsync / SendPrivateMessageAsync
   ↓ HTTP API
6. NapCatQQ → QQ Platform
```

## Key Abstractions

### IAIClient - AI Provider Abstraction
- **Purpose:** Unified interface for multiple AI providers
- **Location:** `src/DigimonBot.AI/Services/IAIClient.cs`
- **Implementations:** `DeepSeekClient`, `GLMClient`
- **Factory:** `AIClientFactory` creates appropriate implementation
- **Pattern:** Strategy + Factory

```csharp
public interface IAIClient
{
    Task<AIResponse> ChatAsync(List<ChatMessage> history, string systemPrompt);
    Task<EmotionAnalysis> AnalyzeEmotionAsync(string userMessage, string aiResponse);
}
```

### IDigimonManager - Domain Aggregate Root
- **Purpose:** Manages user-digimon relationship lifecycle
- **Location:** `src/DigimonBot.Core/Services/IDigimonManager.cs`
- **Implementation:** `PersistentDigimonManager`
- **Pattern:** Repository + Unit of Work

**Key Methods:**
- `GetOrCreateAsync(userId)` - retrieve or initialize
- `RecordConversationAsync` - log interaction and update tokens
- `UpdateDigimonAsync` - handle evolution

### ICommand - Command Pattern
- **Purpose:** Encapsulate user commands as objects
- **Location:** `src/DigimonBot.Messaging/Commands/ICommand.cs`
- **Pattern:** Command Pattern with Registry

```csharp
public interface ICommand
{
    string Name { get; }
    string[] Aliases { get; }
    string Description { get; }
    Task<CommandResult> ExecuteAsync(CommandContext context);
}
```

### IMessageHandler - Message Processing Pipeline
- **Purpose:** Process incoming messages and generate responses
- **Location:** `src/DigimonBot.Messaging/Handlers/IMessageHandler.cs`
- **Implementation:** `DigimonMessageHandler`
- **Pattern:** Chain of Responsibility + Strategy

## Entry Points

### Application Entry Point
- **Location:** `src/DigimonBot.Host/Program.cs`
- **Responsibilities:**
  - Configuration loading (`appsettings.json`, environment variables)
  - Dependency injection registration (60+ services)
  - Database initialization
  - Hosted service startup (`BotService`)

### Message Entry Point
- **Location:** `src/DigimonBot.Host/BotService.cs`
- **Responsibilities:**
  - WebSocket connection management to NapCatQQ
  - OneBot event parsing
  - Message routing to `IMessageHandler`
  - Response sending via HTTP API

### Command Entry Point
- **Location:** `src/DigimonBot.Messaging/Handlers/DigimonMessageHandler.cs`
- **Method:** `HandleCommandAsync`
- **Flow:** Parse command → Lookup registry → Execute → Return result

## Configuration Management

**Configuration Sources (Priority Order):**
1. `appsettings.json` (base configuration)
2. Environment variables (override)

**Key Configuration Sections:**
- `QQBot:NapCat` - WebSocket/HTTP connection settings
- `AI` - Provider, API key, model, temperature settings
- `Data` - Database paths, persistence provider
- `Admin` - Whitelist, permission settings

**Configuration Pattern:**
```csharp
services.Configure<AppSettings>(hostContext.Configuration);
var settings = hostContext.Configuration.Get<AppSettings>();
```

## State Management

**Hybrid State Architecture:**

| State Type | Storage | Persistence | Location |
|------------|---------|-------------|----------|
| Core State (emotions, tokens) | SQLite | Persistent | `UserDigimonState` table |
| Chat History | Memory | Ephemeral | `ConcurrentDictionary<string, List<ChatMessage>>` |
| Digimon Definitions | JSON Files | Read-only | `Data/digimon_database.json` |
| User Economy | SQLite | Persistent | `UserEconomy` table |
| Inventory | SQLite | Persistent | `UserInventory` table |

**Rationale:** Chat history is kept in memory for performance (frequent access), while core progression state is persisted to SQLite.

## Event System

**Purpose:** Decouple features that shouldn't have direct dependencies

**Event Types:**
- `OnEvolved` - Evolution completed
- `OnEmotionChanged` - Emotion values updated
- `OnEvolutionReady` - Multiple evolution options available
- `OnTavernAutoSpeak` - Character wants to speak in group

**Publisher:** `EventPublisher` (injected as `IEventPublisher`)

**Subscribers:**
- `BotService` subscribes to `OnTavernAutoSpeak` and `OnEvolutionReady`

## Error Handling Strategy

**Approach:** Graceful degradation with logging

**Patterns:**
1. Try-catch at service boundaries (BotService, handlers)
2. Return fallback responses on errors
3. Async error isolation (background tasks for emotion analysis)
4. Structured logging with correlation IDs

**Example from DigimonMessageHandler:**
```csharp
try
{
    var emotionAnalysis = await _aiClient.AnalyzeEmotionAsync(...);
    await _emotionTracker.ApplyEmotionAnalysisAsync(...);
}
catch (Exception ex)
{
    _logger.LogError(ex, "情感分析异常");
    // Continue without emotion update - don't fail the conversation
}
```

## Cross-Cutting Concerns

**Logging:**
- Framework: Microsoft.Extensions.Logging
- Provider: Console logging
- Levels: Debug (development), Information (production)

**Configuration:**
- Microsoft.Extensions.Configuration
- JSON + Environment variable sources
- Strongly-typed configuration classes

**HTTP Clients:**
- IHttpClientFactory for AI clients
- Named clients per provider
- Configurable timeouts

---

*Architecture analysis: 2026-04-06*
