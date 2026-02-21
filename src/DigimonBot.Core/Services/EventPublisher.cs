using DigimonBot.Core.Events;

namespace DigimonBot.Core.Services;

/// <summary>
/// 事件发布器实现
/// </summary>
public class EventPublisher : IEventPublisher
{
    public event EventHandler<EvolutionEventArgs>? OnEvolved;
    public event EventHandler<EmotionChangedEventArgs>? OnEmotionChanged;
    public event EventHandler<EvolutionApproachingEventArgs>? OnEvolutionApproaching;
    public event EventHandler<TavernAutoSpeakEventArgs>? OnTavernAutoSpeak;

    public void PublishEvolution(EvolutionEventArgs args)
    {
        OnEvolved?.Invoke(this, args);
    }

    public void PublishEmotionChanged(EmotionChangedEventArgs args)
    {
        OnEmotionChanged?.Invoke(this, args);
    }

    public void PublishEvolutionApproaching(EvolutionApproachingEventArgs args)
    {
        OnEvolutionApproaching?.Invoke(this, args);
    }

    public void PublishTavernAutoSpeak(TavernAutoSpeakEventArgs args)
    {
        OnTavernAutoSpeak?.Invoke(this, args);
    }
}
