# Codebase Structure

**Analysis Date:** 2026-04-06

## Directory Layout

```
E:\Projects\AI-digimon/
├── src/                          # Source projects
│   ├── DigimonBot.Core/          # Domain models and interfaces
│   ├── DigimonBot.AI/            # AI service implementations
│   ├── DigimonBot.Data/          # Data persistence layer
│   ├── DigimonBot.Messaging/     # Message handling and commands
│   └── DigimonBot.Host/          # Application host and entry point
├── tests/                        # Test projects
│   ├── DigimonBot.Core.Tests/    # Core domain tests
│   ├── DigimonBot.AI.Tests/      # AI service tests
│   └── IntegrationTest/          # Integration tests
├── tools/                        # Development tools
│   └── EvolutionEditor/          # Evolution path editor (WPF)
├── Data/                         # Runtime data files (JSON, SQLite)
└── DigimonBot.sln                # Solution file
```

## Source Project Structure

### DigimonBot.Core (`src/DigimonBot.Core/`)
**Purpose:** Domain layer - models, interfaces, events

```
DigimonBot.Core/
├── DigimonBot.Core.csproj        # Project file (no external deps)
├── Models/                       # Domain entities and value objects
│   ├── DigimonDefinition.cs      # Digimon species definition
│   ├── DigimonStage.cs           # Evolution stage enum
│   ├── DigimonPersonality.cs     # Personality types
│   ├── EmotionType.cs            # Emotion attribute enum
│   ├── UserDigimon.cs            # User's digimon instance (partial in Def)
│   ├── ChatMessage.cs            # Chat history entry
│   ├── EmotionAnalysis.cs        # AI emotion analysis result
│   ├── EmotionValues.cs          # Courage/Friendship/Love/Knowledge
│   ├── ItemDefinition.cs         # Shop items
│   ├── UserEconomy.cs            # Gold/currency
│   ├── CheckInRecord.cs          # Daily check-in tracking
│   ├── AdminConfig.cs            # Whitelist settings
│   ├── DigimonPersonalityConfig.cs  # Personality definitions
│   └── DigimonDialogueConfig.cs  # Dialogue templates
├── Services/                     # Service interfaces (abstractions)
│   ├── IDigimonManager.cs        # Digimon lifecycle management
│   ├── IEvolutionEngine.cs       # Evolution logic interface
│   ├── IEmotionTracker.cs        # Emotion tracking interface
│   ├── IEventPublisher.cs        # Event system interface
│   ├── ITavernService.cs         # Tavern/character RP service
│   ├── ITavernConfigService.cs   # Tavern configuration
│   ├── IBattleService.cs         # Battle system
│   ├── IGroupModeConfig.cs       # Group chat mode settings
│   └── IMessageHistoryService.cs # Message tracking
└── Events/                       # Domain events
    └── EvolutionEvents.cs        # Evolution, emotion, tavern events
```

**When adding new domain concepts:**
- Add models to `Models/` folder
- Add service interfaces to `Services/` folder
- Add events to `Events/` folder
- Keep project dependency-free

### DigimonBot.AI (`src/DigimonBot.AI/`)
**Purpose:** AI provider implementations and intelligent services

```
DigimonBot.AI/
├── DigimonBot.AI.csproj          # References Core only
└── Services/
    ├── IAIClient.cs              # AI client abstraction
    ├── AIClientFactory.cs        # Factory for creating clients
    ├── DeepSeekClient.cs         # DeepSeek API implementation
    ├── GLMClient.cs              # Zhipu AI implementation
    ├── EmotionAnalysisModels.cs  # Emotion JSON models
    ├── IPersonalityEngine.cs     # Personality processing interface
    ├── PersonalityEngine.cs      # Builds system prompts
    ├── ITavernCharacterParser.cs # Character card parser
    ├── TavernCharacterParser.cs  # PNG card metadata extractor
    ├── TavernService.cs          # Character roleplay logic
    ├── BattleService.cs          # AI-driven battles
    ├── IVisionService.cs         # Image recognition interface
    └── VisionService.cs          # GLM-4V image analysis
```

**When adding a new AI provider:**
1. Create new client class implementing `IAIClient`
2. Add provider enum to `AIClientFactory.AIProvider`
3. Add factory method in `AIClientFactory`
4. Update configuration parsing in `AppSettings.AIConfig`

### DigimonBot.Data (`src/DigimonBot.Data/`)
**Purpose:** Data persistence and repository implementations

