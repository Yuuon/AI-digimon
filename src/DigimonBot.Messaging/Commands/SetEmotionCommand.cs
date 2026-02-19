using DigimonBot.Core.Models;
using DigimonBot.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DigimonBot.Messaging.Commands;

/// <summary>
/// 设置/修改情感值指令（仅白名单用户可用）
/// </summary>
public class SetEmotionCommand : ICommand
{
    private readonly IDigimonManager _digimonManager;
    private readonly IEmotionTracker _emotionTracker;
    private readonly List<string> _whitelist;
    private readonly ILogger<SetEmotionCommand> _logger;

    public SetEmotionCommand(
        IDigimonManager digimonManager,
        IEmotionTracker emotionTracker,
        AdminConfig adminConfig,
        ILogger<SetEmotionCommand> logger)
    {
        _digimonManager = digimonManager;
        _emotionTracker = emotionTracker;
        _whitelist = adminConfig.Whitelist ?? new List<string>();
        _logger = logger;
    }

    public string Name => "setemotion";
    public string[] Aliases => new[] { "设置情感", "emotion" };
    public string Description => "【管理员】设置或修改情感值（白名单限定）";

    public async Task<CommandResult> ExecuteAsync(CommandContext context)
    {
        // 白名单检查
        if (!IsWhitelisted(context.UserId))
        {
            _logger.LogWarning("用户 {UserId} 尝试使用管理指令 {Command}，但不在白名单中", context.UserId, Name);
            return new CommandResult 
            { 
                Success = false, 
                Message = "❌ 你没有权限使用此指令。"
            };
        }

        // 检查参数
        if (context.Args.Length == 0)
        {
            return new CommandResult 
            { 
                Success = false, 
                Message = GetHelpMessage()
            };
        }

        // 获取用户数码宝贝
        var digimon = await _digimonManager.GetOrCreateAsync(context.UserId);
        if (digimon == null)
        {
            return new CommandResult 
            { 
                Success = false, 
                Message = "❌ 获取数码宝贝信息失败。"
            };
        }

        var firstArg = context.Args[0].ToLower();

        // 处理 reset 子命令
        if (firstArg == "reset" || firstArg == "重置")
        {
            return await ResetEmotionsAsync(digimon);
        }

        // 处理查看当前情感值
        if (firstArg == "show" || firstArg == "查看")
        {
            return ShowCurrentEmotions(digimon);
        }

        // 解析参数：支持两种格式
        // 1. /setemotion courage 10    (增加)
        // 2. /setemotion courage=50    (设置)
        if (context.Args.Length >= 2)
        {
            // 格式1: emotionType value (增加)
            var emotionType = ParseEmotionType(context.Args[0]);
            if (!emotionType.HasValue)
            {
                return new CommandResult 
                { 
                    Success = false, 
                    Message = $"❌ 未知的情感类型: {context.Args[0]}\n{GetHelpMessage()}"
                };
            }

            if (!int.TryParse(context.Args[1], out var delta))
            {
                return new CommandResult 
                { 
                    Success = false, 
                    Message = $"❌ 无效的数量: {context.Args[1]}"
                };
            }

            return await AddEmotionAsync(digimon, emotionType.Value, delta);
        }
        else if (firstArg.Contains('='))
        {
            // 格式2: emotionType=value (设置)
            var parts = firstArg.Split('=');
            if (parts.Length != 2)
            {
                return new CommandResult 
                { 
                    Success = false, 
                    Message = GetHelpMessage()
                };
            }

            var emotionType = ParseEmotionType(parts[0]);
            if (!emotionType.HasValue)
            {
                return new CommandResult 
                { 
                    Success = false, 
                    Message = $"❌ 未知的情感类型: {parts[0]}\n{GetHelpMessage()}"
                };
            }

            if (!int.TryParse(parts[1], out var value))
            {
                return new CommandResult 
                { 
                    Success = false, 
                    Message = $"❌ 无效的数值: {parts[1]}"
                };
            }

            return await SetEmotionAsync(digimon, emotionType.Value, value);
        }
        else
        {
            // 尝试解析为 all=value 或 其他简写
            var emotionType = ParseEmotionType(firstArg);
            if (emotionType.HasValue)
            {
                return new CommandResult 
                { 
                    Success = false, 
                    Message = $"❌ 请指定数值\n{GetHelpMessage()}"
                };
            }

            return new CommandResult 
            { 
                Success = false, 
                Message = GetHelpMessage()
            };
        }
    }

    /// <summary>
    /// 检查用户是否在白名单中
    /// </summary>
    private bool IsWhitelisted(string userId)
    {
        if (_whitelist == null || _whitelist.Count == 0)
        {
            _logger.LogWarning("白名单为空，拒绝所有管理指令请求");
            return false;
        }
        return _whitelist.Contains(userId);
    }

    /// <summary>
    /// 增加情感值
    /// </summary>
    private async Task<CommandResult> AddEmotionAsync(UserDigimon digimon, EmotionType emotionType, int delta)
    {
        var oldValue = digimon.Emotions.GetValue(emotionType);
        digimon.Emotions.AddValue(emotionType, delta);
        var newValue = digimon.Emotions.GetValue(emotionType);

        await _digimonManager.SaveAsync(digimon);

        var emotionName = GetEmotionDisplayName(emotionType);
        var operation = delta >= 0 ? "增加" : "减少";
        var absDelta = Math.Abs(delta);

        _logger.LogInformation("用户 {UserId} 修改情感值: {Emotion} {Operation} {Delta} ({Old} -> {New})",
            digimon.UserId, emotionName, operation, absDelta, oldValue, newValue);

        return new CommandResult
        {
            Success = true,
            Message = $"✅ {emotionName} {operation}了 {absDelta} 点\n" +
                     $"📊 {oldValue} → {newValue}"
        };
    }

