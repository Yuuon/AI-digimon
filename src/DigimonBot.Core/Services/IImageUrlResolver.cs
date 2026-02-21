namespace DigimonBot.Core.Services;

/// <summary>
/// 图片URL解析服务接口
/// </summary>
public interface IImageUrlResolver
{
    /// <summary>
    /// 根据文件标识获取图片的真实下载URL
    /// </summary>
    /// <param name="file">图片文件标识</param>
    /// <returns>可访问的图片URL，如果获取失败返回null</returns>
    Task<string?> ResolveImageUrlAsync(string file);
}
