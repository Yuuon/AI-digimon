namespace DigimonBot.Core.Models;

/// <summary>
/// 用户签到记录
/// </summary>
public class CheckInRecord
{
    /// <summary>用户ID</summary>
    public string UserId { get; set; } = "";
    
    /// <summary>总签到天数</summary>
    public int TotalCheckIns { get; set; }
    
    /// <summary>连续签到天数</summary>
    public int ConsecutiveCheckIns { get; set; }
    
    /// <summary>上次签到日期（yyyy-MM-dd）</summary>
    public string LastCheckInDate { get; set; } = "";
    
    /// <summary>累计签到奖励（获得的最高品级食物数量）</summary>
    public int HighTierRewards { get; set; }
}