```
DigimonBot.Data/
├── DigimonBot.Data.csproj        # References Core and AI
├── Database/
│   └── DatabaseInitializer.cs    # SQLite schema creation
├── Repositories/
│   ├── IDigimonRepository.cs     # Digimon definitions (JSON)
│   ├── IDigimonStateRepository.cs # User state (SQLite)
│   ├── IUserDataRepository.cs    # Economy data (SQLite)
│   ├── IInventoryRepository.cs   # Items (SQLite)
│   ├── ICheckInRepository.cs     # Check-ins (SQLite)
│   ├── IItemRepository.cs        # Item definitions (JSON)
│   ├── JsonDigimonRepository.cs  # JSON file implementation
│   ├── JsonItemRepository.cs     # Items JSON implementation
│   ├── PersistentDigimonManager.cs # State + memory coordinator
│   ├── InMemoryDigimonManager.cs # Testing/development fallback
│   └── Sqlite/                   # SQLite implementations
│       ├── SqliteDigimonStateRepository.cs
│       ├── SqliteUserDataRepository.cs
│       ├── SqliteInventoryRepository.cs
│       └── SqliteCheckInRepository.cs
└── Services/
    └── (extension services)
```

**When adding a new repository:**
1. Define interface in `Repositories/` folder
2. Add SQLite implementation in `Repositories/Sqlite/`
3. Register in `Program.cs` DI container
4. Run `DatabaseInitializer` to add table schema

### DigimonBot.Messaging (`src/DigimonBot.Messaging/`)
**Purpose:** Message processing, commands, and user interaction

```
DigimonBot.Messaging/
├── DigimonBot.Messaging.csproj   # References Core, AI, Data
├── Commands/                     # All bot commands
│   ├── ICommand.cs               # Command interface and context
│   ├── CommandRegistry.cs        # Command routing
│   ├── CommandContext.cs         # Command execution context
│   ├── CommandResult.cs          # Command result
│   ├── StatusCommand.cs          # /status - digimon status
│   ├── EvolutionPathCommand.cs   # /evo - evolution path
│   ├── EvolutionListCommand.cs   # /evolist - available evolutions
│   ├── EvolutionSelectCommand.cs # /evoselect - choose evolution
│   ├── ResetCommand.cs           # /reset - restart digimon
│   ├── HelpCommand.cs            # /help - command list
│   ├── JrrpCommand.cs            # /jrrp - daily luck
│   ├── SetEmotionCommand.cs      # /setemotion - admin emotion edit
│   ├── ShopCommand.cs            # /shop - item shop
│   ├── InventoryCommand.cs       # /inventory - view items
│   ├── UseItemCommand.cs         # /use - use item
│   ├── AttackCommand.cs          # /attack - battle command
│   ├── CheckInCommand.cs         # /checkin - daily reward
│   ├── WhatIsThisCommand.cs      # /识图 - image recognition
│   ├── TavernToggleCommand.cs    # /酒馆开关 - toggle tavern
│   ├── ListCharactersCommand.cs  # /酒馆列表 - list characters
│   ├── LoadCharacterCommand.cs   # /酒馆加载 - load character
│   ├── TavernChatCommand.cs      # /酒馆对话 - character RP
│   ├── CheckMonitorCommand.cs    # /监测状态 - debug monitoring
│   ├── SpecialFocusCommand.cs    # /特别关注 - admin focus list
│   ├── ReloadPersonalityConfigCommand.cs
│   ├── ReloadTavernConfigCommand.cs
│   └── ReloadDialogueConfigCommand.cs
└── Handlers/
    ├── IMessageHandler.cs        # Message handler interface
    └── DigimonMessageHandler.cs  # Main message processor
```

**When adding a new command:**
1. Create class implementing `ICommand` in `Commands/`
2. Register in `Program.cs` `CommandRegistry` setup
3. Follow naming: `[Name]Command.cs`
4. Use constructor injection for dependencies
5. Return `CommandResult` with appropriate flags

### DigimonBot.Host (`src/DigimonBot.Host/`)
**Purpose:** Application composition and infrastructure

```
DigimonBot.Host/
├── DigimonBot.Host.csproj        # References all other projects
├── Program.cs                    # Entry point, DI registration
├── BotService.cs                 # WebSocket QQ bot service
└── Configs/
    ├── AppSettings.cs            # Configuration models and presets
    └── GroupModeConfig.cs        # Group mode implementation
```

**Service Registration in Program.cs (line 33-330):**
- Lines 47-67: Repository registrations
- Lines 69-77: DigimonManager registration
- Lines 79-84: Core services (EvolutionEngine, EmotionTracker)
- Lines 86-106: AI client registration with factory
- Lines 108-159: Supporting services (Vision, Tavern, etc.)
- Lines 161-174: Battle service
- Lines 177-323: Command registry with all commands
- Line 329: Hosted service (BotService)

