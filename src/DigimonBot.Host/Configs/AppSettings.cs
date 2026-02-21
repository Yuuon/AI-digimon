using DigimonBot.AI.Services;
using DigimonBot.Core.Models;

namespace DigimonBot.Host.Configs;

/// <summary>
/// 应用配置
/// </summary>
public class AppSettings
{
    /// <summary>QQ Bot配置</summary>
    public QQBotConfig QQBot { get; set; } = new();
    
    /// <summary>AI API配置</summary>
    public AIConfig AI { get; set; } = new();
    
    /// <summary>数据文件路径</summary>
    public DataConfig Data { get; set; } = new();
    
    /// <summary>管理员配置（白名单等）</summary>
    public AdminConfig Admin { get; set; } = new();
}

public class QQBotConfig
{
    /// <summary>
    /// NapCatQQ 连接配置
    /// </summary>
    public NapCatConfig NapCat { get; set; } = new();
}

/// <summary>
/// NapCatQQ 连接配置
/// </summary>
public class NapCatConfig
{
    /// <summary>
    /// Bot 的 QQ 号（用于识别@Bot的消息，必须填写）
    /// </summary>
    public long BotQQ { get; set; }
    
    /// <summary>
    /// 连接方式：WebSocketReverse, WebSocketForward, HTTP
    /// </summary>
    public string ConnectionType { get; set; } = "WebSocketReverse";
    
    /// <summary>
    /// WebSocket反向连接 - 服务端监听的Host</summary>
    public string WebSocketHost { get; set; } = "127.0.0.1";
    
    /// <summary>
    /// WebSocket反向连接 - 服务端监听的端口</summary>
    public int WebSocketPort { get; set; } = 5140;
    
    /// <summary>
    /// WebSocket反向连接 - 访问令牌（可选）</summary>
    public string? AccessToken { get; set; }
    
    /// <summary>
    /// HTTP API 地址（用于发送消息）</summary>
    public string HttpApiUrl { get; set; } = "http://127.0.0.1:3000";
    
    /// <summary>
    /// HTTP API 访问令牌（可选）</summary>
    public string? HttpAccessToken { get; set; }
    
    /// <summary>
    /// 消息上报路径（WebSocket反向连接使用）</summary>
    public string PostPath { get; set; } = "/onebot";
    
    /// <summary>
    /// 是否自动重连</summary>
    public bool AutoReconnect { get; set; } = true;
    
    /// <summary>
    /// 重连间隔（秒）</summary>
    public int ReconnectInterval { get; set; } = 10;
    
    /// <summary>
    /// 群聊数码兽模式：Separate（各自培养）/ Shared（共同培养）</summary>
    public string GroupDigimonMode { get; set; } = "Separate";
}

/// <summary>
/// AI配置 - 支持多提供商
/// </summary>
public class AIConfig
{
    /// <summary>
    /// 主要AI提供商
    /// deepseek - DeepSeek (默认)
    /// glm - 智谱AI
    /// openai - OpenAI兼容API
    /// custom - 自定义API
    /// </summary>
    public string Provider { get; set; } = "deepseek";
    
    /// <summary>API密钥</summary>
    public string ApiKey { get; set; } = "";
    
    /// <summary>模型名称</summary>
    public string Model { get; set; } = "deepseek-chat";
    
    /// <summary>
    /// 自定义API基础URL（可选）
    /// 留空则使用各提供商默认值
    /// </summary>
    public string? BaseUrl { get; set; }
    
    /// <summary>请求超时（秒）</summary>
    public int TimeoutSeconds { get; set; } = 60;
    
    /// <summary>
    /// 温度参数 (0-2)
    /// 越高回答越创造性，越低越确定性
    /// </summary>
    public float Temperature { get; set; } = 0.8f;
    
    /// <summary>最大Token数</summary>
    public int MaxTokens { get; set; } = 1000;
    
    /// <summary>
    /// 识图模型配置（可选）
    /// 如果配置，识图功能将使用此模型
    /// </summary>
    public VisionConfig? VisionModel { get; set; }
    
