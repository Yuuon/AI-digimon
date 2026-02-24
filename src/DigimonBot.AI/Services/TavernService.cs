using DigimonBot.Core.Models;
using DigimonBot.Core.Models.Tavern;
using DigimonBot.Core.Services;
using Microsoft.Extensions.Logging;

namespace DigimonBot.AI.Services;

/// <summary>
/// 酒馆服务实现
/// </summary>
public class TavernService : ITavernService
{
    private readonly ITavernCharacterParser _characterParser;
    private readonly IAIClient _aiClient;
    private readonly ILogger<TavernService> _logger;
    private readonly ITavernConfigService _configService;
    private bool _isEnabled;
    private TavernCharacter? _currentCharacter;
    private readonly List<ChatMessage> _conversationHistory = new();

    // 从配置读取最大历史长度
    private int MaxHistoryLength => _configService.Config.Generation.MaxHistoryLength;

    public TavernService(
        ITavernCharacterParser characterParser,
        IAIClient aiClient,
        ILogger<TavernService> logger,
        ITavernConfigService configService)
    {
        _characterParser = characterParser;
        _aiClient = aiClient;
        _logger = logger;
        _configService = configService;
        
        // 从配置读取角色目录
        CharacterDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, 
            _configService.Config.CharacterDirectory);
        
