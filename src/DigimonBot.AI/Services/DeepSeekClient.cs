using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DigimonBot.Core.Models;
using Microsoft.Extensions.Logging;

namespace DigimonBot.AI.Services;

/// <summary>
/// DeepSeek API客户端（兼容OpenAI格式）
/// </summary>
public class DeepSeekClient : IAIClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DeepSeekClient> _logger;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly string _baseUrl;

    public float Temperature { get; set; } = 0.8f;
    public int MaxTokens { get; set; } = 1000;

    public DeepSeekClient(HttpClient httpClient, ILogger<DeepSeekClient> logger, string apiKey, string model = "deepseek-chat", string? baseUrl = null)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = apiKey;
        _model = model;
        // 处理 null 和空字符串的情况
        _baseUrl = string.IsNullOrWhiteSpace(baseUrl) 
            ? "https://api.deepseek.com" 
            : baseUrl.TrimEnd('/');
        
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
    }

    public async Task<AIResponse> ChatAsync(List<ChatMessage> history, string systemPrompt)
    {
        var messages = new List<object>();
        
        // 添加系统提示词
        messages.Add(new { role = "system", content = systemPrompt });
        
        // 添加历史对话
        foreach (var msg in history.TakeLast(10)) // 只保留最近10轮
        {
            messages.Add(new 
            { 
                role = msg.IsFromUser ? "user" : "assistant", 
                content = msg.Content 
            });
        }

        var request = new
        {
            model = _model,
            messages = messages,
            temperature = Temperature,
            max_tokens = MaxTokens
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            _logger.LogDebug("Sending request to DeepSeek API...");
            var response = await _httpClient.PostAsync($"{_baseUrl}/v1/chat/completions", content);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<OpenAIResponse>(responseJson);

            if (result?.Choices == null || result.Choices.Count == 0)
            {
                throw new InvalidOperationException("Invalid response from AI API");
            }

            return new AIResponse
            {
                Content = result.Choices[0].Message.Content,
                PromptTokens = result.Usage?.PromptTokens ?? 0,
                CompletionTokens = result.Usage?.CompletionTokens ?? 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling DeepSeek API");
            throw;
        }
    }

    public async Task<EmotionAnalysis> AnalyzeEmotionAsync(string userMessage, string aiResponse)
    {
        var prompt = $@"分析以下对话中用户的情感倾向，判断哪些情感属性应该增加。

用户消息：{userMessage}
数码宝贝回复：{aiResponse}

请分析这段对话反映的用户行为倾向，并以JSON格式返回情感变化值（-10到10之间的整数）：
{{
    ""courage_delta"": 0,      // 勇气：用户是否表现出勇敢、挑战、保护等行为
    ""friendship_delta"": 0,    // 友情：用户是否表现出合作、关心、陪伴等
    ""love_delta"": 0,          // 爱心：用户是否表现出温柔、治愈、体贴等
    ""knowledge_delta"": 0,     // 知识：用户是否表现出学习、探索、提问等
    ""reasoning"": ""简要说明理由""
}}

注意：
1. 正值表示该情感应该增加，负值表示减少（极少情况）
2. 单次变化幅度建议在1-5之间，特殊情况不超过10
3. 基于对话内容自然分析，不要强行分配数值";

        var messages = new List<object>
        {
            new { role = "system", content = "你是一个情感分析助手，专门分析用户与数码宝贝互动时的情感倾向。" },
            new { role = "user", content = prompt }
        };

        var request = new
        {
            model = _model,
            messages = messages,
            temperature = 0.3,
            max_tokens = 500,
            response_format = new { type = "json_object" }
        };

        try
        {
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync($"{_baseUrl}/v1/chat/completions", content);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<OpenAIResponse>(responseJson);
            
            if (result?.Choices == null || result.Choices.Count == 0)
            {
                return new EmotionAnalysis();
            }

            var analysisJson = result.Choices[0].Message.Content;
            var analysis = JsonSerializer.Deserialize<EmotionAnalysisResult>(analysisJson);
            
            if (analysis == null)
            {
                return new EmotionAnalysis();
            }

            return new EmotionAnalysis
            {
                CourageDelta = analysis.GetCourageDelta(),
                FriendshipDelta = analysis.GetFriendshipDelta(),
                LoveDelta = analysis.GetLoveDelta(),
                KnowledgeDelta = analysis.GetKnowledgeDelta(),
                Reasoning = analysis.reasoning ?? ""
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing emotion");
            // 返回空分析，不影响正常对话
            return new EmotionAnalysis();
        }
    }
}

// OpenAI API响应模型
public class OpenAIResponse
{
    [JsonPropertyName("choices")]
    public List<Choice> Choices { get; set; } = new();
    
    [JsonPropertyName("usage")]
    public UsageInfo? Usage { get; set; }
}

public class Choice
{
    [JsonPropertyName("message")]
    public Message Message { get; set; } = new();
}

public class Message
{
    [JsonPropertyName("content")]
    public string Content { get; set; } = "";
}

public class UsageInfo
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }
    
    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }
}

// EmotionAnalysisResult 已移到 EmotionAnalysisModels.cs