    /// <summary>
    /// 转换为客户端配置对象
    /// </summary>
    public AIClientConfig ToClientConfig()
    {
        return new AIClientConfig
        {
            Provider = ParseProvider(Provider),
            ApiKey = ApiKey,
            Model = Model,
            BaseUrl = BaseUrl,
            TimeoutSeconds = TimeoutSeconds,
            Temperature = Temperature,
            MaxTokens = MaxTokens
        };
    }

    private static AIProvider ParseProvider(string provider) => provider?.ToLower() switch
    {
        "glm" or "zhipu" or "智谱" => AIProvider.GLM,
        "openai" => AIProvider.OpenAI,
        "custom" => AIProvider.Custom,
        _ => AIProvider.DeepSeek
    };
}

/// <summary>
/// 识图模型配置
/// </summary>
public class VisionConfig
{
    /// <summary>API基础URL</summary>
    public string BaseUrl { get; set; } = "";
    
    /// <summary>模型名称，例如：glm-4v</summary>
    public string Model { get; set; } = "";
    
    /// <summary>API密钥（可选，留空则使用主AI配置的密钥）</summary>
    public string? ApiKey { get; set; }
    
    /// <summary>是否启用识图功能</summary>
    public bool Enabled => !string.IsNullOrEmpty(BaseUrl) && !string.IsNullOrEmpty(Model);
}

public class DataConfig
{
    /// <summary>数码宝贝数据库路径</summary>
    public string DigimonDatabasePath { get; set; } = "Data/digimon_database.json";
    
    /// <summary>物品数据库路径</summary>
    public string ItemsDatabasePath { get; set; } = "Data/items_database.json";
    
    /// <summary>持久化提供程序：sqlite / memory</summary>
    public string PersistenceProvider { get; set; } = "sqlite";
    
    /// <summary>SQLite 连接字符串</summary>
    public string SqliteConnectionString { get; set; } = "Data Source=Data/bot_data.db";
    
    /// <summary>每 Token 获得的金币数（默认每10个token获得1金币）</summary>
    public int GoldPerToken { get; set; } = 1;
    
    /// <summary>Token计算分母（实际金币 = Tokens / GoldTokenDivisor）</summary>
    public int GoldTokenDivisor { get; set; } = 10;
    
    /// <summary>战斗保护时间（秒），默认5分钟</summary>
    public int BattleProtectionSeconds { get; set; } = 300;
}

/// <summary>
/// 预配置的AI提供商设置
/// </summary>
public static class AIPresets
{
    /// <summary>DeepSeek 默认配置</summary>
    public static AIConfig DeepSeek => new()
    {
        Provider = "deepseek",
        Model = "deepseek-chat",
        BaseUrl = null,
        Temperature = 0.8f,
        MaxTokens = 1000
    };

    /// <summary>DeepSeek Reasoner (推理模型)</summary>
    public static AIConfig DeepSeekReasoner => new()
    {
        Provider = "deepseek",
        Model = "deepseek-reasoner",
        BaseUrl = null,
        Temperature = 0.6f,
        MaxTokens = 2000
    };

    /// <summary>智谱GLM-4-Flash (免费版)</summary>
    public static AIConfig GLM4Flash => new()
    {
        Provider = "glm",
        Model = "glm-4-flash",
        BaseUrl = null,
        Temperature = 0.8f,
        MaxTokens = 1000
    };

    /// <summary>智谱GLM-4 (标准版)</summary>
    public static AIConfig GLM4 => new()
    {
        Provider = "glm",
        Model = "glm-4",
        BaseUrl = null,
        Temperature = 0.8f,
        MaxTokens = 1500
    };

    /// <summary>智谱GLM-4-Air (轻量版)</summary>
    public static AIConfig GLM4Air => new()
    {
        Provider = "glm",
        Model = "glm-4-air",
        BaseUrl = null,
        Temperature = 0.8f,
        MaxTokens = 1000
    };

    /// <summary>硅基流动 (国内OpenAI兼容)</summary>
    public static AIConfig SiliconFlow => new()
    {
        Provider = "openai",
        Model = "deepseek-ai/DeepSeek-V2.5",
        BaseUrl = "https://api.siliconflow.cn/v1",
        Temperature = 0.8f,
        MaxTokens = 1000
    };
}
