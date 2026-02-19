using System.Security.Cryptography;
using System.Text;

namespace DigimonBot.Messaging.Commands;

/// <summary>
/// 今日人品 (Jin Ri Ren Pin) 指令
/// 根据用户QQ号和日期计算今日运势
/// </summary>
public class JrrpCommand : ICommand
{
    public string Name => "jrrp";
    public string[] Aliases => new[] { "今日人品", "人品", "运势" };
    public string Description => "查看今日人品值 (0-100)";

    public Task<CommandResult> ExecuteAsync(CommandContext context)
    {
        // 获取今天的日期字符串 (格式: yyyyMMdd)
        var today = DateTime.Now.ToString("yyyyMMdd");
        
        // 拼接QQ号和日期
        var input = $"{context.UserId}:{today}";
        
        // 计算Hash并转换为0-100的数字
        var luckValue = CalculateLuckValue(input);
        
        // 获取评语
        var comment = GetComment(luckValue);
        
        // 构建回复消息
        var message = $"""
        🎲 **{context.UserName}** 的今日人品
        
        📅 日期：{DateTime.Now:yyyy年MM月dd日}
        🎰 人品值：{luckValue}/100
        
        💭 {comment}
        """;

        // 特殊值额外提示
        if (luckValue == 100)
        {
            message += "\n\n🌟 **今日欧皇！适合抽卡、买彩票、表白！**";
        }
        else if (luckValue == 0)
        {
            message += "\n\n💀 **建议今天宅在家里，不要出门...**";
        }
        else if (luckValue >= 90)
        {
            message += "\n\n✨ 运气不错，把握机会！";
        }
        else if (luckValue <= 10)
        {
            message += "\n\n⚠️ 小心行事，凡事三思...";
        }

        return Task.FromResult(new CommandResult
        {
            Success = true,
            Message = message
        });
    }

    /// <summary>
    /// 计算人品值 (0-100)
    /// </summary>
    private int CalculateLuckValue(string input)
    {
        // 使用MD5计算Hash
        using var md5 = MD5.Create();
        var hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
        
        // 将Hash转换为数字
        // 取前4个字节转换为整数
        var hashNumber = BitConverter.ToUInt32(hashBytes, 0);
        
        // 对101取模，得到0-100的数字
        var luckValue = (int)(hashNumber % 101);
        
        return luckValue;
    }

    /// <summary>
    /// 根据人品值获取评语
    /// </summary>
    private string GetComment(int luckValue)
    {
        return luckValue switch
        {
            100 => "⭐⭐⭐⭐⭐ 完美无瑕！天选之子！",
            >= 90 => "⭐⭐⭐⭐⭐ 鸿运当头！诸事顺遂！",
            >= 80 => "⭐⭐⭐⭐☆ 福星高照！好运连连！",
            >= 70 => "⭐⭐⭐⭐☆ 吉星拱照！心想事成！",
            >= 60 => "⭐⭐⭐☆☆ 顺风顺水！小有收获！",
            >= 50 => "⭐⭐⭐☆☆ 平平淡淡才是真~",
            >= 40 => "⭐⭐☆☆☆ 略有波折，保持乐观！",
            >= 30 => "⭐⭐☆☆☆ 小心谨慎，避免冲动！",
            >= 20 => "⭐☆☆☆☆ 时运不济，多喝热水...",
            >= 10 => "⭐☆☆☆☆ 霉运缠身，宅家保平安",
            > 0 => "💦 危！建议今天躺平...",
            0 => "💀 大凶！建议重新投胎（不是）",
            _ => "❓ 神秘力量干扰，无法预测"
        };
    }
}

/// <summary>
/// 简单的替代实现（如果不需要MD5）
/// </summary>
public class SimpleJrrpCommand : ICommand
{
    public string Name => "jrrp2";
    public string[] Aliases => new[] { "人品2" };
    public string Description => "查看今日人品值 (简单算法版)";

    public Task<CommandResult> ExecuteAsync(CommandContext context)
    {
        var today = DateTime.Now.ToString("yyyyMMdd");
        var luckValue = SimpleHash(context.UserId, today);
        
        var comment = luckValue switch
        {
            100 => "🌟 传说级欧皇！",
            >= 80 => "✨ 超级幸运！",
            >= 60 => "😊 运气不错~",
            >= 40 => "😐 平平淡淡",
            >= 20 => "😅 有点背啊",
            >= 1 => "😭 霉运附体",
            0 => "💀 建议重开",
            _ => "❓ 未知"
        };

        var message = $"""
        🎲 {context.UserName} 的今日人品
        
        人品值：{luckValue}/100
        {comment}
        """;

        return Task.FromResult(new CommandResult
        {
            Success = true,
            Message = message
        });
    }

    /// <summary>
    /// 简单Hash算法（不使用MD5）
    /// </summary>
    private int SimpleHash(string userId, string date)
    {
        var combined = userId + date;
        var hash = 0;
        
        // 简单的字符累加
        foreach (var c in combined)
        {
            hash = ((hash << 5) - hash) + c;
            hash = hash & 0x7FFFFFFF; // 确保正数
        }
        
        return hash % 101;
    }
}
