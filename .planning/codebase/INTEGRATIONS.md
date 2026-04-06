# External Integrations

**Analysis Date:** 2025-04-06

## QQ Bot Integration (NapCatQQ)

**Primary Bot Framework:**
- **NapCatQQ** - OneBot 11 protocol compatible QQ Bot framework
- Connection Mode: WebSocket Reverse (Bot connects to NapCat as client)
- Implementation: `src/DigimonBot.Host/BotService.cs`

**Connection Configuration:**
```json
{
  "QQBot": {
    "NapCat": {
      "BotQQ": 123456789,
      "ConnectionType": "WebSocketReverse",
      "WebSocketHost": "127.0.0.1",
      "WebSocketPort": 5140,
      "HttpApiUrl": "http://127.0.0.1:3000",
      "GroupDigimonMode": "Separate"
    }
  }
}
```

**Protocol Details:**
- WebSocket URL: `ws://{host}:{port}/onebot`
- Message receiving: WebSocket events (OneBot format)
- Message sending: HTTP POST to `/send_private_msg` and `/send_group_msg`
- Image handling: NapCat `get_image` API for resolving file IDs to URLs

**Message Types Supported:**
- Private messages (1-on-1 chat)
- Group messages with @ mention detection
- Image extraction and processing
- Message mention parsing (`at` segments)

## AI Service Integrations

**Supported AI Providers:**

### 1. DeepSeek (Default)
- **Base URL:** `https://api.deepseek.com`
- **Models:** `deepseek-chat`, `deepseek-reasoner`
- **Format:** OpenAI-compatible API (`/v1/chat/completions`)
- **Implementation:** `src/DigimonBot.AI/Services/DeepSeekClient.cs`
- **Features:**
  - Chat completions with conversation history
  - Emotion analysis with structured JSON output
  - Token usage tracking

### 2. 智谱AI (GLM)
- **Base URL:** `https://open.bigmodel.cn/api/paas/v4`
- **Models:** `glm-4`, `glm-4-flash`, `glm-4v` (vision)
- **Implementation:** `src/DigimonBot.AI/Services/GLMClient.cs`
- **Features:**
  - GLM-4 Chinese language model
  - GLM-4v multimodal (vision) support
  - Automatic JSON extraction from responses
  - Message role handling for GLM API requirements

### 3. OpenAI-Compatible
- **Base URL:** Configurable (e.g., `https://api.openai.com/v1`)
- **Models:** Any OpenAI-compatible model
- **Implementation:** Uses `DeepSeekClient` (OpenAI format compatible)
- **Use Cases:** SiliconFlow, Azure OpenAI, self-hosted models

### 4. Custom Provider
- **Requirements:** Must provide BaseUrl
- **Format:** OpenAI API compatible
- **Configuration:** Set `Provider: custom` in appsettings

**AI Configuration Options:**
```json
{
  "AI": {
    "Provider": "deepseek",
    "ApiKey": "your-api-key",
    "Model": "deepseek-chat",
    "BaseUrl": null,
    "TimeoutSeconds": 60,
    "Temperature": 0.8,
    "MaxTokens": 1000,
    "VisionModel": {
      "BaseUrl": "https://open.bigmodel.cn/api/paas/v4/chat/completions",
      "Model": "glm-4v",
      "ApiKey": null
    }
  }
}
```

**Vision/Image Analysis:**
- Provider: GLM-4v (configurable)
- Process: Download → Compress → Upload to Catbox → Send to vision API
- Fallback: Base64 encoding for small images
- Implementation: `src/DigimonBot.Host/Services/VisionService.cs`

## Data Storage

### Primary Database: SQLite
- **Provider:** Microsoft.Data.Sqlite 8.0.0
- **ORM:** Dapper 2.1.28 (micro-ORM for query execution)
- **Connection String:** `Data Source=Data/bot_data.db`
- **Implementation:** `src/DigimonBot.Data/Database/DatabaseInitializer.cs`

**Database Schema:**
| Table | Purpose |
|-------|---------|
| `UserDigimonState` | User's current Digimon, emotions, evolution state |
| `UserEconomy` | Gold/currency tracking |
| `UserInventory` | Items owned by users |
| `PurchaseRecord` | Shop purchase history |
| `CheckInRecord` | Daily check-in tracking |

### JSON Data Stores
- **Digimon Database:** `Data/digimon_database.json` - Digimon definitions, evolution trees
- **Items Database:** `Data/items_database.json` - Shop items and effects
- **Personality Config:** `Data/digimon_personalities.json` - Per-Digimon personality settings
- **Dialogue Config:** `Data/digimon_dialogue_config.json` - Dialogue templates
- **Tavern Config:** `Data/tavern_config.json` - Roleplay mode settings
- **Character Cards:** `Data/Characters/*.json` - TavernAI format character definitions

