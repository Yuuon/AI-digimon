using DigimonBot.Core.Models;
using DigimonBot.Core.Services;
using Xunit;

namespace DigimonBot.Core.Tests.Services;

public class EmotionTrackerTests
{
    private readonly EmotionTracker _tracker;

    public EmotionTrackerTests()
    {
        _tracker = new EmotionTracker();
    }

    [Fact]
    public async Task ApplyEmotionAnalysisAsync_AppliesDelta()
    {
        var userDigimon = new UserDigimon
        {
            Emotions = new EmotionValues { Courage = 10, Friendship = 10 }
        };

        var analysis = new EmotionAnalysis
        {
            CourageDelta = 5,
            FriendshipDelta = 3,
            LoveDelta = 2,
            KnowledgeDelta = 1
        };

        await _tracker.ApplyEmotionAnalysisAsync(userDigimon, analysis, "测试");

        Assert.Equal(15, userDigimon.Emotions.Courage);
        Assert.Equal(13, userDigimon.Emotions.Friendship);
        Assert.Equal(2, userDigimon.Emotions.Love);
        Assert.Equal(1, userDigimon.Emotions.Knowledge);
    }

    [Fact]
    public async Task ApplyEmotionAnalysisAsync_DeltaExceedsMax_IsClamped()
    {
        var userDigimon = new UserDigimon
        {
            Emotions = new EmotionValues { Courage = 10 }
        };

        var analysis = new EmotionAnalysis
        {
            CourageDelta = 50 // 超过单次最大限制(10)
        };

        await _tracker.ApplyEmotionAnalysisAsync(userDigimon, analysis, "测试");

        Assert.Equal(20, userDigimon.Emotions.Courage); // 10 + 10(max)
    }

    [Fact]
    public async Task ApplyEmotionAnalysisAsync_NegativeDelta_NotBelowZero()
    {
        var userDigimon = new UserDigimon
        {
            Emotions = new EmotionValues { Courage = 5 }
        };

        var analysis = new EmotionAnalysis
        {
            CourageDelta = -10
        };

        await _tracker.ApplyEmotionAnalysisAsync(userDigimon, analysis, "测试");

        Assert.Equal(0, userDigimon.Emotions.Courage); // 不会低于0
    }

    [Fact]
    public void GetEmotionDescription_AllZero_ReturnsDefault()
    {
        var emotions = new EmotionValues();

        var desc = _tracker.GetEmotionDescription(emotions);

        Assert.Contains("天真", desc);
    }

    [Fact]
    public void GetEmotionDescription_WithValues_ReturnsDescription()
    {
        var emotions = new EmotionValues
        {
            Courage = 20,
            Friendship = 35
        };

        var desc = _tracker.GetEmotionDescription(emotions);

        Assert.Contains("勇敢", desc);
        Assert.Contains("重视友情", desc);
    }

    [Fact]
    public void GetDominantEmotion_ReturnsHighest()
    {
        var emotions = new EmotionValues
        {
            Courage = 10,
            Friendship = 30,
            Love = 20,
            Knowledge = 5
        };

        var dominant = _tracker.GetDominantEmotion(emotions);

        Assert.Equal(EmotionType.Friendship, dominant.Type);
        Assert.Equal(30, dominant.Value);
    }

    [Theory]
    [InlineData(DigimonPersonality.Brave, "勇气")]
    [InlineData(DigimonPersonality.Friendly, "友情")]
    [InlineData(DigimonPersonality.Gentle, "爱心")]
    [InlineData(DigimonPersonality.Curious, "知识")]
    public void GetEmotionContextHint_WithAffinity_IncludesHint(DigimonPersonality personality, string expectedAffinity)
    {
        var emotions = new EmotionValues
        {
            Courage = 25,
            Friendship = 25,
            Love = 25,
            Knowledge = 25
        };

        var hint = _tracker.GetEmotionContextHint(emotions, personality);

        Assert.NotNull(hint);
        Assert.Contains(expectedAffinity, hint);
    }
    
    [Theory]
    [InlineData(DigimonPersonality.Mischievous)]
    [InlineData(DigimonPersonality.Calm)]
    public void GetEmotionContextHint_WithoutAffinity_ReturnsHint(DigimonPersonality personality)
    {
        var emotions = new EmotionValues
        {
            Courage = 25,
            Friendship = 25,
            Love = 25,
            Knowledge = 25
        };

        var hint = _tracker.GetEmotionContextHint(emotions, personality);

        Assert.NotNull(hint);
    }
}