## Test Project Structure

### DigimonBot.Core.Tests (`tests/DigimonBot.Core.Tests/`)
```
Commands/
    JrrpCommandTests.cs           # Command unit tests
Models/
    DigimonStageTests.cs          # Model validation tests
    EmotionValuesTests.cs         # Emotion calculation tests
Services/
    EmotionTrackerTests.cs        # Service logic tests
    EvolutionEngineTests.cs       # Evolution algorithm tests
```

### DigimonBot.AI.Tests (`tests/DigimonBot.AI.Tests/`)
```
Services/
    AIClientFactoryTests.cs       # Factory pattern tests
    PersonalityEngineTests.cs     # Personality prompt tests
```

### IntegrationTest (`tests/IntegrationTest/`)
```
Program.cs                      # Manual integration testing
```

## Key File Locations

**Entry Points:**
- `src/DigimonBot.Host/Program.cs` - Application startup
- `src/DigimonBot.Host/BotService.cs` - QQ connection handler

**Configuration:**
- `src/DigimonBot.Host/Configs/AppSettings.cs` - Configuration models
- `appsettings.json` (runtime) - Configuration values

**Core Domain:**
- `src/DigimonBot.Core/Models/DigimonDefinition.cs` - Digimon data model
- `src/DigimonBot.Core/Services/IDigimonManager.cs` - Core aggregate interface
- `src/DigimonBot.Core/Services/IEvolutionEngine.cs` - Evolution logic

**AI Integration:**
- `src/DigimonBot.AI/Services/IAIClient.cs` - AI abstraction
- `src/DigimonBot.AI/Services/AIClientFactory.cs` - Provider factory
- `src/DigimonBot.AI/Services/DeepSeekClient.cs` - Primary AI implementation

**Data Access:**
- `src/DigimonBot.Data/Database/DatabaseInitializer.cs` - Schema management
- `src/DigimonBot.Data/Repositories/PersistentDigimonManager.cs` - State coordinator

**Message Processing:**
- `src/DigimonBot.Messaging/Handlers/DigimonMessageHandler.cs` - Main processor
- `src/DigimonBot.Messaging/Commands/ICommand.cs` - Command contract

## Naming Conventions

**Files:**
- PascalCase for all files: `DigimonMessageHandler.cs`, `StatusCommand.cs`
- Suffix indicates type: `*Service.cs`, `*Command.cs`, `*Repository.cs`, `*Handler.cs`

**Interfaces:**
- Prefix with `I`: `ICommand`, `IAIClient`, `IDigimonManager`

**Classes:**
- Implementations match interface minus `I`: `Command` → `StatusCommand`

**Directories:**
- PascalCase matching project/namespace
- Plural for collections: `Commands/`, `Services/`, `Models/`

## Where to Add New Code

**New Digimon Feature:**
1. Add model to `src/DigimonBot.Core/Models/`
2. Add service interface to `src/DigimonBot.Core/Services/`
3. Implement in `src/DigimonBot.AI/Services/` or `src/DigimonBot.Data/Services/`
4. Add command in `src/DigimonBot.Messaging/Commands/`
5. Register in `src/DigimonBot.Host/Program.cs`

**New AI Provider:**
1. Add to `AIProvider` enum in `AIClientFactory.cs`
2. Create client class in `src/DigimonBot.AI/Services/`
3. Add factory method in `AIClientFactory`
4. Update `AppSettings.cs` parsing

**New Command:**
1. Create `src/DigimonBot.Messaging/Commands/[Name]Command.cs`
2. Implement `ICommand` interface
3. Register in `Program.cs` `CommandRegistry` setup (around line 177+)

**New Repository:**
1. Define interface in `src/DigimonBot.Data/Repositories/I[Entity]Repository.cs`
2. Add SQLite implementation in `src/DigimonBot.Data/Repositories/Sqlite/`
3. Add table creation in `DatabaseInitializer.cs`
4. Register in `Program.cs` DI container

## Data Files (Runtime)

**Configuration Data:**
- `Data/digimon_database.json` - Digimon definitions and evolution paths
- `Data/items_database.json` - Shop item definitions
- `Data/digimon_personalities.json` - Personality configurations
- `Data/digimon_dialogue_config.json` - Dialogue templates
- `Data/tavern_config.json` - Tavern/character RP settings

**Runtime Database:**
- `Data/bot_data.db` - SQLite database with user states

**Logs:**
- Console output (no file logging configured)

---

*Structure analysis: 2026-04-06*
