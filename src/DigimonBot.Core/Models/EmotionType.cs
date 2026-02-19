namespace DigimonBot.Core.Models;

/// <summary>
/// 四维情感属性 - 参考数码宝贝Next Order设计
/// </summary>
public enum EmotionType
{
    /// <summary>勇气 - 主动、挑战、保护</summary>
    Courage,
    /// <summary>友情 - 陪伴、合作、关心</summary>
    Friendship,
    /// <summary>爱心 - 温柔、治愈、体贴</summary>
    Love,
    /// <summary>知识 - 学习、探索、智慧</summary>
    Knowledge
}

/// <summary>
/// 情感属性集合
/// </summary>
public class EmotionValues
{
    public int Courage { get; set; }
    public int Friendship { get; set; }
    public int Love { get; set; }
    public int Knowledge { get; set; }

    public int GetValue(EmotionType type) => type switch
    {
        EmotionType.Courage => Courage,
        EmotionType.Friendship => Friendship,
        EmotionType.Love => Love,
        EmotionType.Knowledge => Knowledge,
        _ => 0
    };

    public void AddValue(EmotionType type, int value)
    {
        switch (type)
        {
            case EmotionType.Courage: Courage += value; break;
            case EmotionType.Friendship: Friendship += value; break;
            case EmotionType.Love: Love += value; break;
            case EmotionType.Knowledge: Knowledge += value; break;
        }
    }

    /// <summary>
    /// 计算与目标的匹配度（用于进化判定）
    /// </summary>
    public double CalculateMatchScore(EmotionValues requirements)
    {
        var types = new[] { EmotionType.Courage, EmotionType.Friendship, EmotionType.Love, EmotionType.Knowledge };
        double totalScore = 0;
        int requirementCount = 0;

        foreach (var type in types)
        {
            var required = requirements.GetValue(type);
            if (required > 0)
            {
                var current = GetValue(type);
                // 如果满足要求则得1分，否则按比例得分
                totalScore += Math.Min((double)current / required, 1.0);
                requirementCount++;
            }
        }

        return requirementCount > 0 ? totalScore / requirementCount : 0;
    }

    /// <summary>
    /// 是否满足所有要求
    /// </summary>
    public bool MeetsRequirements(EmotionValues requirements)
    {
        return Courage >= requirements.Courage
            && Friendship >= requirements.Friendship
            && Love >= requirements.Love
            && Knowledge >= requirements.Knowledge;
    }

    /// <summary>
    /// 计算复杂度（用于多选一时的优先级判定）
    /// </summary>
    public int CalculateComplexity()
    {
        var values = new[] { Courage, Friendship, Love, Knowledge };
        // 非零属性数量 × 10 + 总数值
        var nonZeroCount = values.Count(v => v > 0);
        var totalValue = values.Sum();
        return nonZeroCount * 10 + totalValue;
    }

    public EmotionValues Clone()
    {
        return new EmotionValues
        {
            Courage = Courage,
            Friendship = Friendship,
            Love = Love,
            Knowledge = Knowledge
        };
    }
}
