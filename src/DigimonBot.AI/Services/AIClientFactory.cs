using Microsoft.Extensions.Logging;

namespace DigimonBot.AI.Services;

/// <summary>
/// AI提供商类型
/// </summary>
public enum AIProvider
{
    DeepSeek,
    GLM,        // 智谱AI
    OpenAI,     // 兼容OpenAI的自定义API
    Custom      // 完全自定义配置
}

/// <summary>
/// AI客户端配置
/// </summary>
public class AIClientConfig
{
    /// <summary>提供商类型</summary>
    public AIProvider Provider { get; set; } = AIProvider.DeepSeek;
    
    /// <summary>API密钥</summary>
    public string ApiKey { get; set; } = "";
    
    /// <summary>模型名称</summary>
    public string Model { get; set; } = "deepseek-chat";
    
    /// <summary>自定义Base URL（可选）</summary>
    public string? BaseUrl { get; set; }
    
    /// <summary>请求超时（秒）</summary>
    public int TimeoutSeconds { get; set; } = 60;
    
    /// <summary>温度参数（创造性）</summary>
    public float Temperature { get; set; } = 0.8f;
    
    /// <summary>最大Token数</summary>
    public int MaxTokens { get; set; } = 1000;
}

/// <summary>
/// AI客户端工厂
/// </summary>
public class AIClientFactory
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILoggerFactory _loggerFactory;

    public AIClientFactory(IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory)
    {
        _httpClientFactory = httpClientFactory;
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// 根据配置创建AI客户端
    /// </summary>
    public IAIClient CreateClient(AIClientConfig config)
    {
        var httpClient = _httpClientFactory.CreateClient($"AI_{config.Provider}");
        httpClient.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds);

        return config.Provider switch
        {
            AIProvider.GLM => CreateGLMClient(httpClient, config),
            AIProvider.OpenAI => CreateOpenAICompatibleClient(httpClient, config),
            AIProvider.Custom => CreateCustomClient(httpClient, config),
            _ => CreateDeepSeekClient(httpClient, config)
        };
    }

    private IAIClient CreateDeepSeekClient(HttpClient httpClient, AIClientConfig config)
    {
        var logger = _loggerFactory.CreateLogger<DeepSeekClient>();
        
        // DeepSeek默认配置 - 处理 null 和空字符串
        var baseUrl = string.IsNullOrWhiteSpace(config.BaseUrl) 
            ? "https://api.deepseek.com" 
            : config.BaseUrl;
        var model = string.IsNullOrEmpty(config.Model) ? "deepseek-chat" : config.Model;
        
        return new DeepSeekClient(httpClient, logger, config.ApiKey, model, baseUrl)
        {
            Temperature = config.Temperature,
            MaxTokens = config.MaxTokens
        };
    }

    private IAIClient CreateGLMClient(HttpClient httpClient, AIClientConfig config)
    {
        var logger = _loggerFactory.CreateLogger<GLMClient>();
        
        // GLM默认配置 - 处理 null 和空字符串
        var baseUrl = string.IsNullOrWhiteSpace(config.BaseUrl) 
            ? "https://open.bigmodel.cn/api/paas/v4" 
            : config.BaseUrl;
        var model = string.IsNullOrEmpty(config.Model) ? "glm-4-flash" : config.Model;
        
        return new GLMClient(httpClient, logger, config.ApiKey, model, baseUrl)
        {
            Temperature = config.Temperature,
            MaxTokens = config.MaxTokens
        };
    }

    private IAIClient CreateOpenAICompatibleClient(HttpClient httpClient, AIClientConfig config)
    {
        var logger = _loggerFactory.CreateLogger<DeepSeekClient>();
        
        // 使用DeepSeek客户端作为通用OpenAI兼容客户端 - 处理 null 和空字符串
        var baseUrl = string.IsNullOrWhiteSpace(config.BaseUrl) 
            ? "https://api.openai.com/v1" 
            : config.BaseUrl;
        var model = string.IsNullOrEmpty(config.Model) ? "gpt-3.5-turbo" : config.Model;
        
        return new DeepSeekClient(httpClient, logger, config.ApiKey, model, baseUrl)
        {
            Temperature = config.Temperature,
            MaxTokens = config.MaxTokens
        };
    }

    private IAIClient CreateCustomClient(HttpClient httpClient, AIClientConfig config)
    {
        // Custom使用DeepSeek客户端作为基础（OpenAI兼容格式）
        var logger = _loggerFactory.CreateLogger<DeepSeekClient>();
        
        if (string.IsNullOrEmpty(config.BaseUrl))
        {
            throw new ArgumentException("Custom provider requires BaseUrl to be specified");
        }
        
        return new DeepSeekClient(httpClient, logger, config.ApiKey, config.Model, config.BaseUrl)
        {
            Temperature = config.Temperature,
            MaxTokens = config.MaxTokens
        };
    }
}
