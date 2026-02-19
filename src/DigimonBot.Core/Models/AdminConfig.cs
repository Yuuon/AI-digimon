namespace DigimonBot.Core.Models;

/// <summary>
/// 管理员配置
/// </summary>
public class AdminConfig
{
    /// <summary>
    /// 允许使用管理指令的用户QQ号列表
    /// </summary>
    public List<string> Whitelist { get; set; } = new();
}