    /// <summary>
    /// 设置情感值
    /// </summary>
    private async Task<CommandResult> SetEmotionAsync(UserDigimon digimon, EmotionType emotionType, int value)
    {
        var oldValue = digimon.Emotions.GetValue(emotionType);
        
        switch (emotionType)
        {
            case EmotionType.Courage:
                digimon.Emotions.Courage = Math.Max(0, value);
                break;
            case EmotionType.Friendship:
                digimon.Emotions.Friendship = Math.Max(0, value);
                break;
            case EmotionType.Love:
                digimon.Emotions.Love = Math.Max(0, value);
                break;
            case EmotionType.Knowledge:
                digimon.Emotions.Knowledge = Math.Max(0, value);
                break;
        }

        var newValue = digimon.Emotions.GetValue(emotionType);
        await _digimonManager.SaveAsync(digimon);

        var emotionName = GetEmotionDisplayName(emotionType);

        _logger.LogInformation("用户 {UserId} 设置情感值: {Emotion} = {NewValue} (原值: {OldValue})",
            digimon.UserId, emotionName, newValue, oldValue);

        return new CommandResult
        {
            Success = true,
            Message = $"✅ {emotionName} 设置为 {newValue}\n" +
                     $"📊 {oldValue} → {newValue}"
        };
    }

    /// <summary>
    /// 重置所有情感值
    /// </summary>
    private async Task<CommandResult> ResetEmotionsAsync(UserDigimon digimon)
    {
        var oldEmotions = new EmotionValues
        {
            Courage = digimon.Emotions.Courage,
            Friendship = digimon.Emotions.Friendship,
            Love = digimon.Emotions.Love,
            Knowledge = digimon.Emotions.Knowledge
        };

        digimon.Emotions.Courage = 0;
        digimon.Emotions.Friendship = 0;
        digimon.Emotions.Love = 0;
        digimon.Emotions.Knowledge = 0;

        await _digimonManager.SaveAsync(digimon);

        _logger.LogInformation("用户 {UserId} 重置了所有情感值", digimon.UserId);

        return new CommandResult
        {
            Success = true,
            Message = $"✅ 所有情感值已重置\n" +
                     $"📊 勇气: {oldEmotions.Courage} → 0\n" +
                     $"📊 友情: {oldEmotions.Friendship} → 0\n" +
                     $"📊 爱心: {oldEmotions.Love} → 0\n" +
                     $"📊 知识: {oldEmotions.Knowledge} → 0"
        };
    }

    /// <summary>
    /// 显示当前情感值
    /// </summary>
    private CommandResult ShowCurrentEmotions(UserDigimon digimon)
    {
        var description = _emotionTracker.GetEmotionDescription(digimon.Emotions);
        var dominant = _emotionTracker.GetDominantEmotion(digimon.Emotions);

        return new CommandResult
        {
            Success = true,
            Message = $"📊 当前情感值\n\n" +
                     $"❤️ 勇气: {digimon.Emotions.Courage}\n" +
                     $"💛 友情: {digimon.Emotions.Friendship}\n" +
                     $"💗 爱心: {digimon.Emotions.Love}\n" +
                     $"💙 知识: {digimon.Emotions.Knowledge}\n\n" +
                     $"💭 主导情感: {GetEmotionDisplayName(dominant.Type)} ({dominant.Value})\n" +
                     $"📝 状态: {description}"
        };
    }

    /// <summary>
    /// 解析情感类型
    /// </summary>
    private EmotionType? ParseEmotionType(string input)
    {
        return input.ToLower() switch
        {
            "courage" or "勇气" or "勇" or "c" => EmotionType.Courage,
            "friendship" or "友情" or "友" or "f" => EmotionType.Friendship,
            "love" or "爱心" or "爱" or "l" => EmotionType.Love,
            "knowledge" or "知识" or "知" or "k" => EmotionType.Knowledge,
            _ => null
        };
    }

    /// <summary>
    /// 获取情感显示名称
    /// </summary>
    private string GetEmotionDisplayName(EmotionType type) => type switch
    {
        EmotionType.Courage => "❤️ 勇气",
        EmotionType.Friendship => "💛 友情",
        EmotionType.Love => "💗 爱心",
        EmotionType.Knowledge => "💙 知识",
        _ => "未知"
    };

    /// <summary>
    /// 获取帮助信息
    /// </summary>
    private string GetHelpMessage()
    {
        return """
        🛠️ 情感值管理指令

        使用方式：
        • /setemotion <情感类型> <数值>  - 增加/减少情感值
        • /setemotion <情感类型>=<数值>  - 设置情感值为指定值
        • /setemotion show               - 查看当前情感值
        • /setemotion reset              - 重置所有情感值

        情感类型：
        • courage / 勇气 / c    - 勇气
        • friendship / 友情 / f - 友情
        • love / 爱心 / l       - 爱心
        • knowledge / 知识 / k  - 知识

        示例：
        • /setemotion courage 10      (勇气+10)
        • /setemotion love=-5         (爱心-5)
        • /setemotion courage=50      (设置勇气为50)
        • /setemotion show            (查看当前值)
        • /setemotion reset           (重置所有值)
        """;
    }
}
