using DigimonBot.Host.Configs;
using DigimonBot.Core.Modules;
using DigimonBot.Core.Services;
using DigimonBot.Host.Modules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DigimonBot.Host;

/// <summary>
/// NapCatQQ Bot 服务 - 仅负责协议层（WebSocket/HTTP）和消息路由。
/// 所有具体业务逻辑由模块(IModule)处理。
/// </summary>
public class BotService : BackgroundService, Core.Services.IImageUrlResolver
{
    private readonly ILogger<BotService> _logger;
    private readonly AppSettings _settings;
    private readonly ModuleManager _moduleManager;
    private readonly IMessageHistoryService _messageHistory;
    private readonly IServiceProvider _serviceProvider;
    private readonly HttpClient _httpClient;
    private ClientWebSocket? _webSocket;
    private readonly CancellationTokenSource _reconnectCts = new();
    private bool _isRunning;
    private long _botQQ;

    public BotService(
        ILogger<BotService> logger,
        IOptions<AppSettings> settings,
        ModuleManager moduleManager,
        IMessageHistoryService messageHistory,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _settings = settings.Value;
        _moduleManager = moduleManager;
        _messageHistory = messageHistory;
        _serviceProvider = serviceProvider;
        _httpClient = new HttpClient();
        
        // 从配置读取 Bot QQ 号
        _botQQ = _settings.QQBot.NapCat.BotQQ;
        if (_botQQ <= 0)
        {
            _logger.LogWarning("⚠️ BotQQ 未配置！请在 appsettings.json 中设置 QQBot:NapCat:BotQQ");
        }
        else
        {
            _logger.LogInformation("✅ Bot QQ 号已配置: {BotQQ}", _botQQ);
        }
        
        // 设置HTTP API访问令牌
        if (!string.IsNullOrEmpty(_settings.QQBot.NapCat.HttpAccessToken))
        {
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_settings.QQBot.NapCat.HttpAccessToken}");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting Digimon Bot with NapCatQQ...");
        _logger.LogInformation("Config: AI Provider={Provider}, Model={Model}", 
            _settings.AI.Provider, _settings.AI.Model);
        
        _isRunning = true;

        // Initialize module context and load modules
        var context = new BotModuleContext(
            SendGroupMessageAsync,
            SendPrivateMessageAsync,
            _serviceProvider);
        _moduleManager.SetContext(context);
        await LoadModulesAsync();

