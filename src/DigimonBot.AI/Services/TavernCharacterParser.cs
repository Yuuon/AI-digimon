using System.IO.Compression;
using System.Text;
using System.Text.Json;
using DigimonBot.Core.Models.Tavern;
using DigimonBot.Core.Services;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;

namespace DigimonBot.AI.Services;

/// <summary>
/// 酒馆角色卡解析器实现
/// </summary>
public class TavernCharacterParser : ITavernCharacterParser
{
    private readonly ILogger<TavernCharacterParser> _logger;

    public TavernCharacterParser(ILogger<TavernCharacterParser> logger)
    {
        _logger = logger;
    }

    public async Task<TavernCharacter?> ParseAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            _logger.LogWarning("角色卡文件不存在: {Path}", filePath);
            return null;
        }

        var extension = Path.GetExtension(filePath).ToLower();
        
        try
        {
            return extension switch
            {
                ".png" => await ParsePngAsync(filePath),
                ".json" => await ParseJsonAsync(filePath),
                _ => null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解析角色卡失败: {Path}", filePath);
            return null;
        }
    }

    public async Task<TavernCharacter?> ParsePngAsync(string filePath)
    {
        try
        {
            using var image = await Image.LoadAsync(filePath);
            
            // 尝试从 PNG 元数据中提取 chara 数据
            string? charaData = null;
            
            // 检查 Exif 中的 UserComment
            if (image.Metadata.ExifProfile != null)
            {
                if (image.Metadata.ExifProfile.TryGetValue(SixLabors.ImageSharp.Metadata.Profiles.Exif.ExifTag.UserComment, out var userComment))
                {
                    charaData = userComment.ToString();
                }
            }
            
            // 如果 Exif 中没有，尝试读取 PNG tEXt chunk（需要更底层的读取）
            if (string.IsNullOrEmpty(charaData))
            {
                charaData = ExtractCharaFromPng(filePath);
            }
            
            if (string.IsNullOrEmpty(charaData))
            {
                _logger.LogWarning("PNG 中没有找到角色卡数据: {Path}", filePath);
                return null;
            }
            
            // 尝试解码数据
            var jsonData = DecodeCharaData(charaData);
            if (string.IsNullOrEmpty(jsonData))
            {
                return null;
            }
            
            var character = ParseCharacterJson(jsonData);
            if (character != null)
            {
                character.SourcePath = filePath;
                character.IsPngCard = true;
                
                // 尝试提取封面图（Base64）
                using var ms = new MemoryStream();
                await image.SaveAsPngAsync(ms);
                character.CoverImage = Convert.ToBase64String(ms.ToArray());
            }
            
            return character;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解析 PNG 角色卡失败: {Path}", filePath);
            return null;
        }
    }

    public async Task<TavernCharacter?> ParseJsonAsync(string filePath)
    {
        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            var character = ParseCharacterJson(json);
            
            if (character != null)
            {
                character.SourcePath = filePath;
                character.IsPngCard = false;
            }
            
            return character;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解析 JSON 角色卡失败: {Path}", filePath);
            return null;
        }
    }

    public IEnumerable<string> GetCharacterFiles(string directory)
    {
        if (!Directory.Exists(directory))
        {
            _logger.LogWarning("角色卡目录不存在: {Directory}", directory);
            return Enumerable.Empty<string>();
        }

        var files = new List<string>();
        files.AddRange(Directory.GetFiles(directory, "*.png"));
        files.AddRange(Directory.GetFiles(directory, "*.json"));
        
        return files.Where(f => !f.EndsWith(".temp") && !f.EndsWith(".tmp"));
    }

    /// <summary>
    /// 从 PNG 文件中提取 chara 数据
    /// </summary>
    private string? ExtractCharaFromPng(string filePath)
    {
        try
        {
            using var fs = File.OpenRead(filePath);
            using var reader = new BinaryReader(fs);
            
            // 检查 PNG 签名
            var signature = reader.ReadBytes(8);
            if (!IsPngSignature(signature))
            {
                return null;
            }
            
            // 读取 PNG chunks
            while (fs.Position < fs.Length)
            {
                var length = ReadBigEndianInt32(reader);
                var chunkType = Encoding.ASCII.GetString(reader.ReadBytes(4));
                var data = reader.ReadBytes(length);
                var crc = reader.ReadBytes(4); // 跳过 CRC
                
                // 查找 tEXt chunk
                if (chunkType == "tEXt")
                {
                    var textData = Encoding.Latin1.GetString(data);
                    var separatorIndex = textData.IndexOf('\0');
                    
                    if (separatorIndex > 0)
                    {
                        var key = textData.Substring(0, separatorIndex);
                        var value = textData.Substring(separatorIndex + 1);
                        
                        if (key == "chara")
                        {
                            return value;
                        }
                    }
                }
                
                // 如果遇到 IDAT chunk，停止搜索（tEXt 通常在 IDAT 之前）
                if (chunkType == "IDAT")
                {
                    break;
                }
            }
            
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "提取 PNG chara 数据失败");
            return null;
        }
    }

    /// <summary>
    /// 解码 chara 数据（支持 base64 和 zlib）
    /// </summary>
    private string? DecodeCharaData(string data)
    {
        // 尝试直接解析为 JSON
        try
        {
            JsonDocument.Parse(data);
            return data;
        }
        catch { }
        
        // 尝试 Base64 解码
        try
        {
            var bytes = Convert.FromBase64String(data);
            var decoded = Encoding.UTF8.GetString(bytes);
            JsonDocument.Parse(decoded);
            return decoded;
        }
        catch { }
        
        // 尝试 Zlib 解压缩
        try
        {
            var bytes = Convert.FromBase64String(data);
            using var ms = new MemoryStream(bytes);
            using var zlib = new ZLibStream(ms, CompressionMode.Decompress);
            using var reader = new StreamReader(zlib);
            var decompressed = reader.ReadToEnd();
            JsonDocument.Parse(decompressed);
            return decompressed;
        }
        catch { }
        
        _logger.LogWarning("无法解码 chara 数据");
        return null;
    }

    /// <summary>
    /// 解析角色 JSON 数据
    /// </summary>
    private TavernCharacter? ParseCharacterJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            var character = new TavernCharacter();
            
            // 解析基本字段（支持大小写不敏感）
            character.Name = GetStringProperty(root, "name") ?? "Unknown";
            character.Description = GetStringProperty(root, "description") ?? "";
            character.Personality = GetStringProperty(root, "personality") ?? "";
            character.Scenario = GetStringProperty(root, "scenario") ?? "";
            character.FirstMessage = GetStringProperty(root, "first_mes") ?? "";
            character.MessageExample = GetStringProperty(root, "mes_example") ?? "";
            character.CreatorComment = GetStringProperty(root, "creatorcomment");
            character.Creator = GetStringProperty(root, "creator");
            character.CharacterVersion = GetStringProperty(root, "character_version") ?? GetStringProperty(root, "version");
            
            // 解析标签
            if (root.TryGetProperty("tags", out var tagsElement) && tagsElement.ValueKind == JsonValueKind.Array)
            {
                character.Tags = tagsElement.EnumerateArray()
                    .Select(t => t.GetString())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Cast<string>()
                    .ToList();
            }
            
            // 解析扩展数据
            if (root.TryGetProperty("extensions", out var extElement))
            {
                character.Extensions = new TavernExtensions();
                
                if (extElement.TryGetProperty("talkativeness", out var talkElement) && 
                    talkElement.TryGetDouble(out var talkValue))
                {
                    character.Extensions.Talkativeness = talkValue;
                }
                
                if (extElement.TryGetProperty("fav", out var favElement) && 
                    favElement.ValueKind == JsonValueKind.True)
                {
                    character.Extensions.Favorite = true;
                }
            }
            
            return character;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解析角色 JSON 失败");
            return null;
        }
    }

    private string? GetStringProperty(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var property))
        {
            return property.GetString();
        }
        
        // 尝试首字母大写
        var capitalized = char.ToUpper(propertyName[0]) + propertyName.Substring(1);
        if (element.TryGetProperty(capitalized, out var capitalizedProperty))
        {
            return capitalizedProperty.GetString();
        }
        
        return null;
    }

    private bool IsPngSignature(byte[] signature)
    {
        return signature.Length >= 8 &&
               signature[0] == 0x89 &&
               signature[1] == 0x50 &&
               signature[2] == 0x4E &&
               signature[3] == 0x47 &&
               signature[4] == 0x0D &&
               signature[5] == 0x0A &&
               signature[6] == 0x1A &&
               signature[7] == 0x0A;
    }

    private int ReadBigEndianInt32(BinaryReader reader)
    {
        var bytes = reader.ReadBytes(4);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }
        return BitConverter.ToInt32(bytes, 0);
    }
}
