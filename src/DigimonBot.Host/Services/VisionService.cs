using System.Diagnostics;
using System.Text;
using System.Text.Json;
using DigimonBot.AI.Services;
using DigimonBot.Host.Configs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace DigimonBot.Host.Services;

/// <summary>
/// 识图服务实现 - 使用图床+HttpClient
/// </summary>
public class VisionService : IVisionService
{
    private readonly HttpClient _httpClient;
    private readonly AIConfig _aiConfig;
    private readonly ILogger<VisionService> _logger;

    public VisionService(
        HttpClient httpClient,
        IOptions<AppSettings> settings,
        ILogger<VisionService> logger)
    {
        _httpClient = httpClient;
        _aiConfig = settings.Value.AI;
        _logger = logger;
    }

    public bool IsAvailable => _aiConfig.VisionModel?.Enabled == true;

    public async Task<string> AnalyzeImageAsync(string imageUrl, string prompt = "这是什么？请详细描述。")
    {
        if (!IsAvailable)
        {
            throw new InvalidOperationException("识图模型未配置");
        }

        var visionConfig = _aiConfig.VisionModel!;
        var apiKey = !string.IsNullOrEmpty(visionConfig.ApiKey) 
            ? visionConfig.ApiKey 
            : _aiConfig.ApiKey;

        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException("API密钥未配置");
        }

        try
        {
            _logger.LogInformation("[VisionService] 开始处理图片");

            // 1. 下载并压缩图片
            var (compressedPath, originalSize, compressedSize) = await DownloadAndCompressImageAsync(imageUrl);
            
            if (string.IsNullOrEmpty(compressedPath) || !File.Exists(compressedPath))
            {
                return "❌ 图片处理失败";
            }

            _logger.LogInformation("[VisionService] 图片压缩: {Original}KB -> {Compressed}KB", 
                originalSize / 1024, compressedSize / 1024);

            try
            {
                // 2. 上传到 catbox 获取公网URL
                var publicUrl = await UploadToCatboxAsync(compressedPath);
                
                if (string.IsNullOrEmpty(publicUrl))
                {
                    // 如果上传失败且图片较小，使用 base64
                    if (compressedSize < 200_000)
                    {
                        _logger.LogWarning("[VisionService] 图床上传失败，尝试base64方式");
                        return await AnalyzeWithBase64Async(compressedPath, prompt, apiKey, visionConfig);
                    }
                    return "❌ 图片上传失败，请发送更小的图片";
                }

                _logger.LogInformation("[VisionService] 获取到公网URL: {Url}", publicUrl);

                // 3. 使用 HttpClient 调用 GLM API
                return await CallVisionApiAsync(publicUrl, prompt, apiKey, visionConfig);
            }
            finally
            {
                // 清理临时文件
                try
                {
                    if (File.Exists(compressedPath))
                    {
                        File.Delete(compressedPath);
                        _logger.LogDebug("[VisionService] 已清理临时文件: {Path}", compressedPath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[VisionService] 清理临时文件失败");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[VisionService] 识图请求失败");
            return $"❌ 图片分析出错: {ex.Message}";
        }
    }

    /// <summary>
    /// 下载图片并压缩保存到临时文件
    /// </summary>
    private async Task<(string? Path, int OriginalSize, int CompressedSize)> DownloadAndCompressImageAsync(string imageUrl)
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);
            
            var imageBytes = await httpClient.GetByteArrayAsync(imageUrl);
            var originalSize = imageBytes.Length;
            
            _logger.LogInformation("[VisionService] 下载图片: {Size}KB", originalSize / 1024);

            using var inputStream = new MemoryStream(imageBytes);
            using var image = await Image.LoadAsync(inputStream);

            // 最大尺寸 1024x1024
            var maxSize = 1024;
            if (image.Width > maxSize || image.Height > maxSize)
            {
                var ratio = Math.Min((double)maxSize / image.Width, (double)maxSize / image.Height);
                image.Mutate(x => x.Resize((int)(image.Width * ratio), (int)(image.Height * ratio)));
            }

            var tempPath = Path.Combine(Path.GetTempPath(), $"vision_{Guid.NewGuid():N}.jpg");
            await image.SaveAsync(tempPath, new JpegEncoder { Quality = 85 });
            
            var compressedSize = (int)new FileInfo(tempPath).Length;
            
            return (tempPath, originalSize, compressedSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[VisionService] 下载或压缩图片失败");
            return (null, 0, 0);
        }
    }

