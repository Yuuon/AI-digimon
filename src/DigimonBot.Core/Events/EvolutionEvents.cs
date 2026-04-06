namespace DigimonBot.Core.Events;

/// <summary>
/// 进化事件参数
/// </summary>
public class EvolutionEventArgs : EventArgs
{
    public string UserId { get; set; } = "";
    public string OldDigimonId { get; set; } = "";
    public string OldDigimonName { get; set; } = "";
    public string NewDigimonId { get; set; } = "";
    public string NewDigimonName { get; set; } = "";
    public bool IsRebirth { get; set; }
    public string EvolutionDescription { get; set; } = "";
}

/// <summary>
/// 情感变化事件参数
/// </summary>
public class EmotionChangedEventArgs : EventArgs
{
    public string UserId { get; set; } = "";
    public string DigimonId { get; set; } = "";
    public Dictionary<Models.EmotionType, int> Changes { get; set; } = new();
    public string Reason { get; set; } = "";
}

/// <summary>
/// 进化接近事件参数（用于提前通知用户）
/// </summary>
public class EvolutionApproachingEventArgs : EventArgs
{
    public string UserId { get; set; } = "";
    public string CurrentDigimonId { get; set; } = "";
    public int CurrentTokens { get; set; }
    public int RequiredTokens { get; set; }
    public double ProgressPercentage => RequiredTokens > 0 
        ? Math.Min((double)CurrentTokens / RequiredTokens * 100, 100) 
        : 0;
}

/// <summary>
/// 酒馆自主发言事件参数
/// </summary>
public class TavernAutoSpeakEventArgs : EventArgs
{
    public long GroupId { get; set; }
    public string Message { get; set; } = "";
}

/// <summary>
/// 进化选项信息
/// </summary>
public class EvolutionOptionInfo
{
    public string TargetId { get; set; } = "";
    public string TargetName { get; set; } = "";
    public string Description { get; set; } = "";
    public int RequiredTokens { get; set; }
    public double MatchScore { get; set; }
}

/// <summary>
/// 进化就绪事件参数（多个进化选项可用时）
/// </summary>
public class EvolutionReadyEventArgs : EventArgs
{
    public string UserId { get; set; } = "";
    public long GroupId { get; set; }
    public string CurrentDigimonId { get; set; } = "";
    public string CurrentDigimonName { get; set; } = "";
    public List<EvolutionOptionInfo> AvailableEvolutions { get; set; } = new();
}

/// <summary>
/// 事件发布器接口
/// </summary>
public interface IEventPublisher
{
    event EventHandler<EvolutionEventArgs>? OnEvolved;
    event EventHandler<EmotionChangedEventArgs>? OnEmotionChanged;
    event EventHandler<EvolutionApproachingEventArgs>? OnEvolutionApproaching;
    event EventHandler<TavernAutoSpeakEventArgs>? OnTavernAutoSpeak;
    event EventHandler<EvolutionReadyEventArgs>? OnEvolutionReady;
    
    void PublishEvolution(EvolutionEventArgs args);
    void PublishEmotionChanged(EmotionChangedEventArgs args);
    void PublishEvolutionApproaching(EvolutionApproachingEventArgs args);
    void PublishTavernAutoSpeak(TavernAutoSpeakEventArgs args);
    void PublishEvolutionReady(EvolutionReadyEventArgs args);
}
