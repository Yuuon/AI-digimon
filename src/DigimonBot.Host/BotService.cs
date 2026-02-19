using DigimonBot.Host.Configs;
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
public class BotService : BackgroundService
{
    private readonly ILogger<BotService> _logger;
    private readonly AppSettings _settings;
    private readonly Messaging.Handlers.IMessageHandler _messageHandler;
    private readonly HttpClient _httpClient;
    private ClientWebSocket? _webSocket;
    private readonly CancellationTokenSource _reconnectCts = new();
    private bool _isRunning;
    private long _botQQ; // Bot 自己的 QQ 号

    public BotService(
        ILogger<BotService> logger,
        IOptions<AppSettings> settings,
        Messaging.Handlers.IMessageHandler messageHandler)
    {
        _logger = logger;
        _settings = settings.Value;
        _messageHandler = messageHandler;
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

        // 构建消息上下文
        var context = new Messaging.Handlers.MessageContext
        {
            UserId = userId,
            UserName = userName,
            Content = content,
            GroupId = eventData.GroupId ?? 0,
            IsGroupMessage = messageType == "group",
            Timestamp = DateTime.Now,
            Source = messageType == "group" ? Messaging.Handlers.MessageSource.Group : Messaging.Handlers.MessageSource.Private
        };

        // 群聊特殊处理：检查是否@Bot或以/开头
        if (context.IsGroupMessage)
        {
            // _logger.LogDebug("群聊消息，检查触发条件...");
            
            bool isAtBot = false;
            bool isCommand = false;
            
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
            
            // _logger.LogDebug("isAtBot={IsAtBot}, isCommand={IsCommand}", isAtBot, isCommand);
            
            if (!isAtBot && !isCommand) return; // 忽略不相关的群消息

            // 去除@的文本
            if (isAtBot)
            {
                content = RemoveAtContent(content, _botQQ);
                context.Content = content;
                // _logger.LogDebug("去除@后的内容: '{Content}'", content);
            }
        }

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
                if (context.IsGroupMessage)
                {
                    await SendGroupMessageAsync(context.GroupId, result.Response);
                }
                else
                {
                    await SendPrivateMessageAsync(long.Parse(userId), result.Response);
                }

                // 如果有进化消息，延迟后发送
                if (result.EvolutionOccurred && !string.IsNullOrEmpty(result.EvolutionMessage))
                {
                    await Task.Delay(500);
                    
                    if (context.IsGroupMessage)
                    {
                        await SendGroupMessageAsync(context.GroupId, result.EvolutionMessage);
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
