namespace DigimonBot.Core.Services;

/// <summary>
/// 图片上传服务接口 - 将图片上传到图床获取公网URL
/// </summary>
public interface IImageUploadService
{
    /// <summary>
    /// 上传图片到图床
    /// </summary>
    /// <param name="imageUrl">原始图片URL（本地/内网地址）</param>
    /// <returns>公网可访问的图片URL，失败返回null</returns>
    Task<string?> UploadImageAsync(string imageUrl);
}
