using DigimonBot.Host.Configs;
using DigimonBot.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DigimonBot.Host;

/// <summary>
/// NapCatQQ Bot 服务
/// 通过 WebSocket 接收消息，通过 HTTP API 发送消息
/// </summary>
public class BotService : BackgroundService, Core.Services.IImageUrlResolver
{
    private readonly ILogger<BotService> _logger;
    private readonly AppSettings _settings;
    private readonly Messaging.Handlers.IMessageHandler _messageHandler;
    private readonly IMessageHistoryService _messageHistory;
    private readonly HttpClient _httpClient;
    private ClientWebSocket? _webSocket;
    private readonly CancellationTokenSource _reconnectCts = new();
    private bool _isRunning;
    private long _botQQ; // Bot 自己的 QQ 号

    private readonly Core.Events.IEventPublisher _eventPublisher;
    private readonly Core.Services.IGroupChatMonitorService _groupChatMonitor;
    private readonly Core.Services.ITavernService _tavernService;
    private readonly Core.Services.ITavernConfigService _tavernConfigService;
    
    // 特别关注用户冷却时间记录：Key = "{groupId}:{userId}"
    private readonly Dictionary<string, DateTime> _specialFocusCooldown = new();

    public BotService(
        ILogger<BotService> logger,
        IOptions<AppSettings> settings,
        Messaging.Handlers.IMessageHandler messageHandler,
        IMessageHistoryService messageHistory,
        Core.Events.IEventPublisher eventPublisher,
        Core.Services.IGroupChatMonitorService groupChatMonitor,
        Core.Services.ITavernService tavernService,
        Core.Services.ITavernConfigService tavernConfigService)
    {
        _logger = logger;
        _settings = settings.Value;
        _messageHandler = messageHandler;
        _messageHistory = messageHistory;
        _eventPublisher = eventPublisher;
        _groupChatMonitor = groupChatMonitor;
        _tavernService = tavernService;
        _tavernConfigService = tavernConfigService;
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
        
        // 订阅酒馆自主发言事件
        _eventPublisher.OnTavernAutoSpeak += async (sender, args) =>
        {
            try
            {
                _logger.LogInformation("收到酒馆自主发言事件: Group={GroupId}", args.GroupId);
                await SendGroupMessageAsync(args.GroupId, args.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "发送酒馆自主发言消息失败");
            }
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting Digimon Bot with NapCatQQ...");
        _logger.LogInformation("Config: AI Provider={Provider}, Model={Model}", 
            _settings.AI.Provider, _settings.AI.Model);
        
        _isRunning = true;

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
            // _logger.LogDebug("收到WebSocket消息: {Message}", jsonMessage);
            
            var eventData = JsonSerializer.Deserialize<OneBotEvent>(jsonMessage);
            if (eventData == null) 
            {
                _logger.LogWarning("消息反序列化失败");
                return;
            }

            // _logger.LogDebug("消息类型: PostType={PostType}, MessageType={MessageType}", 
            //     eventData.PostType, eventData.MessageType);

            // 处理消息事件
            if (eventData.PostType == "message")
            {
                // _logger.LogInformation("收到聊天消息: 类型={MsgType}, 发送者={User}", 
                //     eventData.MessageType, eventData.UserId);
                await HandleMessageEventAsync(eventData);
            }
            // 处理元事件（心跳等）
            else if (eventData.PostType == "meta_event")
            {
                _logger.LogDebug("Meta event: {MetaEvent}", eventData.MetaEventType);
            }
            // 处理通知事件
            else if (eventData.PostType == "notice")
            {
                _logger.LogDebug("Notice event: {NoticeType}", eventData.NoticeType);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling message: {Message}", jsonMessage);
        }
    }

    /// <summary>
    /// 处理消息事件（私聊/群聊）
    /// </summary>
    private async Task HandleMessageEventAsync(OneBotEvent eventData)
    {
        var messageType = eventData.MessageType;
        var userId = eventData.UserId?.ToString() ?? "unknown";
        var messageId = eventData.MessageId;
        
        // 更新 Bot QQ 号
        if (eventData.SelfId > 0 && _botQQ == 0)
        {
            _botQQ = eventData.SelfId;
            _logger.LogInformation("Bot QQ号已设置: {BotQQ}", _botQQ);
        }
        
        // 提取消息内容
        _logger.LogDebug("开始提取消息内容...");
        var content = ExtractMessageContent(eventData.Message);
        _logger.LogInformation("提取后的消息内容: '{Content}'", content);
        
        if (string.IsNullOrWhiteSpace(content)) 
        {
            _logger.LogWarning("消息内容为空，忽略此消息");
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
        
        // 群聊监测：记录所有群消息（在过滤之前，确保监测到所有消息）
        if (messageType == "group" && eventData.GroupId.HasValue)
        {
            _groupChatMonitor.AddMessage(eventData.GroupId.Value, userId, userName, content);
            
            // 检查是否触发酒馆自主发言（在过滤之前，确保普通消息也能触发）
            _ = Task.Run(async () => await CheckTavernAutoSpeakAsync(eventData.GroupId.Value));
        }
        
        // 群聊特殊处理：检查是否@Bot或以/开头
        bool isAtBot = false;
        bool isCommand = false;
        
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
            
            // 特别关注检查（在过滤之前，但需要知道是否@Bot）
            if (eventData.GroupId.HasValue)
            {
                _ = Task.Run(async () => await CheckSpecialFocusAsync(
                    eventData.GroupId.Value, userId, userName, content, isAtBot));
            }
            
            if (!isAtBot && !isCommand) return; // 忽略不相关的群消息

            // 去除@的文本
            if (isAtBot)
            {
                content = RemoveAtContent(content, _botQQ);
                // _logger.LogDebug("去除@后的内容: '{Content}'", content);
            }
        }

        // 构建消息上下文
        var context = new Messaging.Handlers.MessageContext
        {
            UserId = userId,
            OriginalUserId = userId,
            UserName = userName,
            Content = content,
            GroupId = eventData.GroupId,
            IsGroupMessage = messageType == "group",
            IsMentioned = isAtBot,
            Timestamp = DateTime.Now,
            Source = messageType == "group" ? Messaging.Handlers.MessageSource.Group : Messaging.Handlers.MessageSource.Private,
            MentionedUserIds = mentionedUserIds
        };

        _logger.LogInformation("[{Source}] {User}: {Content}", 
            context.IsGroupMessage ? $"Group {context.GroupId}" : "Private",
            context.UserName, 
            context.Content);

        // 处理消息
        try
        {
            _logger.LogDebug("Calling message handler...");
            var result = await _messageHandler.HandleMessageAsync(context);
            _logger.LogDebug("Handler result: Handled={Handled}, HasResponse={HasResponse}", 
                result.Handled, !string.IsNullOrEmpty(result.Response));
            
            if (result.Handled && !string.IsNullOrEmpty(result.Response))
            {
                _logger.LogInformation("Sending response: {Response}", result.Response);
                
                // 发送回复
                if (context.IsGroupMessage && context.GroupId.HasValue)
                {
                    await SendGroupMessageAsync(context.GroupId.Value, result.Response);
                }
                else
                {
                    await SendPrivateMessageAsync(long.Parse(userId), result.Response);
                }

                // 如果有进化消息，延迟后发送
                if (result.EvolutionOccurred && !string.IsNullOrEmpty(result.EvolutionMessage))
                {
                    await Task.Delay(500);
                    
                    if (context.IsGroupMessage && context.GroupId.HasValue)
                    {
                        await SendGroupMessageAsync(context.GroupId.Value, result.EvolutionMessage);
                    }
                    else
                    {
                        await SendPrivateMessageAsync(long.Parse(userId), result.EvolutionMessage);
                    }
                }
            }
            else
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
    /// 发送私聊消息
    /// </summary>
    private async Task SendPrivateMessageAsync(long userId, string message)
    {
        await SendMessageAsync("send_private_msg", new
        {
            user_id = userId,
            message = message
        });
    }

    /// <summary>
    /// 发送群消息
    /// </summary>
    private async Task SendGroupMessageAsync(long groupId, string message)
    {
        await SendMessageAsync("send_group_msg", new
        {
            group_id = groupId,
            message = message
        });
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
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to send message: {Status} - {Error}", 
                    response.StatusCode, error);
            }
            else
            {
                _logger.LogInformation("Message sent successfully: {Status}", response.StatusCode);
                _logger.LogDebug("Message sent successfully");
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

        if (_webSocket?.State == WebSocketState.Open)
        {
            await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Shutting down", cancellationToken);
        }

        _webSocket?.Dispose();
        _httpClient.Dispose();
        _reconnectCts.Dispose();

        await base.StopAsync(cancellationToken);
    }

    /// <summary>
    /// 检查并触发酒馆自主发言
    /// </summary>
    private async Task CheckTavernAutoSpeakAsync(long groupId)
    {
        try
        {
            // 检查酒馆状态
            if (!_tavernService.IsEnabled || !_tavernService.HasCharacterLoaded())
            {
                return;
            }

            _logger.LogInformation("[BotService] 检查群 {GroupId} 自主发言条件", groupId);

            // 获取监测状态
            var status = _groupChatMonitor.GetGroupStatus(groupId);
            _logger.LogInformation("[BotService] 群 {GroupId} 状态: 消息={Count}, 关键词={HasKeyword}, 冷却={Cooldown}", 
                groupId, status.MessageCount, status.HasHighFreqKeyword, status.IsInCooldown);

            if (!status.CanTrigger)
            {
                return;
            }

            _logger.LogInformation("[BotService] 群 {GroupId} 满足触发条件，开始生成回复", groupId);

            // 检查是否启用自主发言
            if (!_tavernConfigService.Config.AutoSpeak.Enabled)
            {
                _logger.LogInformation("[BotService] 自主发言已禁用");
                return;
            }

            // 生成总结和回复
            var summary = await _groupChatMonitor.GenerateSummaryAsync(groupId);
            var keywords = string.Join(",", status.TopKeywords.Take(3).Select(kv => kv.Key));
            var response = await _tavernService.GenerateSummaryResponseAsync(summary, keywords);

            // 使用配置中的消息模板
            var characterName = _tavernService.CurrentCharacter?.Name ?? "角色";
            var messagePrefix = _tavernConfigService.Config.AutoSpeak.MessagePrefix;
            var message = messagePrefix.Replace("{CharacterName}", characterName) + response;

            _logger.LogInformation("[BotService] 群 {GroupId} 发送自主发言", groupId);
            await SendGroupMessageAsync(groupId, message);
            
            // 记录触发时间，启动冷却
            _groupChatMonitor.RecordTriggerTime(groupId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BotService] 自主发言检查异常");
        }
    }

    /// <summary>
    /// 检查特别关注用户发言
    /// </summary>
    private async Task CheckSpecialFocusAsync(long groupId, string userId, string userName, string content, bool isAtBot)
    {
        try
        {
            var config = _tavernConfigService.Config.SpecialFocus;
            
            // 检查是否启用特别关注
            if (!config.Enabled)
            {
                return;
            }
            
            // 检查酒馆状态
            if (!_tavernService.IsEnabled || !_tavernService.HasCharacterLoaded())
            {
                return;
            }
            
            // 检查是否@Bot（如果配置要求）
            if (config.RequireMention && !isAtBot)
            {
                return;
            }
            
            // 检查用户是否在特别关注列表中
            // 支持两种格式：纯QQ号 或 QQ号@groupId
            var isFocused = config.UserIds.Any(id => 
                id == userId || 
                id == $"{userId}@g{groupId}" ||
                id == $"{userId}@{groupId}");
            
            if (!isFocused)
            {
                return;
            }
            
            _logger.LogInformation("[特别关注] 检测到关注用户发言: Group={GroupId}, User={User}, Content={Content}", 
                groupId, userName, content.Length > 20 ? content[..20] + "..." : content);
            
            // 检查冷却时间
            var cooldownKey = $"{groupId}:{userId}";
            if (_specialFocusCooldown.TryGetValue(cooldownKey, out var lastTime))
            {
                var elapsed = DateTime.Now - lastTime;
                var cooldown = TimeSpan.FromMinutes(config.CooldownMinutes);
                if (elapsed < cooldown)
                {
                    var remaining = (int)(cooldown - elapsed).TotalSeconds;
                    _logger.LogInformation("[特别关注] 用户 {User} 处于冷却期，剩余 {Remaining} 秒", userName, remaining);
                    return;
                }
            }
            
            _logger.LogInformation("[特别关注] 为用户 {User} 生成回复", userName);
            
            // 构建提示词
            var scenario = config.ScenarioTemplate
                .Replace("{UserName}", userName)
                .Replace("{Message}", content);
            
            // 调用AI生成回复
            var characterName = _tavernService.CurrentCharacter?.Name ?? "角色";
            var response = await _tavernService.GenerateResponseAsync(scenario, userName);
            
            // 构建消息
            var messagePrefix = config.MessagePrefix
                .Replace("{CharacterName}", characterName)
                .Replace("{UserName}", userName);
            var message = messagePrefix + response;
            
            _logger.LogInformation("[特别关注] 发送回复给 {User}", userName);
            await SendGroupMessageAsync(groupId, message);
            
            // 记录冷却时间
            _specialFocusCooldown[cooldownKey] = DateTime.Now;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[特别关注] 处理异常");
        }
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
