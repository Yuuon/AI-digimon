using System.Text.Json;
using System.Text.Json.Serialization;
using DigimonBot.Core.Models;
using DigimonBot.Core.Services;
using Microsoft.Extensions.Logging;

namespace DigimonBot.Data.Services;

/// <summary>
/// 数码兽对话配置服务实现
/// </summary>
public class DialogueConfigService : IDialogueConfigService
{
    private DigimonDialogueConfig _config = new();
    private readonly string _configFilePath;
    private readonly ILogger<DialogueConfigService> _logger;
    private readonly object _lock = new();

    public DigimonDialogueConfig Config
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

    public DialogueConfigService(ILogger<DialogueConfigService> logger, string? configFilePath = null)
    {
        _logger = logger;
        _configFilePath = configFilePath ?? DigimonDialogueConfig.DefaultConfigPath;

        // 尝试加载配置，如果不存在则创建默认配置
        if (!File.Exists(_configFilePath))
        {
            _logger.LogInformation("对话配置文件不存在，创建默认配置: {Path}", _configFilePath);
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
                _logger.LogWarning("对话配置文件不存在: {Path}", path);
                return false;
            }

            var json = File.ReadAllText(path);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };

            var config = JsonSerializer.Deserialize<DigimonDialogueConfig>(json, options);
            if (config == null)
            {
                _logger.LogError("反序列化对话配置文件失败: {Path}", path);
                return false;
            }

            lock (_lock)
            {
                _config = config;
            }

            _logger.LogInformation("对话配置已加载: {Path}", path);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载对话配置文件失败: {Path}", path);
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

            _logger.LogInformation("对话配置已保存: {Path}", path);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存对话配置文件失败: {Path}", path);
            return false;
        }
    }

    public bool ReloadConfig()
    {
        _logger.LogInformation("重新加载对话配置...");

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

    private void CreateDefaultConfig()
    {
        _config = new DigimonDialogueConfig
        {
            Battle = new BattleDialogue
            {
                AttackPrompt = "你正在战斗！你对{Target}发起了攻击。描述你的攻击动作、使用的技能，以及战斗时的气势。",
                DefensePrompt = "你正在防御{Attacker}的攻击！描述你如何躲避或格挡，以及你的反应。",
                VictoryPrompt = "战斗胜利了！描述你获胜后的反应，庆祝胜利的方式。",
                DefeatPrompt = "战斗失利了...描述你失败后的反应，但不要气馁，表示会继续努力。",
                ToObjectPrompt = "你对{Target}发起了攻击！描述你的攻击动作，以及击中目标后的效果。"
            },
            CheckIn = new CheckInDialogue
            {
                GreetingTemplates = new List<string>
                {
                    "早上好！{UserName}！今天也来看我啦~",
                    "欢迎回来，{UserName}！我等你好久了呢！"
                },
                StreakEncouragement = new List<string>
                {
                    "连续{Streak}天啦！真厉害！",
                    "{Streak}天连续打卡！我们的羁绊越来越深了！"
                },
                ReceivedGiftResponse = new List<string>
                {
                    "哇！收到了{ItemName}！谢谢你！",
                    "{ItemName}！我最喜欢的！"
                }
            },
            Evolution = new EvolutionDialogue
            {
                EvolutionAnnouncement = "✨ **进化之光！**\n\n{Name}的身体开始发光...",
                RebirthAnnouncement = "🥚 **新的生命开始了！**\n\n{Name}化作光芒，然后...",
                PostEvolutionGreeting = "感觉充满了力量！谢谢{UserName}一直以来的陪伴！"
            },
            Idle = new IdleDialogue
            {
                IdlePrompts = new List<string>
                {
                    "（打哈欠）有点困了呢...",
                    "好无聊啊~{UserName}陪我玩嘛~"
                }
            },
            EmotionResponses = new EmotionResponses
            {
                HighCourage = "我感觉充满了勇气！什么困难都不怕！",
                HighFriendship = "{UserName}，能遇到你真是太好了！",
                HighLove = "{UserName}，我会一直陪着你的~",
                HighKnowledge = "我又学到了新知识！这个世界真奇妙！"
            }
        };
        SaveConfig(_configFilePath);
    }
}
