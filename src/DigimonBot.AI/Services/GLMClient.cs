using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using DigimonBot.Core.Models;
using Microsoft.Extensions.Logging;

namespace DigimonBot.AI.Services;

/// <summary>
/// 智谱GLM API客户端
/// 文档：https://open.bigmodel.cn/dev/api
/// </summary>
public class GLMClient : IAIClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GLMClient> _logger;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly string _baseUrl;

    public float Temperature { get; set; } = 0.8f;
    public int MaxTokens { get; set; } = 1000;

    public GLMClient(HttpClient httpClient, ILogger<GLMClient> logger, string apiKey, string model = "glm-4-flash", string? baseUrl = null)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = apiKey;
        _model = model;
        // 处理 null 和空字符串的情况
        _baseUrl = string.IsNullOrWhiteSpace(baseUrl) 
            ? "https://open.bigmodel.cn/api/paas/v4" 
            : baseUrl.TrimEnd('/');
        
        // GLM使用Bearer Token认证
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
    }

    public async Task<AIResponse> ChatAsync(List<ChatMessage> history, string systemPrompt)
    {
        var messages = new List<object>();
        
        // 2. 预处理历史对话：取最后10条
        var recentHistory = history.TakeLast(10).ToList();
        
        // 3. 关键修复：找到第一个 User 消息作为起点
        // GLM API 要求 messages 必须以 user 角色开始，不能以 assistant 开始
        int startIndex = 0;
        while (startIndex < recentHistory.Count && !recentHistory[startIndex].IsFromUser)
        {
            startIndex++;
        }
        
        // 4. 如果没有历史消息（新用户），把 systemPrompt 作为 user 消息发送
        if (recentHistory.Count == 0 || startIndex >= recentHistory.Count)
        {
            if (!string.IsNullOrWhiteSpace(systemPrompt))
            {
                messages.Add(new { role = "user", content = systemPrompt });
            }
        }
        else
        {
            // 有历史消息，先添加 system，再添加历史
            if (!string.IsNullOrWhiteSpace(systemPrompt))
            {
                messages.Add(new { role = "system", content = systemPrompt });
            }
            
            // 添加有效的历史对话
            for (int i = startIndex; i < recentHistory.Count; i++)
            {
                var msg = recentHistory[i];
                messages.Add(new 
                { 
                    role = msg.IsFromUser ? "user" : "assistant", 
                    content = msg.Content 
                });
            }
        }

        var request = new
        {
            model = _model,
            messages = messages,
            temperature = Temperature,
            max_tokens = MaxTokens,
            top_p = 0.7
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        try
        {
            _logger.LogDebug("Sending request to GLM API... Model: {Model}", _model);
            //_logger.LogInformation("Request URL: {Url}", $"{_baseUrl}/chat/completions");
            //_logger.LogInformation("Request Headers: {Headers}",
            //    string.Join(", ", _httpClient.DefaultRequestHeaders.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}")));
            //_logger.LogInformation("Request JSON: {Json}", json);
            var response = await _httpClient.PostAsync($"{_baseUrl}/chat/completions", content);
            
            var responseBody = await response.Content.ReadAsStringAsync();
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("GLM API error: {StatusCode}, {Body}", response.StatusCode, responseBody);
                response.EnsureSuccessStatusCode();
            }

            var result = JsonSerializer.Deserialize<GLMResponse>(responseBody);

            if (result?.Choices == null || result.Choices.Count == 0)
            {
                throw new InvalidOperationException("Invalid response from GLM API");
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
            _logger.LogError(ex, "Error calling GLM API");
            throw;
        }
    }

    public async Task<EmotionAnalysis> AnalyzeEmotionAsync(string userMessage, string aiResponse)
    {
        // _logger.LogDebug("开始情感分析...");
        
        // GLM 4.5/4.7 不支持 system 角色，使用 user 角色代替
        var prompt = $@"你是一个情感分析助手，专门分析用户与数码宝贝互动时的情感倾向。

分析以下对话中用户的情感倾向，判断哪些情感属性应该增加。

用户消息：{userMessage}
数码宝贝回复：{aiResponse}

请分析这段对话反映的用户行为倾向，并**严格**以JSON格式返回情感变化值（-10到10之间的整数）：
{{
    ""courage_delta"": 0,
    ""friendship_delta"": 0,
    ""love_delta"": 0,
    ""knowledge_delta"": 0,
    ""reasoning"": ""简要说明理由""
}}

要求：
1. 只返回JSON，不要添加任何其他文字说明
2. 正值表示该情感应该增加，负值表示减少
3. 单次变化幅度建议在1-5之间，特殊情况不超过10";

        var messages = new List<object>
        {
            new { role = "user", content = prompt }
        };

        var request = new
        {
            model = _model,
            messages = messages,
            temperature = 0.3,
            max_tokens = 500
        };

        try
        {
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync($"{_baseUrl}/chat/completions", content);
            // _logger.LogDebug("情感分析API响应状态: {Status}", response.StatusCode);
            
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            
            var result = JsonSerializer.Deserialize<GLMResponse>(responseJson);
            
            if (result?.Choices == null || result.Choices.Count == 0)
            {
                _logger.LogWarning("情感分析API返回空结果");
                return new EmotionAnalysis();
            }

            var analysisJson = result.Choices[0].Message.Content;
            
            // 尝试提取JSON（GLM有时会在JSON外添加说明文字或Markdown代码块）
            var extractedJson = ExtractJson(analysisJson);
            // _logger.LogDebug("提取JSON后: {Content}", extractedJson);
            
            // 如果内容为空，返回空分析
            if (string.IsNullOrWhiteSpace(extractedJson)) return new EmotionAnalysis();
            
            // 尝试修复可能的截断JSON
            var fixedJson = FixTruncatedJson(extractedJson);
            
            // 尝试解析JSON，如果失败则使用正则提取
            EmotionAnalysisResult? analysis = null;
            try
            {
                analysis = JsonSerializer.Deserialize<EmotionAnalysisResult>(fixedJson);
            }
            catch (JsonException)
            {
                analysis = ExtractEmotionFromText(fixedJson);
            }
            
            if (analysis == null) return new EmotionAnalysis();
            
            // 获取解析后的值（自动处理多种字段名格式和范围限制）
            var emotionResult = new EmotionAnalysis
            {
                CourageDelta = analysis.GetCourageDelta(),
                FriendshipDelta = analysis.GetFriendshipDelta(),
                LoveDelta = analysis.GetLoveDelta(),
                KnowledgeDelta = analysis.GetKnowledgeDelta(),
                Reasoning = analysis.reasoning ?? ""
            };
            
            // _logger.LogDebug("情感分析结果: C={C}, F={F}, L={L}, K={K}",
            //     emotionResult.CourageDelta, emotionResult.FriendshipDelta, emotionResult.LoveDelta, emotionResult.KnowledgeDelta);
            
            // 保底措施：如果所有值都是0，随机给一个情感加 1-2 点
            if (emotionResult.CourageDelta == 0 && emotionResult.FriendshipDelta == 0 && 
                emotionResult.LoveDelta == 0 && emotionResult.KnowledgeDelta == 0)
            {
                var random = new Random();
                var emotionType = random.Next(4);
                var bonus = random.Next(1, 3);
                
                switch (emotionType)
                {
                    case 0: emotionResult.CourageDelta = bonus; break;
                    case 1: emotionResult.FriendshipDelta = bonus; break;
                    case 2: emotionResult.LoveDelta = bonus; break;
                    case 3: emotionResult.KnowledgeDelta = bonus; break;
                }
                
                // _logger.LogDebug("保底加成: {Emotion} +{Value}",
                //     emotionType switch { 0 => "C", 1 => "F", 2 => "L", _ => "K" }, bonus);
            }
            
            return emotionResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "情感分析异常: {Message}", ex.Message);
            _logger.LogError("异常堆栈: {StackTrace}", ex.StackTrace);
            // 返回空分析，不影响正常对话
            return new EmotionAnalysis();
        }
    }

    /// <summary>
    /// 从文本中提取JSON（处理Markdown代码块等格式）
    /// </summary>
    private string ExtractJson(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;
        
        // 1. 去除 Markdown 代码块标记 ```json ... ```
        if (text.Contains("```"))
        {
            // 找到第一个 ``` 之后的内容
            var startMarker = text.IndexOf("```");
            if (startMarker >= 0)
            {
                // 跳过 ```json 或 ``` 行
                var contentStart = text.IndexOf('\n', startMarker);
                if (contentStart > 0)
                {
                    // 找到结束的 ```
                    var endMarker = text.LastIndexOf("```");
                    if (endMarker > contentStart)
                    {
                        text = text.Substring(contentStart + 1, endMarker - contentStart - 1).Trim();
                    }
                }
            }
        }
        
        // 2. 去除行内代码标记 `...`
        text = text.Trim();
        if (text.StartsWith("`") && text.EndsWith("`"))
        {
            text = text.Trim('`').Trim();
        }
        
        // 3. 查找JSON对象的开始和结束位置
        var startIdx = text.IndexOf('{');
        var endIdx = text.LastIndexOf('}');
        
        if (startIdx >= 0 && endIdx > startIdx)
        {
            return text.Substring(startIdx, endIdx - startIdx + 1);
        }
        
        return text;
    }
    
    /// <summary>
    /// 尝试修复可能被截断的 JSON
    /// </summary>
    private string FixTruncatedJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return json;
        
        json = json.Trim();
        
        // 检查 JSON 是否完整（以 } 结尾）
        if (json.EndsWith("}"))
            return json;
        
        // 尝试找到最后一个完整的键值对并补全
        var lastBrace = json.LastIndexOf('}');
        if (lastBrace < 0)
        {
            // 完全没有结束括号，尝试补全结构
            // 计算有多少个开始括号
            var openBraces = json.Count(c => c == '{');
            var closeBraces = json.Count(c => c == '}');
            
            for (int i = 0; i < openBraces - closeBraces; i++)
            {
                json += "}";
            }
        }
        
        _logger.LogDebug("Fixed JSON: {Json}", json);
        return json;
    }
    
    /// <summary>
    /// 使用正则表达式从文本中提取情感值
    /// </summary>
    private EmotionAnalysisResult ExtractEmotionFromText(string text)
    {
        var result = new EmotionAnalysisResult();
        
        try
        {
            // 匹配各种可能的格式：courage_delta: 5, "courage_delta": 5, courage: 5, 勇气: 5 等
            var courageMatch = Regex.Match(text, @"(courage_delta|courage|勇气)[""']?\s*[:=]\s*(-?\d+)");
            if (courageMatch.Success)
                result.courage_delta = int.Parse(courageMatch.Groups[2].Value);
            
            var friendshipMatch = Regex.Match(text, @"(friendship_delta|friendship|友情)[""']?\s*[:=]\s*(-?\d+)");
            if (friendshipMatch.Success)
                result.friendship_delta = int.Parse(friendshipMatch.Groups[2].Value);
            
            var loveMatch = Regex.Match(text, @"(love_delta|love|爱心)[""']?\s*[:=]\s*(-?\d+)");
            if (loveMatch.Success)
                result.love_delta = int.Parse(loveMatch.Groups[2].Value);
            
            var knowledgeMatch = Regex.Match(text, @"(knowledge_delta|knowledge|知识)[""']?\s*[:=]\s*(-?\d+)");
            if (knowledgeMatch.Success)
                result.knowledge_delta = int.Parse(knowledgeMatch.Groups[2].Value);
            
            _logger.LogInformation("Extracted emotions from text: Courage={C}, Friendship={F}, Love={L}, Knowledge={K}",
                result.courage_delta, result.friendship_delta, result.love_delta, result.knowledge_delta);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract emotions from text");
        }
        
        return result;
    }
}

// GLM API响应模型
public class GLMResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";
    
    [JsonPropertyName("created")]
    public long Created { get; set; }
    
    [JsonPropertyName("model")]
    public string Model { get; set; } = "";
    
    [JsonPropertyName("choices")]
    public List<GLMChoice> Choices { get; set; } = new();
    
    [JsonPropertyName("usage")]
    public GLMUsage? Usage { get; set; }
}

public class GLMChoice
{
    [JsonPropertyName("index")]
    public int Index { get; set; }
    
    [JsonPropertyName("message")]
    public GLMMessage Message { get; set; } = new();
    
    [JsonPropertyName("finish_reason")]
    public string FinishReason { get; set; } = "";
}

public class GLMMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "";
    
    [JsonPropertyName("content")]
    public string Content { get; set; } = "";
}

public class GLMUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }
    
    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }
    
    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}
