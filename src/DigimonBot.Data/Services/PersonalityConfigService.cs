using System.Text.Json;
using System.Text.Json.Serialization;
using DigimonBot.Core.Models;
using DigimonBot.Core.Services;
using Microsoft.Extensions.Logging;

namespace DigimonBot.Data.Services;

/// <summary>
/// 性格配置服务实现
/// </summary>
public class PersonalityConfigService : IPersonalityConfigService
{
    private DigimonPersonalityConfig _config = new();
    private readonly string _configFilePath;
    private readonly ILogger<PersonalityConfigService> _logger;
    private readonly object _lock = new();

    public DigimonPersonalityConfig Config
    {
        get
        {
            lock (_lock)
            {
                return _config;
            }
        }
    }

    public event EventHandler? OnConfigChanged;

    public PersonalityConfigService(ILogger<PersonalityConfigService> logger, string? configFilePath = null)
    {
        _logger = logger;
        _configFilePath = configFilePath ?? DigimonPersonalityConfig.DefaultConfigPath;

        // 尝试加载配置，如果不存在则创建默认配置
        if (!File.Exists(_configFilePath))
        {
            _logger.LogInformation("性格配置文件不存在，创建默认配置: {Path}", _configFilePath);
            CreateDefaultConfig();
        }
        else
        {
            LoadConfig(_configFilePath);
        }
    }

    public bool LoadConfig(string? filePath = null)
    {
        var path = filePath ?? _configFilePath;

        try
        {
            if (!File.Exists(path))
            {
                _logger.LogWarning("性格配置文件不存在: {Path}", path);
                return false;
            }

            var json = File.ReadAllText(path);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };

            var config = JsonSerializer.Deserialize<DigimonPersonalityConfig>(json, options);
            if (config == null)
            {
                _logger.LogError("反序列化性格配置文件失败: {Path}", path);
                return false;
            }

            lock (_lock)
            {
                _config = config;
            }

            _logger.LogInformation("性格配置已加载: {Path}", path);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载性格配置文件失败: {Path}", path);
            return false;
        }
    }

    public bool SaveConfig(string? filePath = null)
    {
        var path = filePath ?? _configFilePath;

        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            lock (_lock)
            {
                var json = JsonSerializer.Serialize(_config, options);
                File.WriteAllText(path, json);
            }

            _logger.LogInformation("性格配置已保存: {Path}", path);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存性格配置文件失败: {Path}", path);
            return false;
        }
    }

    public bool ReloadConfig()
    {
        _logger.LogInformation("重新加载性格配置...");

        if (LoadConfig(_configFilePath))
        {
            OnConfigChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        return false;
    }

    public string GetConfigFilePath()
    {
        return Path.GetFullPath(_configFilePath);
    }

    public PersonalityDefinition? GetPersonality(string personalityKey)
    {
        lock (_lock)
        {
            if (_config.Personalities.TryGetValue(personalityKey, out var personality))
            {
                return personality;
            }
            return null;
        }
    }

    public string GetPersonalityPrompt(string personalityKey)
    {
        var personality = GetPersonality(personalityKey);
        return personality?.SystemPrompt ?? "";
    }

    public Dictionary<string, PersonalityDefinition> GetAllPersonalities()
    {
        lock (_lock)
        {
            return new Dictionary<string, PersonalityDefinition>(_config.Personalities);
        }
    }

    public bool PersonalityExists(string personalityKey)
    {
        lock (_lock)
        {
            return _config.Personalities.ContainsKey(personalityKey);
        }
    }

    public string GetDefaultPersonality()
    {
        lock (_lock)
        {
            // 如果默认性格不存在，返回第一个可用的性格
            if (!_config.Personalities.ContainsKey(_config.DefaultPersonality))
            {
                return _config.Personalities.Keys.FirstOrDefault() ?? "Brave";
            }
            return _config.DefaultPersonality;
        }
    }

    private void CreateDefaultConfig()
    {
        _config = new DigimonPersonalityConfig
        {
            DefaultPersonality = "Brave",
            Personalities = new Dictionary<string, PersonalityDefinition>
            {
                ["Brave"] = new()
                {
                    Name = "勇敢",
                    Description = "性格非常勇敢，说话直接果断，不畏惧挑战",
                    SystemPrompt = "你的性格非常勇敢，说话直接果断，不畏惧挑战。常用表达：'没关系，交给我！''这点困难不算什么！''为了守护大家，我会变强！'语气坚定，充满斗志，偶尔会说出鼓舞人心的话。",
                    AffinityEmotion = "Courage"
                },
                ["Friendly"] = new()
                {
                    Name = "友善",
                    Description = "性格非常友善，重视伙伴关系，说话温和亲切",
                    SystemPrompt = "你的性格非常友善，重视伙伴关系，说话温和亲切。常用表达：'我们是好朋友对吧！''一起努力吧！''谢谢你一直陪着我。'语气温暖，经常关心对方，喜欢说鼓励的话。",
                    AffinityEmotion = "Friendship"
                },
                ["Gentle"] = new()
                {
                    Name = "温柔",
                    Description = "性格非常温柔，富有同情心，说话轻声细语",
                    SystemPrompt = "你的性格非常温柔，富有同情心，说话轻声细语。常用表达：'你没事吧？''不要勉强自己哦。''让我来治愈你吧~'语气柔和，经常关心对方的感受，会主动安慰人。",
                    AffinityEmotion = "Love"
                },
                ["Curious"] = new()
                {
                    Name = "好奇",
                    Description = "性格充满好奇心，喜欢探索和学习，说话充满求知欲",
                    SystemPrompt = "你的性格充满好奇心，喜欢探索和学习，说话充满求知欲。常用表达：'这是为什么呀？''好有趣，告诉我更多！''我们一起来研究吧！'语气活泼，经常提问，喜欢分享有趣的知识。",
                    AffinityEmotion = "Knowledge"
                },
                ["Mischievous"] = new()
                {
                    Name = "调皮",
                    Description = "性格调皮捣蛋，喜欢开玩笑，说话活泼俏皮",
                    SystemPrompt = "你的性格调皮捣蛋，喜欢开玩笑，说话活泼俏皮。常用表达：'嘿嘿，被我骗到了吧？''来玩嘛来玩嘛~''这可有趣了！'语气轻快，喜欢恶作剧，但心地善良。",
                    AffinityEmotion = null
                },
                ["Calm"] = new()
                {
                    Name = "冷静",
                    Description = "性格冷静理性，善于分析，说话条理清晰",
                    SystemPrompt = "你的性格冷静理性，善于分析，说话条理清晰。常用表达：'让我想想...''根据我的分析...''冷静点，我们能解决的。'语气平稳，不冲动，经常给出合理的建议。",
                    AffinityEmotion = null
                }
            }
        };
        SaveConfig(_configFilePath);
    }
}