        // 确保角色目录存在
        if (!Directory.Exists(CharacterDirectory))
        {
            Directory.CreateDirectory(CharacterDirectory);
        }
    }

    public bool IsEnabled => _isEnabled;
    public TavernCharacter? CurrentCharacter => _currentCharacter;
    public string CharacterDirectory { get; }

    public void Enable()
    {
        _isEnabled = true;
        _logger.LogInformation("酒馆模式已开启");
    }

    public void Disable()
    {
        _isEnabled = false;
        _conversationHistory.Clear();
        _logger.LogInformation("酒馆模式已关闭");
    }

    public bool Toggle()
    {
        if (_isEnabled)
        {
            Disable();
        }
        else
        {
            Enable();
        }
        return _isEnabled;
    }

    public async Task<bool> LoadCharacterAsync(string characterName)
    {
        // 尝试查找文件（支持模糊匹配）
        var files = _characterParser.GetCharacterFiles(CharacterDirectory);
        var matchedFile = files.FirstOrDefault(f => 
        {
            var name = Path.GetFileNameWithoutExtension(f);
            return name.Equals(characterName, StringComparison.OrdinalIgnoreCase) ||
                   name.Contains(characterName, StringComparison.OrdinalIgnoreCase);
        });

        if (matchedFile == null)
        {
            _logger.LogWarning("未找到角色: {CharacterName}", characterName);
            return false;
        }

        var character = await _characterParser.ParseAsync(matchedFile);
        if (character == null)
        {
            _logger.LogWarning("解析角色失败: {FilePath}", matchedFile);
            return false;
        }

        _currentCharacter = character;
        _conversationHistory.Clear();
        
        // 添加开场白到历史
        if (!string.IsNullOrEmpty(character.FirstMessage))
        {
            _conversationHistory.Add(new ChatMessage
            {
                Timestamp = DateTime.Now,
                IsFromUser = false,
                Content = character.FirstMessage
            });
        }
        
        _logger.LogInformation("角色加载成功: {CharacterName}", character.Name);
        return true;
    }

    public void UnloadCharacter()
    {
        _currentCharacter = null;
        _conversationHistory.Clear();
        _logger.LogInformation("角色已卸载");
    }

    public async Task<IEnumerable<CharacterInfo>> GetAvailableCharactersAsync()
    {
        var files = _characterParser.GetCharacterFiles(CharacterDirectory);
        var characters = new List<CharacterInfo>();

        foreach (var file in files)
        {
            try
            {
                var character = await _characterParser.ParseAsync(file);
                if (character != null)
                {
                    characters.Add(new CharacterInfo
                    {
                        FileName = Path.GetFileNameWithoutExtension(file),
                        Name = character.Name,
                        FilePath = file,
                        Format = Path.GetExtension(file).TrimStart('.').ToUpper(),
                        Tags = character.Tags
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "解析角色文件失败: {File}", file);
            }
        }

        return characters;
    }

    // 同步版本（用于兼容）
    public IEnumerable<CharacterInfo> GetAvailableCharacters()
    {
        // 使用 Task.Run 避免在同步上下文中阻塞
        return Task.Run(() => GetAvailableCharactersAsync()).GetAwaiter().GetResult();
    }

    public async Task<string> GenerateResponseAsync(string userMessage, string userName)
    {
        if (_currentCharacter == null)
        {
            return "[酒馆模式] 没有加载角色，请先使用 /loadchar 加载角色。";
        }

        if (!_isEnabled)
        {
            return "[酒馆模式] 酒馆模式未开启，请先使用 /酒馆 on 开启。";
        }

        try
        {
            // 构建系统提示词
            var systemPrompt = BuildSystemPrompt(userName);
            
            // 构建当前对话上下文
            var messages = new List<ChatMessage>();
            
            // 添加历史对话（限制长度）
            var recentHistory = _conversationHistory.TakeLast(MaxHistoryLength).ToList();
            messages.AddRange(recentHistory);
            
            // 添加用户消息
            messages.Add(new ChatMessage
            {
                Timestamp = DateTime.Now,
                IsFromUser = true,
                Content = userMessage
            });

            // 调用 AI
            var response = await _aiClient.ChatAsync(messages, systemPrompt);
            
            // 记录到历史
            _conversationHistory.Add(new ChatMessage
            {
                Timestamp = DateTime.Now,
                IsFromUser = true,
                Content = userMessage
            });
            _conversationHistory.Add(new ChatMessage
            {
                Timestamp = DateTime.Now,
                IsFromUser = false,
                Content = response.Content
            });
            
            // 限制历史长度
            if (_conversationHistory.Count > MaxHistoryLength * 2)
            {
                _conversationHistory.RemoveRange(0, _conversationHistory.Count - MaxHistoryLength * 2);
            }

            return response.Content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成酒馆回复失败");
            return $"[酒馆模式] 生成回复时出错: {ex.Message}";
        }
    }

    public async Task<string> GenerateSummaryResponseAsync(string summary, string keywords)
    {
        if (_currentCharacter == null || !_isEnabled)
        {
            return "";
        }

        try
        {
            // 从配置读取场景模板
            var scenarioTemplate = _configService.Config.AutoSpeak.ScenarioTemplate;
            var prompt = scenarioTemplate
                .Replace("{Summary}", summary)
                .Replace("{Keywords}", keywords);

            var systemPrompt = BuildSystemPrompt("群友");
            
            var messages = new List<ChatMessage>
            {
                new()
                {
                    Timestamp = DateTime.Now,
                    IsFromUser = true,
                    Content = prompt
                }
            };

            var response = await _aiClient.ChatAsync(messages, systemPrompt);
            return response.Content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成群聊总结回复失败");
            return "";
        }
    }

    public bool HasCharacterLoaded()
    {
        return _currentCharacter != null;
    }

    /// <summary>
    /// 构建系统提示词
    /// </summary>
    private string BuildSystemPrompt(string userName)
    {
        if (_currentCharacter == null)
        {
            return "";
        }

        var sb = new System.Text.StringBuilder();
        
        sb.AppendLine($"你是{_currentCharacter.Name}。");
        sb.AppendLine($"对方的名字是{userName}。");
        
        if (!string.IsNullOrEmpty(_currentCharacter.Description))
        {
            sb.AppendLine($"背景设定：{_currentCharacter.Description}");
        }
        
        if (!string.IsNullOrEmpty(_currentCharacter.Personality))
        {
            sb.AppendLine($"性格特点：{_currentCharacter.Personality}");
        }
        
        if (!string.IsNullOrEmpty(_currentCharacter.Scenario))
        {
            sb.AppendLine($"当前场景：{_currentCharacter.Scenario}");
        }
        
        if (!string.IsNullOrEmpty(_currentCharacter.MessageExample))
        {
            sb.AppendLine($"对话示例：{_currentCharacter.MessageExample}");
        }
        
        sb.AppendLine($"请记住你的人设，以{_currentCharacter.Name}的身份回应。");
        sb.AppendLine("保持角色性格一致性，回应要自然、有角色特色。");
        
        return sb.ToString();
    }
}