        try
        {
            await RunBotAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bot service failed");
            throw;
        }
    }

    /// <summary>
    /// Load all modules from DI container.
    /// </summary>
    private async Task LoadModulesAsync()
    {
        var modules = _serviceProvider.GetServices<IModule>();
        foreach (var module in modules)
        {
            await _moduleManager.LoadModuleAsync(module);
        }
        _logger.LogInformation("Loaded {Count} modules", _moduleManager.Modules.Count);
    }

    private async Task RunBotAsync(CancellationToken cancellationToken)
    {
        var config = _settings.QQBot.NapCat;
        
        if (config.ConnectionType.Equals("WebSocketReverse", StringComparison.OrdinalIgnoreCase))
        {
            await RunWebSocketReverseAsync(cancellationToken);
        }
        else if (config.ConnectionType.Equals("HTTP", StringComparison.OrdinalIgnoreCase))
        {
            await RunHttpModeAsync(cancellationToken);
        }
        else
        {
            throw new NotSupportedException($"Connection type '{config.ConnectionType}' is not supported");
        }
    }

    /// <summary>
    /// WebSocket 反向连接模式 - 作为服务端接收 NapCatQQ 的连接
    /// </summary>
    private async Task RunWebSocketReverseAsync(CancellationToken cancellationToken)
    {
        var config = _settings.QQBot.NapCat;
        var url = $"ws://{config.WebSocketHost}:{config.WebSocketPort}{config.PostPath}";
        
        _logger.LogInformation("Connecting to NapCatQQ WebSocket at {Url}...", url);

        while (_isRunning && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                _webSocket = new ClientWebSocket();
                
                // 设置访问令牌
                if (!string.IsNullOrEmpty(config.AccessToken))
                {
                    _webSocket.Options.SetRequestHeader("Authorization", $"Bearer {config.AccessToken}");
                }

                await _webSocket.ConnectAsync(new Uri(url), cancellationToken);
                _logger.LogInformation("Connected to NapCatQQ WebSocket successfully!");

                await ReceiveMessagesAsync(_webSocket, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WebSocket connection error");
            }
            finally
            {
                _webSocket?.Dispose();
                _webSocket = null;
            }

            if (_isRunning && config.AutoReconnect && !cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Reconnecting in {Seconds} seconds...", config.ReconnectInterval);
                await Task.Delay(TimeSpan.FromSeconds(config.ReconnectInterval), cancellationToken);
            }
        }
    }

    /// <summary>
    /// HTTP 模式 - 轮询或监听 HTTP 事件上报
    /// </summary>
    private async Task RunHttpModeAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP mode is not fully implemented. Please use WebSocketReverse mode.");
        _logger.LogInformation("Waiting for cancellation...");
        
        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // 正常退出
        }
    }

    /// <summary>
    /// 接收 WebSocket 消息
    /// </summary>
    private async Task ReceiveMessagesAsync(ClientWebSocket webSocket, CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        var messageBuilder = new StringBuilder();

        while (webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                messageBuilder.Clear();
                WebSocketReceiveResult result;

                do
                {
                    result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                    
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", cancellationToken);
                        _logger.LogInformation("WebSocket closed by server");
                        return;
                    }

                    var chunk = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    messageBuilder.Append(chunk);
                }
                while (!result.EndOfMessage);

                var message = messageBuilder.ToString();
                if (!string.IsNullOrWhiteSpace(message))
                {
                    _ = Task.Run(() => HandleNapCatMessageAsync(message), cancellationToken);
                }
            }
            catch (WebSocketException ex)
            {
                _logger.LogError(ex, "WebSocket error");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error receiving message");
            }
        }
    }

    /// <summary>
    /// 处理 NapCatQQ 消息
    /// </summary>
    private async Task HandleNapCatMessageAsync(string jsonMessage)
    {
        try
        {
            var eventData = JsonSerializer.Deserialize<OneBotEvent>(jsonMessage);
            if (eventData == null) 
            {
                _logger.LogWarning("消息反序列化失败");
                return;
            }

            // 处理消息事件
            if (eventData.PostType == "message")
            {
                await HandleMessageEventAsync(eventData);
            }
            // 非消息事件分发给模块
            else
            {
                var evt = new ModuleEvent
                {
                    PostType = eventData.PostType,
                    EventType = eventData.MetaEventType ?? eventData.NoticeType,
                    RawJson = jsonMessage
                };
                await _moduleManager.DispatchEventAsync(evt);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling message: {Message}", jsonMessage);
        }
    }

    /// <summary>
    /// 处理消息事件（私聊/群聊）- 仅解析协议，然后委托给模块处理
    /// </summary>
    private async Task HandleMessageEventAsync(OneBotEvent eventData)
    {
        var messageType = eventData.MessageType;
        var userId = eventData.UserId?.ToString() ?? "unknown";
        
        // 更新 Bot QQ 号
        if (eventData.SelfId > 0 && _botQQ == 0)
        {
            _botQQ = eventData.SelfId;
            _logger.LogInformation("Bot QQ号已设置: {BotQQ}", _botQQ);
        }
        
        // 提取消息内容
        var content = ExtractMessageContent(eventData.Message);
        
        if (string.IsNullOrWhiteSpace(content)) 
        {
            return;
        }

        // 获取发送者昵称
        var userName = eventData.Sender?.Nickname ?? userId;

        // 解析消息中的@提及（排除Bot自己）
        var mentionedUserIds = ExtractMentionedUsers(eventData.Message, _botQQ);
        
        // 提取图片信息
        var (imageUrl, imageFile) = ExtractImageInfo(eventData.Message);
        
        // 记录消息历史
        _messageHistory.AddMessage(userId, eventData.GroupId ?? 0, new MessageEntry
        {
            Content = content,
            Type = string.IsNullOrEmpty(imageUrl) && string.IsNullOrEmpty(imageFile) ? "text" : "image",
            ImageUrl = imageUrl,
            ImageFile = imageFile,
            Timestamp = DateTime.Now,
            IsFromBot = false,
            RawData = eventData.Message
        });
        
        // 群聊特殊处理：检查是否@Bot或以/开头
        bool isAtBot = false;
        bool isCommand = false;
        bool shouldDispatchToCommandModule = true;
        
        if (messageType == "group")
        {
            try
            {
                isAtBot = IsAtBot(eventData.Message, _botQQ);
                isCommand = content.StartsWith('/') || content.StartsWith('！') || content.StartsWith('!');
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "检查触发条件时出错");
                return;
            }
            
            // For non-@bot and non-command group messages, still dispatch to observer modules
            // (TavernModule observes all group messages) but don't route to command handler
            if (!isAtBot && !isCommand)
            {
                shouldDispatchToCommandModule = false;
            }

            // 去除@的文本
            if (isAtBot)
            {
                content = RemoveAtContent(content, _botQQ);
            }
        }

        // Build module message
        var moduleMessage = new ModuleMessage
        {
            UserId = userId,
            OriginalUserId = userId,
            UserName = userName,
            Content = content,
            GroupId = eventData.GroupId,
            IsGroupMessage = messageType == "group",
            IsMentioned = isAtBot,
            Timestamp = DateTime.Now,
            MentionedUserIds = mentionedUserIds,
            RawMessage = eventData.Message
        };

        _logger.LogInformation("[{Source}] {User}: {Content}", 
            moduleMessage.IsGroupMessage ? $"Group {moduleMessage.GroupId}" : "Private",
            moduleMessage.UserName, 
            moduleMessage.Content);

        // Dispatch message to all modules
        try
        {
            var result = await _moduleManager.DispatchMessageAsync(moduleMessage);

            // Only send response if shouldDispatchToCommandModule is true
            // (observer modules like TavernModule never set Handled=true for non-targeted messages)
            if (!shouldDispatchToCommandModule)
                return;
            
            if (result.Handled && !string.IsNullOrEmpty(result.Response))
            {
                _logger.LogInformation("Sending response: {Response}", result.Response);
                
                if (moduleMessage.IsGroupMessage && moduleMessage.GroupId.HasValue)
                {
                    await SendGroupMessageAsync(moduleMessage.GroupId.Value, result.Response);
                }
                else
                {
                    await SendPrivateMessageAsync(long.Parse(userId), result.Response);
                }

                // Send additional message parts
                foreach (var part in result.AdditionalMessages)
                {
                    await Task.Delay(500);
                    if (moduleMessage.IsGroupMessage && moduleMessage.GroupId.HasValue)
                    {
                        await SendGroupMessageAsync(moduleMessage.GroupId.Value, part);
                    }
                    else
                    {
                        await SendPrivateMessageAsync(long.Parse(userId), part);
                    }
                }

                // Send evolution message if occurred
                if (result.EvolutionOccurred && !string.IsNullOrEmpty(result.EvolutionMessage))
                {
                    await Task.Delay(500);
                    if (moduleMessage.IsGroupMessage && moduleMessage.GroupId.HasValue)
                    {
                        await SendGroupMessageAsync(moduleMessage.GroupId.Value, result.EvolutionMessage);
                    }
                    else
                    {
                        await SendPrivateMessageAsync(long.Parse(userId), result.EvolutionMessage);
                    }
                }
            }
            else if (shouldDispatchToCommandModule)
            {
                _logger.LogWarning("Message not handled or empty response");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message");
        }
    }

    /// <summary>
    /// 提取消息内容（纯文本）
    /// </summary>
    private string ExtractMessageContent(object? message)
    {
        if (message == null) return string.Empty;
        
        // 如果是字符串，直接返回
        if (message is string str) return str;

        // 如果是消息段数组，提取文本
        if (message is JsonElement element)
        {
            // _logger.LogDebug("消息类型: {ValueKind}", element.ValueKind);
            
            // 尝试直接作为字符串解析
            if (element.ValueKind == JsonValueKind.String)
            {
                return element.GetString() ?? string.Empty;
            }

            // 解析消息段数组
            if (element.ValueKind == JsonValueKind.Array)
            {
                var texts = new List<string>();
                foreach (var segment in element.EnumerateArray())
                {
                    if (segment.TryGetProperty("type", out var typeProp))
                    {
                        var segType = typeProp.GetString();
                        _logger.LogDebug("消息段类型: {Type}", segType);
                        
                        if (segType == "text")
                        {
                            if (segment.TryGetProperty("data", out var dataProp) &&
                                dataProp.TryGetProperty("text", out var textProp))
                            {
                                var txt = textProp.GetString() ?? "";
                                texts.Add(txt);
                                _logger.LogDebug("提取文本: {Text}", txt);
                            }
                        }
                    }
                }
                return string.Join("", texts);
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// 提取消息中@的所有用户ID（排除Bot自己）
    /// </summary>
    private List<string> ExtractMentionedUsers(object? message, long botQQ)
    {
        var result = new List<string>();
        if (message == null) return result;

        if (message is JsonElement element && element.ValueKind == JsonValueKind.Array)
        {
            foreach (var segment in element.EnumerateArray())
            {
                if (segment.TryGetProperty("type", out var typeProp) && 
                    typeProp.GetString() == "at")
                {
                    // 获取被@的QQ号
                    long atQQ = 0;
                    
                    // 先尝试直接读取 qq 属性
                    if (segment.TryGetProperty("qq", out var qqProp))
                    {
                        if (qqProp.ValueKind == JsonValueKind.Number)
                        {
                            atQQ = qqProp.GetInt64();
                        }
                        else if (qqProp.ValueKind == JsonValueKind.String)
                        {
                            var qqStr = qqProp.GetString();
                            long.TryParse(qqStr, out atQQ);
                        }
                    }
                    // 再尝试读取 data.qq 嵌套属性 (NapCat标准格式)
                    else if (segment.TryGetProperty("data", out var dataProp) && 
                             dataProp.ValueKind == JsonValueKind.Object)
                    {
                        if (dataProp.TryGetProperty("qq", out var nestedQqProp))
                        {
                            if (nestedQqProp.ValueKind == JsonValueKind.Number)
                            {
                                atQQ = nestedQqProp.GetInt64();
                            }
                            else if (nestedQqProp.ValueKind == JsonValueKind.String)
                            {
                                var qqStr = nestedQqProp.GetString();
                                long.TryParse(qqStr, out atQQ);
                            }
                        }
                    }
                    
                    // 排除Bot自己
                    if (atQQ > 0 && atQQ != botQQ)
                    {
                        result.Add(atQQ.ToString());
                    }
                }
            }
        }

        return result;
    }

    /// <summary>
    /// 提取消息中的图片信息
    /// </summary>
    private (string? Url, string? File) ExtractImageInfo(object? message)
    {
        if (message == null) return (null, null);

        if (message is JsonElement element && element.ValueKind == JsonValueKind.Array)
        {
            _logger.LogDebug("[ExtractImageInfo] 开始解析消息数组，共{Count}个segment", element.GetArrayLength());
            
            foreach (var segment in element.EnumerateArray())
            {
                if (segment.TryGetProperty("type", out var typeProp))
                {
                    var segType = typeProp.GetString();
                    _logger.LogDebug("[ExtractImageInfo] 找到segment类型: {Type}", segType);
                    
                    if (segType == "image")
                    {
                        // 尝试获取图片URL (NapCat格式: data.url)
                        if (segment.TryGetProperty("data", out var dataProp) && 
                            dataProp.ValueKind == JsonValueKind.Object)
                        {
                            string? url = null;
                            string? file = null;
                            
                            // 获取 url 字段
                            if (dataProp.TryGetProperty("url", out var urlProp))
                            {
                                url = urlProp.GetString();
                                _logger.LogDebug("[ExtractImageInfo] 找到url: {Url}", url);
                            }
                            
                            // 获取 file 字段（用于后续调用get_image API）
                            if (dataProp.TryGetProperty("file", out var fileProp))
                            {
                                file = fileProp.GetString();
                                _logger.LogInformation("[ExtractImageInfo] 找到图片file: {File}", file);
                            }
                            
                            // 也尝试从path字段获取
                            if (string.IsNullOrEmpty(file) && dataProp.TryGetProperty("path", out var pathProp))
                            {
                                file = pathProp.GetString();
                                _logger.LogInformation("[ExtractImageInfo] 从path找到file: {File}", file);
                            }
                            
                            if (!string.IsNullOrEmpty(url) || !string.IsNullOrEmpty(file))
                            {
                                _logger.LogInformation("[ExtractImageInfo] 成功提取图片信息: Url={Url}, File={File}", url, file);
                                return (url, file);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("[ExtractImageInfo] image segment没有data属性");
                        }
                    }
                }
            }
        }
        else
        {
            _logger.LogDebug("[ExtractImageInfo] 消息不是数组类型或为空: {Type}", message?.GetType()?.Name);
        }

        return (null, null);
    }

    /// <summary>
    /// 获取图片的真实下载URL（调用NapCat get_image API）
    /// </summary>
    public async Task<string?> ResolveImageUrlAsync(string file)
    {
        try
        {
            var url = $"{_settings.QQBot.NapCat.HttpApiUrl}/get_image";
            var payload = new { file = file };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            _logger.LogInformation("[ResolveImageUrl] 调用get_image API: File={File}, Url={ApiUrl}", file, url);
            
            var response = await _httpClient.PostAsync(url, content);
            var responseJson = await response.Content.ReadAsStringAsync();
            
            _logger.LogInformation("[ResolveImageUrl] API响应: Status={Status}, Body={Body}", 
                response.StatusCode, responseJson);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("[ResolveImageUrl] get_image API调用失败: {Status}", response.StatusCode);
                return null;
            }
            
            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;
            
            // 尝试获取 data.url
            if (root.TryGetProperty("data", out var data))
            {
                _logger.LogDebug("[ResolveImageUrl] 找到data字段: {Data}", data);
                
                if (data.TryGetProperty("url", out var urlProp))
                {
                    var result = urlProp.GetString();
                    _logger.LogInformation("[ResolveImageUrl] 成功获取图片URL: {Url}", result);
                    return result;
                }
                else
                {
                    _logger.LogWarning("[ResolveImageUrl] data中未找到url字段");
                }
            }
            else
            {
                _logger.LogWarning("[ResolveImageUrl] 响应中未找到data字段");
            }
            
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ResolveImageUrl] 获取图片URL失败");
            return null;
        }
    }

    /// <summary>
    /// 检查消息中是否@了Bot
    /// </summary>
    private bool IsAtBot(object? message, long botQQ)
    {
        if (message == null) return false;

        if (message is JsonElement element && element.ValueKind == JsonValueKind.Array)
        {
            
            foreach (var segment in element.EnumerateArray())
            {
                if (segment.TryGetProperty("type", out var typeProp) && 
                    typeProp.GetString() == "at")
                {
                    // 获取被@的QQ号 (NapCat格式: data.qq)
                    long atQQ = 0;
                    
                    // 先尝试直接读取 qq 属性
                    if (segment.TryGetProperty("qq", out var qqProp))
                    {
                        if (qqProp.ValueKind == JsonValueKind.Number)
                        {
                            atQQ = qqProp.GetInt64();
                        }
                        else if (qqProp.ValueKind == JsonValueKind.String)
                        {
                            var qqStr = qqProp.GetString();
                            long.TryParse(qqStr, out atQQ);
                        }
                    }
                    // 再尝试读取 data.qq 嵌套属性 (NapCat标准格式)
                    else if (segment.TryGetProperty("data", out var dataProp) && 
                             dataProp.ValueKind == JsonValueKind.Object)
                    {
                        if (dataProp.TryGetProperty("qq", out var nestedQqProp))
                        {
                            if (nestedQqProp.ValueKind == JsonValueKind.Number)
                            {
                                atQQ = nestedQqProp.GetInt64();
                            }
                            else if (nestedQqProp.ValueKind == JsonValueKind.String)
                            {
                                var qqStr = nestedQqProp.GetString();
                                long.TryParse(qqStr, out atQQ);
                            }
                        }
                    }
                    
                    // _logger.LogDebug("检测到@行为，目标QQ: {AtQQ}, BotQQ: {BotQQ}", atQQ, botQQ);
                    
                    // 如果知道Bot的QQ号，精确匹配
                    if (botQQ > 0) return atQQ == botQQ;
                    return true; // BotQQ未知，接受任何@
                }
            }
        }

        return false;
    }

    /// <summary>
    /// 去除消息中的@Bot内容
    /// </summary>
    private string RemoveAtContent(string content, long botQQ)
    {
        // 去除 @BotQQ 或 @昵称 的文本
        // 尝试多种可能的@格式
        content = System.Text.RegularExpressions.Regex.Replace(content, $"@[^\\s]*", "").Trim();
        return content;
    }

    /// <summary>
    /// 发送私聊消息（支持 &lt;img&gt; 标签自动解析为图片消息）
    /// </summary>
    public async Task SendPrivateMessageAsync(long userId, string message)
    {
        var segments = ParseMessageSegments(message);
        if (segments != null)
        {
            // 包含图片标签，使用消息段数组格式发送
            await SendMessageAsync("send_private_msg", new
            {
                user_id = userId,
                message = segments
            });
        }
        else
        {
            await SendMessageAsync("send_private_msg", new
            {
                user_id = userId,
                message = message
            });
        }
    }

    /// <summary>
    /// 发送群消息（支持 &lt;img&gt; 标签自动解析为图片消息）
    /// </summary>
    public async Task SendGroupMessageAsync(long groupId, string message)
    {
        var segments = ParseMessageSegments(message);
        if (segments != null)
        {
            // 包含图片标签，使用消息段数组格式发送
            await SendMessageAsync("send_group_msg", new
            {
                group_id = groupId,
                message = segments
            });
        }
        else
        {
            await SendMessageAsync("send_group_msg", new
            {
                group_id = groupId,
                message = message
            });
        }
    }

    /// <summary>
    /// 发送群图片消息（供 Bot 内部直接使用）
    /// </summary>
    /// <param name="groupId">群号</param>
    /// <param name="filePath">图片文件路径（本地绝对路径）</param>
    /// <param name="text">可选的附带文本</param>
    public async Task SendGroupImageAsync(long groupId, string filePath, string? text = null)
    {
        var segments = BuildImageMessageSegments(filePath, text);
        await SendMessageAsync("send_group_msg", new
        {
            group_id = groupId,
            message = segments
        });
    }

    /// <summary>
    /// 发送私聊图片消息（供 Bot 内部直接使用）
    /// </summary>
    /// <param name="userId">用户QQ号</param>
    /// <param name="filePath">图片文件路径（本地绝对路径）</param>
    /// <param name="text">可选的附带文本</param>
    public async Task SendPrivateImageAsync(long userId, string filePath, string? text = null)
    {
        var segments = BuildImageMessageSegments(filePath, text);
        await SendMessageAsync("send_private_msg", new
        {
            user_id = userId,
            message = segments
        });
    }

    /// <summary>
    /// 将文件路径转换为 OneBot11 支持的 URI 格式。
    /// 本地文件优先转换为 base64:// 格式，确保 NapCat 无需直接访问文件系统即可发送图片。
    /// </summary>
    private static string ResolveFileUri(string filePath)
    {
        // Already a recognized URI scheme — pass through unchanged
        if (filePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            filePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            filePath.StartsWith("file://", StringComparison.OrdinalIgnoreCase) ||
            filePath.StartsWith("base64://", StringComparison.OrdinalIgnoreCase))
        {
            return filePath;
        }

        // Convert data: URL (e.g., returned by ImageUploadService) to base64:// format
        // NapCat does not understand the data: URI scheme
        if (filePath.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var base64Start = filePath.IndexOf(";base64,", StringComparison.OrdinalIgnoreCase);
            if (base64Start >= 0)
            {
                return "base64://" + filePath.Substring(base64Start + 8);
            }
        }

        // For local file paths, embed the file as base64:// so NapCat can send
        // the image regardless of filesystem accessibility (e.g., different containers).
        var fullPath = Path.GetFullPath(filePath);
        if (File.Exists(fullPath))
        {
            try
            {
                var bytes = File.ReadAllBytes(fullPath);
                return "base64://" + Convert.ToBase64String(bytes);
            }
            catch (IOException)
            {
                // File exists but cannot be read — fall back to file:// URI
            }
            catch (UnauthorizedAccessException)
            {
                // No read permission — fall back to file:// URI
            }
        }

        return "file://" + fullPath;
    }

    /// <summary>
    /// 构建包含图片的消息段数组（OneBot11 message segment 格式）
    /// </summary>
    /// <param name="filePath">图片文件路径（本地绝对路径或URL）</param>
    /// <param name="text">可选的附带文本</param>
    private static List<object> BuildImageMessageSegments(string filePath, string? text = null)
    {
        var segments = new List<object>();

        if (!string.IsNullOrEmpty(text))
        {
            segments.Add(new { type = "text", data = new { text = text } });
        }

        segments.Add(new { type = "image", data = new { file = ResolveFileUri(filePath) } });

        return segments;
    }

    /// <summary>
    /// 解析消息中的 &lt;img&gt; 标签，将消息转换为 OneBot11 消息段数组。
    /// 格式: &lt;img&gt;file_path
    /// 支持文本和图片混合消息。
    /// </summary>
    /// <returns>消息段数组，如果消息不包含 &lt;img&gt; 标签则返回 null</returns>
    private static List<object>? ParseMessageSegments(string message)
    {
        const string imgTag = "<img>";
        if (!message.Contains(imgTag, StringComparison.OrdinalIgnoreCase))
            return null;

        var segments = new List<object>();
        var remaining = message;

        while (remaining.Length > 0)
        {
            var imgIndex = remaining.IndexOf(imgTag, StringComparison.OrdinalIgnoreCase);
            if (imgIndex < 0)
            {
                // 没有更多图片标签，剩余部分作为文本
                if (remaining.Length > 0)
                {
                    segments.Add(new { type = "text", data = new { text = remaining } });
                }
                break;
            }

            // 图片标签之前的文本
            if (imgIndex > 0)
            {
                var textBefore = remaining[..imgIndex];
                if (!string.IsNullOrWhiteSpace(textBefore))
                {
                    segments.Add(new { type = "text", data = new { text = textBefore } });
                }
            }

            // 提取图片路径：从 <img> 后到行尾或下一个 <img> 标签
            var pathStart = imgIndex + imgTag.Length;
            var pathEnd = remaining.Length;

            // 图片路径到行尾
            var newlineIndex = remaining.IndexOf('\n', pathStart);
            if (newlineIndex >= 0)
            {
                pathEnd = newlineIndex;
            }

            // 或者到下一个 <img> 标签
            var nextImgIndex = remaining.IndexOf(imgTag, pathStart, StringComparison.OrdinalIgnoreCase);
            if (nextImgIndex >= 0 && nextImgIndex < pathEnd)
            {
                pathEnd = nextImgIndex;
            }

            var filePath = remaining[pathStart..pathEnd].Trim();
            if (!string.IsNullOrEmpty(filePath))
            {
                segments.Add(new { type = "image", data = new { file = ResolveFileUri(filePath) } });
            }

            // 移动到剩余部分
            remaining = pathEnd < remaining.Length ? remaining[pathEnd..] : "";
            // 如果当前位置是换行符，跳过它
            if (remaining.Length > 0 && remaining[0] == '\n')
            {
                remaining = remaining[1..];
            }
        }

        return segments.Count > 0 ? segments : null;
    }

    /// <summary>
    /// 调用 OneBot HTTP API 发送消息
    /// </summary>
    private async Task SendMessageAsync(string action, object payload)
    {
        try
        {
            var url = $"{_settings.QQBot.NapCat.HttpApiUrl}/{action}";
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            _logger.LogInformation("Sending {Action} to {Url}: {Payload}", action, url, json);
            
            var response = await _httpClient.PostAsync(url, content);
            var responseBody = await response.Content.ReadAsStringAsync();
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to send message: {Status} - {Error}", 
                    response.StatusCode, responseBody);
            }
            else
            {
                // NapCat always returns HTTP 200; check the JSON body for the actual result.
                // A successful delivery has {"status":"ok"} or {"retcode":0}.
                try
                {
                    using var doc = JsonDocument.Parse(responseBody);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("status", out var statusProp) &&
                        statusProp.GetString() == "failed")
                    {
                        var retcode = root.TryGetProperty("retcode", out var retcodeProp)
                            ? retcodeProp.GetInt32().ToString()
                            : "unknown";
                        var msg = root.TryGetProperty("msg", out var msgProp)
                            ? msgProp.GetString()
                            : responseBody;
                        _logger.LogError(
                            "NapCat failed to send {Action}: retcode={Retcode}, msg={Message}",
                            action, retcode, msg);
                    }
                    else
                    {
                        _logger.LogDebug("Message sent successfully via {Action}", action);
                    }
                }
                catch (JsonException ex)
                {
                    // Response body is not valid JSON — assume success since HTTP status was OK
                    _logger.LogWarning(ex, "Could not parse NapCat response body for {Action}; assuming success", action);
                    _logger.LogInformation("Message sent successfully: {Status}", response.StatusCode);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Bot...");
        _isRunning = false;
        _reconnectCts.Cancel();

        // Shutdown all modules
        await _moduleManager.ShutdownAllAsync();

        if (_webSocket?.State == WebSocketState.Open)
        {
            await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Shutting down", cancellationToken);
        }

        _webSocket?.Dispose();
        _httpClient.Dispose();
        _reconnectCts.Dispose();

        await base.StopAsync(cancellationToken);
    }
}

/// <summary>
/// OneBot 事件数据结构
/// </summary>
public class OneBotEvent
{
    [JsonPropertyName("post_type")]
    public string PostType { get; set; } = "";

    [JsonPropertyName("message_type")]
    public string? MessageType { get; set; }

    [JsonPropertyName("sub_type")]
    public string? SubType { get; set; }

    [JsonPropertyName("message_id")]
    public long MessageId { get; set; }

    [JsonPropertyName("user_id")]
    public long? UserId { get; set; }

    [JsonPropertyName("group_id")]
    public long? GroupId { get; set; }

    [JsonPropertyName("message")]
    public object? Message { get; set; }

    [JsonPropertyName("raw_message")]
    public string? RawMessage { get; set; }

    [JsonPropertyName("font")]
    public int Font { get; set; }

    [JsonPropertyName("sender")]
    public OneBotSender? Sender { get; set; }

    [JsonPropertyName("time")]
    public long Time { get; set; }

    [JsonPropertyName("self_id")]
    public long SelfId { get; set; }

    [JsonPropertyName("meta_event_type")]
    public string? MetaEventType { get; set; }

    [JsonPropertyName("notice_type")]
    public string? NoticeType { get; set; }
}

/// <summary>
/// OneBot 发送者信息
/// </summary>
public class OneBotSender
{
    [JsonPropertyName("user_id")]
    public long UserId { get; set; }

    [JsonPropertyName("nickname")]
    public string? Nickname { get; set; }

    [JsonPropertyName("card")]
    public string? Card { get; set; }

    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("sex")]
    public string? Sex { get; set; }

    [JsonPropertyName("age")]
    public int Age { get; set; }
}