## Image Processing & External Services

### Image Processing (ImageSharp)
- **Library:** SixLabors.ImageSharp 3.1.4
- **Capabilities:**
  - JPEG compression with quality optimization (binary search algorithm)
  - Image resizing (max 768x768 for upload, 1024x1024 for vision)
  - Base64 encoding for AI transmission
  - File size targeting (<200KB for AI, optimized compression)

### External Image Hosting (Catbox)
- **Service:** litterbox.catbox.moe (temporary 24h hosting)
- **Purpose:** Convert internal NapCat image URLs to public URLs for AI vision APIs
- **Method:** curl subprocess call with form upload
- **Timeout:** 60 seconds
- **Implementation:** `VisionService.UploadToCatboxAsync()`

## WebSocket Communication

**NapCatQQ WebSocket Connection:**
- **Type:** Client WebSocket (`System.Net.WebSockets.ClientWebSocket`)
- **Protocol:** OneBot 11 event reporting
- **Features:**
  - Auto-reconnection with configurable interval (default 10s)
  - Bearer token authentication
  - Message fragmentation handling
  - Heartbeat/meta event handling

**WebSocket Message Flow:**
1. Connect to NapCat WebSocket endpoint
2. Receive OneBot events (messages, notices, meta events)
3. Parse JSON events to `OneBotEvent` model
4. Route to message handler
5. Send responses via HTTP API

## HTTP API Clients

**HttpClient Usage:**
- **Factory:** `Microsoft.Extensions.Http` (IHttpClientFactory)
- **Named Clients:** Per-AI-provider named instances
- **Timeout:** Configurable per provider (default 60s)

**API Endpoints Called:**
| Endpoint | Purpose | Location |
|----------|---------|----------|
| `{NapCat}/send_private_msg` | Send private QQ messages | `BotService.SendPrivateMessageAsync()` |
| `{NapCat}/send_group_msg` | Send group QQ messages | `BotService.SendGroupMessageAsync()` |
| `{NapCat}/get_image` | Resolve image file to URL | `BotService.ResolveImageUrlAsync()` |
| `{AI}/chat/completions` | AI chat completions | `DeepSeekClient`, `GLMClient` |
| `https://litterbox.catbox.moe/...` | Image upload for vision | `VisionService.UploadToCatboxAsync()` |

## Authentication & Security

**API Key Management:**
- AI API keys stored in appsettings.json or environment variables
- Environment variable format: `AI__ApiKey`
- No encryption at rest for configuration (standard .NET practice)
- Bearer token authentication for all AI APIs

**QQ Bot Authentication:**
- Access tokens for NapCat HTTP API (optional)
- WebSocket authorization headers (optional)
- Bot QQ number configured for self-mention detection

## Configuration Environment Variables

**Required for Production:**
```bash
AI__ApiKey                    # AI service API key
AI__VisionModel__ApiKey       # Vision model API key (if different)
```

**Optional Overrides:**
```bash
AI__Provider                  # deepseek, glm, openai, custom
AI__Model                     # Model name override
AI__BaseUrl                   # Custom API endpoint
AI__Temperature               # Creativity parameter
AI__MaxTokens                 # Response length limit
```

## Monitoring & Observability

**Logging:**
- Provider: Microsoft.Extensions.Logging.Console
- Minimum Level: Information (configurable)
- Structured logging with categories
- Log prefixes for subsystem identification (e.g., `[VisionService]`, `[自主发言]`)

**No External Monitoring:**
- No Application Insights, Sentry, or similar APM tools
- No error tracking services
- No metrics collection
- Logging to console only

## CI/CD & Deployment

**Build System:**
- Standard `dotnet` CLI build
- No containerization (Dockerfile not present)
- No CI/CD configuration files detected

**Deployment:**
- Self-contained or framework-dependent deployment
- SQLite database file needs persistent storage
- Configuration via appsettings.json or environment variables

## Webhooks & Callbacks

**Incoming:**
- NapCatQQ WebSocket events (real-time message push)
- OneBot protocol meta events (heartbeat, lifecycle)

**Outgoing:**
- HTTP POST to NapCat API for message sending
- HTTP POST to AI APIs for completions
- HTTP POST to Catbox for image uploads

## Integration Health & Failure Handling

**Auto-Reconnection:**
- WebSocket: Automatic reconnection with exponential backoff
- HTTP: Standard retry logic via HttpClient
- SQLite: Connection pooling with automatic reconnection

**Graceful Degradation:**
- Vision service: Falls back to base64 if Catbox upload fails
- AI service: Returns error messages to users on API failure
- Emotion analysis: Non-blocking, failures don't break conversation flow

---

*Integration audit: 2025-04-06*
