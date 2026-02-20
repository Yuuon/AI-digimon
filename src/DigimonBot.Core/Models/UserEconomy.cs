namespace DigimonBot.Core.Models;

/// <summary>
/// 用户经济数据
/// </summary>
public class UserEconomy
{
    /// <summary>用户ID（QQ号）</summary>
    public string UserId { get; set; } = "";
    
    /// <summary>金币余额</summary>
    public long Gold { get; set; }
    
    /// <summary>上次领取每日奖励时间</summary>
    public DateTime? LastDailyReward { get; set; }
}

/// <summary>
/// 用户背包中的物品
/// </summary>
public class UserItem
{
    /// <summary>记录ID</summary>
    public int Id { get; set; }
    
    /// <summary>用户ID</summary>
    public string UserId { get; set; } = "";
    
    /// <summary>物品ID（对应 ItemDefinition）</summary>
    public string ItemId { get; set; } = "";
    
    /// <summary>数量</summary>
    public int Quantity { get; set; }
    
    /// <summary>获得时间</summary>
    public DateTime AcquiredAt { get; set; }
}

/// <summary>
/// 用户数码宝贝状态（持久化版本）
/// </summary>
public class UserDigimonState
{
    /// <summary>用户ID（QQ号）</summary>
    public string UserId { get; set; } = "";
    
    /// <summary>群ID（为空表示私聊）</summary>
    public string? GroupId { get; set; }
    
    /// <summary>当前数码宝贝定义ID</summary>
    public string CurrentDigimonId { get; set; } = "";
    
    /// <summary>勇气值</summary>
    public int Courage { get; set; }
    
    /// <summary>友情值</summary>
    public int Friendship { get; set; }
    
    /// <summary>爱心值</summary>
    public int Love { get; set; }
    
    /// <summary>知识值</summary>
    public int Knowledge { get; set; }
    
    /// <summary>累计消耗的Token数</summary>
    public int TotalTokensConsumed { get; set; }
    
    /// <summary>孵化时间</summary>
    public DateTime HatchTime { get; set; }
    
    /// <summary>最后互动时间</summary>
    public DateTime LastInteractionTime { get; set; }
    
    /// <summary>
    /// 获取情感值对象
    /// </summary>
    public EmotionValues GetEmotions() => new()
    {
        Courage = Courage,
        Friendship = Friendship,
        Love = Love,
        Knowledge = Knowledge
    };
    
    /// <summary>
    /// 从情感值对象设置
    /// </summary>
    public void SetEmotions(EmotionValues emotions)
    {
        Courage = emotions.Courage;
        Friendship = emotions.Friendship;
        Love = emotions.Love;
        Knowledge = emotions.Knowledge;
    }
    
    /// <summary>
    /// 创建新的数码宝贝状态
    /// </summary>
    public static UserDigimonState CreateNew(string userId, string digimonId, string? groupId = null)
    {
        var now = DateTime.Now;
        return new UserDigimonState
        {
            UserId = userId,
            GroupId = groupId ?? "",  // 使用空字符串代替 null
            CurrentDigimonId = digimonId,
            Courage = 0,
            Friendship = 0,
            Love = 0,
            Knowledge = 0,
            TotalTokensConsumed = 0,
            HatchTime = now,
            LastInteractionTime = now
        };
    }
}
