using DigimonBot.Core.Models;

namespace DigimonBot.AI.Services;

/// <summary>
/// AI客户端接口
/// </summary>
public interface IAIClient
{
    /// <summary>
    /// 发送对话请求并获取回复
    /// </summary>
    Task<AIResponse> ChatAsync(List<ChatMessage> history, string systemPrompt);
    
    /// <summary>
    /// 分析对话情感（用于进化系统）
    /// </summary>
    Task<EmotionAnalysis> AnalyzeEmotionAsync(string userMessage, string aiResponse);
}

/// <summary>
/// AI回复结果
/// </summary>
public class AIResponse
{
    public string Content { get; set; } = "";
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens => PromptTokens + CompletionTokens;
}
