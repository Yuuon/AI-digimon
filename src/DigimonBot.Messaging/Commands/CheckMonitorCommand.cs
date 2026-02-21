using DigimonBot.Core.Services;
using Microsoft.Extensions.Logging;

namespace DigimonBot.Messaging.Commands;

/// <summary>
/// 检查群聊监测状态（调试指令）
/// </summary>
public class CheckMonitorCommand : ICommand
{
    private readonly IGroupChatMonitorService _monitorService;
    private readonly ITavernService _tavernService;
    private readonly ILogger<CheckMonitorCommand> _logger;

    public CheckMonitorCommand(
        IGroupChatMonitorService monitorService,
        ITavernService tavernService,
        ILogger<CheckMonitorCommand> logger)
    {
        _monitorService = monitorService;
        _tavernService = tavernService;
        _logger = logger;
    }

    public string Name => "checkmonitor";
    public string[] Aliases => new[] { "监测状态", "debugmonitor" };
    public string Description => "【调试】检查群聊监测状态（酒馆自主发言触发条件）";

    public Task<CommandResult> ExecuteAsync(CommandContext context)
    {
        if (!context.IsGroupMessage)
        {
            return Task.FromResult(new CommandResult
            {
                Success = false,
                Message = "❌ 此指令只能在群聊中使用。"
            });
        }

        var groupId = context.GroupId;
        
        _logger.LogInformation("用户 {UserId} 请求检查群 {GroupId} 的监测状态", context.OriginalUserId, groupId);

        // 获取监测状态
        var status = _monitorService.GetGroupStatus(groupId);
        
        // 检查酒馆模式
        var tavernEnabled = _tavernService.IsEnabled;
        var hasCharacter = _tavernService.HasCharacterLoaded();
        var characterName = _tavernService.CurrentCharacter?.Name ?? "未加载";

        var message = $"""
            📊 **群聊监测状态检查**
            
            **酒馆模式**: {(tavernEnabled ? "✅ 开启" : "❌ 关闭")}
            **角色状态**: {(hasCharacter ? $"✅ 已加载 ({characterName})" : "❌ 未加载")}
            
            **消息记录**: {status.MessageCount} 条 (需要 ≥3 条)
            {(status.MessageCount < 3 ? "⚠️ 消息数量不足" : "✅ 消息数量足够")}
            
            **关键词统计** (Top 5):
            {FormatKeywords(status.TopKeywords)}
            
            **触发条件检查**:
            • 酒馆开启: {(tavernEnabled ? "✅" : "❌")}
            • 角色加载: {(hasCharacter ? "✅" : "❌")}
            • 消息数量: {(status.MessageCount >= 3 ? "✅" : "❌")}
            • 高频关键词: {(status.HasHighFreqKeyword ? $"✅ ({status.TopKeywords.FirstOrDefault().Key})" : "❌")}
            • 冷却时间: {(status.IsInCooldown ? $"⏳ 还剩 {status.CooldownSeconds}秒" : "✅ 已就绪")}
            
            **结论**: {(status.CanTrigger ? "🎉 满足触发条件，下条消息可能触发自主发言！" : "⏳ 暂不满足触发条件")}
            """;

        return Task.FromResult(new CommandResult
        {
            Success = true,
            Message = message
        });
    }

    private static string FormatKeywords(Dictionary<string, int> keywords)
    {
        if (keywords.Count == 0)
            return "  (暂无关键词)";

        var lines = keywords.Take(5).Select(kvp =>
        {
            var indicator = kvp.Value >= 2 ? "🎯" : "  ";
            return $"  {indicator} `{kvp.Key}`: {kvp.Value}次";
        });

        return string.Join("\n", lines);
    }
}
