using DigimonBot.Core.Services;
using DigimonBot.Host.Configs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace DigimonBot.Host.Services;

/// <summary>
/// 图片处理服务 - 压缩图片并转为Base64
/// 由于NapCat返回的是内网地址，无法使用图床，只能直接传Base64给AI
/// </summary>
public class ImageUploadService : IImageUploadService
{
    private readonly ILogger<ImageUploadService> _logger;
    private readonly ImageProcessingConfig _imageConfig;

    public ImageUploadService(
        ILogger<ImageUploadService> logger,
        IOptions<AppSettings> settings)
    {
        _logger = logger;
        _imageConfig = settings.Value.ImageProcessing;
    }

    /// <summary>
    /// 处理图片：下载、压缩、转Base64
    /// </summary>
    public async Task<string?> UploadImageAsync(string imageUrl)
    {
        try
        {
            _logger.LogInformation("[ImageUpload] 开始处理图片: {Url}", imageUrl);

            // 1. 下载图片
            var imageBytes = await DownloadImageAsync(imageUrl);
            if (imageBytes == null || imageBytes.Length == 0)
            {
                _logger.LogError("[ImageUpload] 下载图片失败");
                return null;
            }

            _logger.LogInformation("[ImageUpload] 原始图片大小: {Size} bytes", imageBytes.Length);

            // 2. 压缩图片（目标：小于配置的最大大小）
            var compressedBytes = await CompressImageAsync(imageBytes, maxSizeInBytes: _imageConfig.MaxBase64SizeBytes);
            
            if (compressedBytes == null || compressedBytes.Length == 0)
            {
                _logger.LogError("[ImageUpload] 图片压缩失败");
                return null;
            }

            _logger.LogInformation("[ImageUpload] 压缩后大小: {Size} bytes", compressedBytes.Length);

            // 3. 转为 Base64
            var base64 = Convert.ToBase64String(compressedBytes);
            _logger.LogInformation("[ImageUpload] Base64长度: {Length} chars", base64.Length);

            // 4. 构建 Data URL
            var dataUrl = $"data:image/jpeg;base64,{base64}";
            
            return dataUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ImageUpload] 处理图片失败");
            return null;
        }
    }

    /// <summary>
    /// 下载图片
    /// </summary>
    private async Task<byte[]?> DownloadImageAsync(string imageUrl)
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(_imageConfig.DownloadTimeoutSeconds);
            
            var response = await httpClient.GetAsync(imageUrl);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("[ImageUpload] 下载图片失败: {Status}", response.StatusCode);
                return null;
            }

            return await response.Content.ReadAsByteArrayAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ImageUpload] 下载图片异常");
            return null;
        }
    }

    /// <summary>
    /// 压缩图片到指定大小以下
    /// </summary>
    private async Task<byte[]?> CompressImageAsync(byte[] imageBytes, int maxSizeInBytes)
    {
        try
        {
            // 如果已经够小，直接返回
            if (imageBytes.Length <= maxSizeInBytes)
            {
                return imageBytes;
            }

            using var inputStream = new MemoryStream(imageBytes);
            using var image = await Image.LoadAsync(inputStream);

            // 计算目标尺寸
            var maxDimension = _imageConfig.UploadMaxDimension;
            if (image.Width > maxDimension || image.Height > maxDimension)
            {
                var ratio = Math.Min((double)maxDimension / image.Width, (double)maxDimension / image.Height);
                var newWidth = (int)(image.Width * ratio);
                var newHeight = (int)(image.Height * ratio);
                
                image.Mutate(x => x.Resize(newWidth, newHeight));
                _logger.LogInformation("[ImageUpload] 图片缩放至: {Width}x{Height}", newWidth, newHeight);
            }

            // 使用二分法找到合适的压缩质量
            byte[]? result = null;
            var minQuality = _imageConfig.MinCompressionQuality;
            var maxQuality = _imageConfig.MaxCompressionQuality;
            
            while (minQuality <= maxQuality)
            {
                var quality = (minQuality + maxQuality) / 2;
                
                using var outputStream = new MemoryStream();
                var encoder = new JpegEncoder { Quality = quality };
                await image.SaveAsync(outputStream, encoder);
                
                var compressed = outputStream.ToArray();
                
                if (compressed.Length <= maxSizeInBytes)
                {
                    // 满足条件，保存结果，尝试更高质量
                    result = compressed;
                    minQuality = quality + 1;
                }
                else
                {
                    // 太大，降低质量
                    maxQuality = quality - 1;
                }
            }

            if (result != null)
            {
                _logger.LogInformation("[ImageUpload] 压缩成功: {Size} bytes (质量: {Quality}%)", 
                    result.Length, maxQuality);
                return result;
            }

            // 如果即使最低质量还是太大，返回最低质量的结果
            using var finalStream = new MemoryStream();
            var finalEncoder = new JpegEncoder { Quality = _imageConfig.MinCompressionQuality };
            await image.SaveAsync(finalStream, finalEncoder);
            
            _logger.LogWarning("[ImageUpload] 即使{Quality}%质量仍超过限制: {Size} bytes", _imageConfig.MinCompressionQuality, finalStream.Length);
            return finalStream.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ImageUpload] 压缩图片失败");
            return null;
        }
    }
}
