using System.Text.Json.Serialization;

namespace DigimonBot.AI.Services;

/// <summary>
/// 情感分析结果 - 支持多种可能的字段名格式
/// </summary>
public class EmotionAnalysisResult
{
    // 标准 snake_case 格式
    [JsonPropertyName("courage_delta")]
    public int courage_delta { get; set; }
    
    [JsonPropertyName("friendship_delta")]
    public int friendship_delta { get; set; }
    
    [JsonPropertyName("love_delta")]
    public int love_delta { get; set; }
    
    [JsonPropertyName("knowledge_delta")]
    public int knowledge_delta { get; set; }
    
    [JsonPropertyName("reasoning")]
    public string reasoning { get; set; } = "";
    
    // 备用字段名（GLM可能返回不同格式）
    [JsonPropertyName("courage")]
    public int courage { get; set; }
    
    [JsonPropertyName("friendship")]
    public int friendship { get; set; }
    
    [JsonPropertyName("love")]
    public int love { get; set; }
    
    [JsonPropertyName("knowledge")]
    public int knowledge { get; set; }
    
    [JsonPropertyName("勇气")]
    public int 勇气 { get; set; }
    
    [JsonPropertyName("友情")]
    public int 友情 { get; set; }
    
    [JsonPropertyName("爱心")]
    public int 爱心 { get; set; }
    
    [JsonPropertyName("知识")]
    public int 知识 { get; set; }
    
    /// <summary>
    /// 获取勇气变化值（带范围限制）
    /// </summary>
    public int GetCourageDelta() => ValidateRange(courage_delta + courage + 勇气);
    
    /// <summary>
    /// 获取友情变化值（带范围限制）
    /// </summary>
    public int GetFriendshipDelta() => ValidateRange(friendship_delta + friendship + 友情);
    
    /// <summary>
    /// 获取爱心变化值（带范围限制）
    /// </summary>
    public int GetLoveDelta() => ValidateRange(love_delta + love + 爱心);
    
    /// <summary>
    /// 获取知识变化值（带范围限制）
    /// </summary>
    public int GetKnowledgeDelta() => ValidateRange(knowledge_delta + knowledge + 知识);
    
    private int ValidateRange(int value)
    {
        // 限制在 -10 到 10 范围内
        if (value < -10) return -10;
        if (value > 10) return 10;
        return value;
    }
}
