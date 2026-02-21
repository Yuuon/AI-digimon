namespace DigimonBot.AI.Services;

/// <summary>
/// 识图服务接口
/// </summary>
public interface IVisionService
{
    /// <summary>
    /// 分析图片内容
    /// </summary>
    /// <param name="imageUrl">图片URL</param>
    /// <param name="prompt">分析提示词</param>
    /// <returns>分析结果</returns>
    Task<string> AnalyzeImageAsync(string imageUrl, string prompt = "这是什么？请详细描述。");
    
    /// <summary>
    /// 是否可用（已配置识图模型）
    /// </summary>
    bool IsAvailable { get; }
}
