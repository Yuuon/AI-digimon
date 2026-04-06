# Technology Stack

**Analysis Date:** 2025-04-06

## Languages

**Primary:**
- **C# 12** - .NET 8.0 codebase with implicit usings and nullable reference types enabled across all projects

**Configuration & Data:**
- **JSON** - All configuration files, databases, and API communication use JSON format

## Runtime

**Environment:**
- **.NET 8.0 (net8.0)** - Target framework for all projects
- **Console Application** - `DigimonBot.Host` runs as executable (`<OutputType>Exe</OutputType>`)

**Package Manager:**
- **NuGet** - Standard .NET package management
- Lockfile: Present in standard .NET ecosystem via `obj/project.assets.json`

## Frameworks

**Core Framework:**
- **Microsoft .NET 8.0** - Base runtime and SDK

**Dependency Injection & Hosting:**
- **Microsoft.Extensions.Hosting** 8.0.0 - Application bootstrap and lifecycle management
- **Microsoft.Extensions.DependencyInjection** 8.0.0 - DI container configuration
- **Microsoft.Extensions.Configuration** 8.0.0 - Configuration management
- **Microsoft.Extensions.Configuration.Json** 8.0.0 - JSON configuration provider

**Logging:**
- **Microsoft.Extensions.Logging.Console** 8.0.0 - Console logging provider
- **Microsoft.Extensions.Logging.Abstractions** 8.0.0 - Logging abstractions (used across Core, AI, Messaging projects)

**HTTP Communication:**
- **Microsoft.Extensions.Http** 8.0.0 - HTTP client factory and typed clients
- **System.Net.WebSockets** - Built-in WebSocket support for NapCatQQ integration
- **System.Net.Http** - Built-in HTTP client for API calls

**Testing:**
- **xUnit** 2.6.2 - Primary testing framework
- **xunit.runner.visualstudio** 2.5.4 - Visual Studio test runner
- **Microsoft.NET.Test.Sdk** 17.8.0 - .NET testing SDK
- **Moq** 4.20.70 - Mocking framework for unit tests
- **coverlet.collector** 6.0.0 - Code coverage collection

## Key Dependencies

**Database & Data Access:**
- **Microsoft.Data.Sqlite** 8.0.0 - SQLite database provider for .NET
- **Dapper** 2.1.28 - Lightweight ORM for SQL query execution and result mapping

**Image Processing:**
- **SixLabors.ImageSharp** 3.1.4 (Host) / 3.1.3 (AI) - Modern cross-platform 2D graphics library
  - Used for: Image compression, resizing, format conversion (JPEG encoding)
  - Features: Binary search compression algorithm to achieve target file sizes

**Serialization:**
- **System.Text.Json** 8.0.0 - High-performance JSON serialization (used across all layers)
  - Used for: API communication, configuration files, database JSON stores

**Desktop Tool (WPF):**
- **EvolutionEditor** - WPF application for Digimon evolution tree editing
  - Targets .NET 8.0 with Windows Desktop runtime

## Project Structure

| Project | Purpose | Key Dependencies |
|---------|---------|------------------|
| `DigimonBot.Host` | Entry point, WebSocket/HTTP handling, DI registration | All above packages, references all other projects |
| `DigimonBot.Core` | Domain models, interfaces, core business logic | Logging.Abstractions only (minimal dependencies) |
| `DigimonBot.AI` | AI service implementations (DeepSeek, GLM) | Http, Logging.Abstractions, System.Text.Json, ImageSharp |
| `DigimonBot.Data` | Data access, repositories, SQLite implementation | Sqlite, Dapper, System.Text.Json |
| `DigimonBot.Messaging` | Message handlers, command system | Logging.Abstractions only |
| `DigimonBot.Core.Tests` | Unit tests for Core | xUnit, Moq |
| `DigimonBot.AI.Tests` | Unit tests for AI services | xUnit, Moq |
| `IntegrationTest` | Integration test suite | xUnit, custom HTTP factory |
| `EvolutionEditor` | WPF tool for editing Digimon data | ImageSharp, System.Text.Json |

## Configuration

**Configuration Sources (in order of precedence):**
1. **Environment Variables** - Override any configuration value
2. **appsettings.json** - Primary configuration file (required)
3. **Code defaults** - Fallback values in `AppSettings.cs`

**Key Configuration Files:**
- `src/DigimonBot.Host/Configs/appsettings.example.json` - Template showing all options
- `Data/digimon_database.json` - Digimon definitions and evolution trees
- `Data/items_database.json` - Item shop definitions
- `Data/digimon_personalities.json` - Personality configurations per Digimon
- `Data/digimon_dialogue_config.json` - Dialogue templates and patterns
- `Data/tavern_config.json` - Tavern/roleplay mode configuration

**Runtime Settings:**
- `<Nullable>enable</Nullable>` - Nullable reference types enabled
- `<ImplicitUsings>enable</ImplicitUsings>` - Implicit global usings enabled

## Platform Requirements

**Development:**
- .NET 8.0 SDK or later
- Visual Studio 2022 17.8+ or VS Code with C# Dev Kit
- Windows (for WPF EvolutionEditor tool)

**Production:**
- .NET 8.0 Runtime
- SQLite support (included via Microsoft.Data.Sqlite)
- Network access to AI APIs and NapCatQQ
- curl command-line tool (used for Catbox image uploads)

**External Dependencies:**
- **NapCatQQ** - QQ Bot framework (separate process, connects via WebSocket)
- **SQLite** - File-based database (no separate server needed)

## Build Configuration

**Build Commands:**
```bash
# Restore dependencies
dotnet restore

# Build solution
dotnet build

# Run tests
dotnet test

# Run application
dotnet run --project src/DigimonBot.Host

# Publish for production
dotnet publish src/DigimonBot.Host -c Release -o ./publish
```

**Configuration Pattern:**
The application uses strongly-typed configuration with `IOptions<AppSettings>` pattern:
- `AppSettings.cs` - Configuration classes with nested structures
- Configuration validated at startup
- Supports hot-reload for some configuration sections

---

*Stack analysis: 2025-04-06*
