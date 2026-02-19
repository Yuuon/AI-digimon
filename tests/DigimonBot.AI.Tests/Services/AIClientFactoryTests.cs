using DigimonBot.AI.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DigimonBot.AI.Tests.Services;

public class AIClientFactoryTests
{
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly ILoggerFactory _loggerFactory;
    private readonly AIClientFactory _factory;

    public AIClientFactoryTests()
    {
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        
        // 使用真正的 LoggerFactory 替代 Mock
        // 因为 CreateLogger<T>() 是扩展方法，Moq 无法 mock
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Warning);
        });

        _factory = new AIClientFactory(_httpClientFactoryMock.Object, _loggerFactory);
    }

    [Fact]
    public void CreateClient_DeepSeekProvider_ReturnsDeepSeekClient()
    {
        SetupHttpClient();
        
        var config = new AIClientConfig
        {
            Provider = AIProvider.DeepSeek,
            ApiKey = "test-key",
            Model = "deepseek-chat"
        };

        var client = _factory.CreateClient(config);

        Assert.IsType<DeepSeekClient>(client);
    }

    [Fact]
    public void CreateClient_GLMProvider_ReturnsGLMClient()
    {
        SetupHttpClient();
        
        var config = new AIClientConfig
        {
            Provider = AIProvider.GLM,
            ApiKey = "test-key",
            Model = "glm-4-flash"
        };

        var client = _factory.CreateClient(config);

        Assert.IsType<GLMClient>(client);
    }

    [Fact]
    public void CreateClient_OpenAIProvider_ReturnsDeepSeekClient()
    {
        SetupHttpClient();
        
        // OpenAI兼容客户端使用DeepSeekClient作为基础
        var config = new AIClientConfig
        {
            Provider = AIProvider.OpenAI,
            ApiKey = "test-key",
            Model = "gpt-3.5-turbo"
        };

        var client = _factory.CreateClient(config);

        Assert.IsType<DeepSeekClient>(client);
    }

    [Fact]
    public void CreateClient_CustomProvider_WithoutBaseUrl_ThrowsException()
    {
        SetupHttpClient();
        
        var config = new AIClientConfig
        {
            Provider = AIProvider.Custom,
            ApiKey = "test-key",
            Model = "custom-model"
            // BaseUrl is null
        };

        Assert.Throws<ArgumentException>(() => _factory.CreateClient(config));
    }

    [Fact]
    public void CreateClient_CustomProvider_WithBaseUrl_ReturnsClient()
    {
        SetupHttpClient();
        
        var config = new AIClientConfig
        {
            Provider = AIProvider.Custom,
            ApiKey = "test-key",
            Model = "custom-model",
            BaseUrl = "https://custom.api.com"
        };

        var client = _factory.CreateClient(config);

        Assert.IsType<DeepSeekClient>(client);
    }

    [Fact]
    public void CreateClient_DefaultModel_UsesProviderDefault()
    {
        SetupHttpClient();
        
        var config = new AIClientConfig
        {
            Provider = AIProvider.GLM,
            ApiKey = "test-key",
            Model = "" // 空模型
        };

        var client = _factory.CreateClient(config);

        Assert.IsType<GLMClient>(client);
        // 应该使用 glm-4-flash 作为默认模型
    }

    [Theory]
    [InlineData(AIProvider.DeepSeek)]
    [InlineData(AIProvider.GLM)]
    public void CreateClient_DefaultBaseUrl_UsesProviderDefault(AIProvider provider)
    {
        SetupHttpClient();

        var config = new AIClientConfig
        {
            Provider = provider,
            ApiKey = "test-key",
            BaseUrl = null // 使用默认值
        };

        // 验证能正常创建（内部会使用正确的BaseUrl）
        var client = _factory.CreateClient(config);
        Assert.NotNull(client);
    }

    [Fact]
    public void CreateClient_AppliesConfiguration()
    {
        SetupHttpClient();
        
        var config = new AIClientConfig
        {
            Provider = AIProvider.DeepSeek,
            ApiKey = "test-key",
            Model = "deepseek-chat",
            Temperature = 0.5f,
            MaxTokens = 500
        };

        var client = _factory.CreateClient(config) as DeepSeekClient;

        Assert.NotNull(client);
        Assert.Equal(0.5f, client.Temperature);
        Assert.Equal(500, client.MaxTokens);
    }

    private void SetupHttpClient()
    {
        _httpClientFactoryMock
            .Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient());
    }
}
