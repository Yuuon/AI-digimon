namespace DigimonBot.Core.Models;

/// <summary>
/// 物品定义（从配置加载）
/// </summary>
public class ItemDefinition
{
    /// <summary>唯一标识</summary>
    public string Id { get; set; } = "";
    
    /// <summary>显示名称</summary>
    public string Name { get; set; } = "";
    
    /// <summary>描述</summary>
    public string Description { get; set; } = "";
    
    /// <summary>商店售价（0表示不可购买）</summary>
    public int Price { get; set; }
    
    /// <summary>物品类型：food, evolution, special 等</summary>
    public string Type { get; set; } = "food";
    
    /// <summary>使用效果：键为效果类型（如 courage, friendship），值为数值</summary>
    public Dictionary<string, int> Effects { get; set; } = new();
    
    /// <summary>是否可堆叠</summary>
    public bool Stackable { get; set; } = true;
    
    /// <summary>堆叠上限</summary>
    public int MaxStack { get; set; } = 99;
}

/// <summary>
/// 物品使用效果
/// </summary>
public class ItemEffect
{
    /// <summary>效果类型</summary>
    public ItemEffectType Type { get; set; }
    
    /// <summary>效果数值</summary>
    public int Value { get; set; }
}

/// <summary>
/// 物品效果类型
/// </summary>
public enum ItemEffectType
{
    /// <summary>增加勇气</summary>
    AddCourage,
    /// <summary>增加友情</summary>
    AddFriendship,
    /// <summary>增加爱心</summary>
    AddLove,
    /// <summary>增加知识</summary>
    AddKnowledge,
    /// <summary>增加金币（特殊物品）</summary>
    AddGold,
    /// <summary>重置情感值</summary>
    ResetEmotions,
    /// <summary>立即进化（特殊道具）</summary>
    ForceEvolution,
    /// <summary>其他效果</summary>
    Custom
}