    /// <summary>
    /// 上传到 catbox.moe
    /// </summary>
    private async Task<string?> UploadToCatboxAsync(string filePath)
    {
        try
        {
            _logger.LogInformation("[VisionService] 使用curl上传图片到catbox.moe...");
            
            if (!File.Exists(filePath))
            {
                _logger.LogError("[VisionService] 文件不存在: {Path}", filePath);
                return null;
            }

            var psi = new ProcessStartInfo
            {
                FileName = "curl",
                Arguments = $"-s --max-time 60 -F \"reqtype=fileupload\" -F \"time=24h\" -F \"fileToUpload=@{filePath}\" https://litterbox.catbox.moe/resources/internals/api.php",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return null;

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            _logger.LogInformation("[VisionService] catbox结果: ExitCode={Code}, Output={Output}", process.ExitCode, output.Trim());

            if (process.ExitCode != 0) return null;

            var url = output.Trim();
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && 
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                _logger.LogInformation("[VisionService] catbox返回URL: {Url}", url);
                return url;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[VisionService] 上传到catbox失败");
            return null;
        }
    }

    /// <summary>
    /// 使用 HttpClient 调用 GLM Vision API
    /// </summary>
    private async Task<string> CallVisionApiAsync(string imageUrl, string prompt, string apiKey, VisionConfig config)
    {
        try
        {
            var requestBody = new
            {
                model = config.Model,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new { type = "text", text = prompt },
                            new { type = "image_url", image_url = new { url = imageUrl } }
                        }
                    }
                },
                max_tokens = 1000
            };

            var json = JsonSerializer.Serialize(requestBody);
            
            using var request = new HttpRequestMessage(HttpMethod.Post, config.BaseUrl);
            request.Headers.Add("Authorization", $"Bearer {apiKey}");
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogInformation("[VisionService] 发送识图请求到: {Url}", config.BaseUrl);

            using var response = await _httpClient.SendAsync(request);
            var responseJson = await response.Content.ReadAsStringAsync();
            
            _logger.LogDebug("[VisionService] GLM响应: {Response}", 
                responseJson.Substring(0, Math.Min(500, responseJson.Length)));

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("[VisionService] 识图请求失败: {Status}, {Body}", 
                    response.StatusCode, responseJson);
                return $"❌ 识图请求失败: {response.StatusCode}";
            }

            return ParseVisionResponse(responseJson);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[VisionService] API调用失败");
            return $"❌ API调用失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 使用 base64 方式分析（备用方案）
    /// </summary>
    private async Task<string> AnalyzeWithBase64Async(string imagePath, string prompt, string apiKey, VisionConfig config)
    {
        try
        {
            var imageBytes = await File.ReadAllBytesAsync(imagePath);
            var base64 = Convert.ToBase64String(imageBytes);
            var dataUrl = $"data:image/jpeg;base64,{base64}";

            _logger.LogInformation("[VisionService] 使用base64方式, 长度: {Length}", dataUrl.Length);

            return await CallVisionApiAsync(dataUrl, prompt, apiKey, config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[VisionService] base64分析失败");
            return $"❌ 分析失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 解析识图响应 - 修复：GLM-4.6v 可能只有 reasoning_content
    /// </summary>
    private string ParseVisionResponse(string responseJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            // 检查错误
            if (root.TryGetProperty("error", out var error))
            {
                var errorMsg = error.ValueKind == JsonValueKind.Object 
                    ? (error.TryGetProperty("message", out var msg) ? msg.GetString() : error.ToString())
                    : error.GetString() ?? "未知错误";
                _logger.LogError("[VisionService] API返回错误: {Error}", errorMsg);
                return $"❌ API错误: {errorMsg}";
            }

            // 解析 choices
            if (root.TryGetProperty("choices", out var choices) && 
                choices.ValueKind == JsonValueKind.Array && 
                choices.GetArrayLength() > 0)
            {
                var firstChoice = choices[0];
                if (firstChoice.ValueKind == JsonValueKind.Object &&
                    firstChoice.TryGetProperty("message", out var message) && 
                    message.ValueKind == JsonValueKind.Object)
                {
                    // 优先取 content
                    if (message.TryGetProperty("content", out var content) &&
                        content.ValueKind == JsonValueKind.String)
                    {
                        var text = content.GetString();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            _logger.LogInformation("[VisionService] 成功解析content");
                            return text;
                        }
                    }
                    
                    // 如果 content 为空，取 reasoning_content (GLM-4.6v thinking模式)
                    if (message.TryGetProperty("reasoning_content", out var reasoning) &&
                        reasoning.ValueKind == JsonValueKind.String)
                    {
                        var text = reasoning.GetString();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            _logger.LogInformation("[VisionService] 使用reasoning_content");
                            return text;
                        }
                    }
                }
            }

            _logger.LogWarning("[VisionService] 无法解析响应: {Response}", responseJson);
            return $"⚠️ 无法解析响应: {responseJson.Substring(0, Math.Min(200, responseJson.Length))}";
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "[VisionService] JSON解析失败");
            return $"❌ 响应解析失败: {ex.Message}";
        }
    }
}

/// <summary>
/// JSON文档扩展方法
/// </summary>
public static class JsonDocumentExtensions
{
    public static string? GetString(this JsonElement element)
    {
        return element.ValueKind == JsonValueKind.String 
            ? element.GetString() 
            : element.ToString();
    }
}
