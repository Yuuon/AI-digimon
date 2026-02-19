using DigimonBot.AI.Services;
using DigimonBot.Core.Models;
using Xunit;

namespace DigimonBot.AI.Tests.Services;

public class PersonalityEngineTests
{
    private readonly PersonalityEngine _engine;

    public PersonalityEngineTests()
    {
        _engine = new PersonalityEngine();
    }

    [Fact]
    public void BuildSystemPrompt_ContainsBasicInfo()
    {
        var digimon = new DigimonDefinition
        {
            Name = "亚古兽",
            Stage = DigimonStage.Child,
            Personality = DigimonPersonality.Brave,
            BasePrompt = "这是基础设定"
        };

        var userDigimon = new UserDigimon
        {
            Emotions = new EmotionValues { Courage = 20 }
        };

        var prompt = _engine.BuildSystemPrompt(digimon, userDigimon);

        Assert.Contains("亚古兽", prompt);
        Assert.Contains("成长期", prompt);
        Assert.Contains("勇敢", prompt);
        Assert.Contains("这是基础设定", prompt);
    }

    [Fact]
    public void BuildSystemPrompt_ContainsStageConstraints()
    {
        var digimon = new DigimonDefinition
        {
            Name = "测试",
            Stage = DigimonStage.Baby1,
            Personality = DigimonPersonality.Brave,
            BasePrompt = "基础"
        };

        var userDigimon = new UserDigimon();
        var prompt = _engine.BuildSystemPrompt(digimon, userDigimon);

        Assert.Contains("20字", prompt); // Baby1的最大字数
    }

    [Fact]
    public void BuildSystemPrompt_ContainsPersonalityTraits()
    {
        var digimon = new DigimonDefinition
        {
            Name = "测试",
            Stage = DigimonStage.Child,
            Personality = DigimonPersonality.Brave,
            BasePrompt = "基础"
        };

        var userDigimon = new UserDigimon();
        var prompt = _engine.BuildSystemPrompt(digimon, userDigimon);

        Assert.Contains("勇敢", prompt);
        Assert.Contains("直接果断", prompt);
    }

    [Fact]
    public void BuildSystemPrompt_ContainsEmotionContext()
    {
        var digimon = new DigimonDefinition
        {
            Name = "测试",
            Stage = DigimonStage.Child,
            Personality = DigimonPersonality.Brave,
            BasePrompt = "基础"
        };

        var userDigimon = new UserDigimon
        {
            Emotions = new EmotionValues { Courage = 30 }
        };

        var prompt = _engine.BuildSystemPrompt(digimon, userDigimon);

        Assert.Contains("勇气", prompt);
        Assert.Contains("30", prompt);
    }

    [Theory]
    [InlineData(DigimonStage.Baby1, "20字")]
    [InlineData(DigimonStage.Child, "150字")]
    [InlineData(DigimonStage.Adult, "300字")]
    public void GetStageConstraints_ReturnsCorrectLimits(DigimonStage stage, string expectedText)
    {
        var constraints = _engine.GetStageConstraints(stage);

        Assert.Contains(expectedText, constraints);
    }

    [Theory]
    [InlineData(true, "新的生命")]
    [InlineData(false, "进化")]
    public void BuildEvolutionAnnouncement_RebirthFlag(bool isRebirth, string expectedText)
    {
        var newDigimon = new DigimonDefinition
        {
            Name = "测试兽",
            Stage = DigimonStage.Child
        };

        var announcement = _engine.BuildEvolutionAnnouncement(newDigimon, isRebirth);

        Assert.Contains(expectedText, announcement);
    }

    [Theory]
    [InlineData(DigimonStage.Ultimate, "究极体")]
    [InlineData(DigimonStage.SuperUltimate, "超究极体")]
    public void BuildEvolutionAnnouncement_FinalForms_HasSpecialText(DigimonStage stage, string expectedText)
    {
        var newDigimon = new DigimonDefinition
        {
            Name = "究极兽",
            Stage = stage
        };

        var announcement = _engine.BuildEvolutionAnnouncement(newDigimon, false);

        Assert.Contains(expectedText, announcement);
    }

    [Theory]
    [InlineData(DigimonStage.Baby2, "幼年期II")]
    [InlineData(DigimonStage.Child, "成长期")]
    [InlineData(DigimonStage.Adult, "成熟期")]
    [InlineData(DigimonStage.Perfect, "完全体")]
    public void ToDisplayName_Stage_ReturnsChineseName(DigimonStage stage, string expectedName)
    {
        var result = stage.ToDisplayName();

        Assert.Equal(expectedName, result);
    }

    [Theory]
    [InlineData(DigimonPersonality.Brave, "勇敢")]
    [InlineData(DigimonPersonality.Friendly, "友善")]
    [InlineData(DigimonPersonality.Gentle, "温柔")]
    [InlineData(DigimonPersonality.Curious, "好奇")]
    public void ToDisplayName_Personality_ReturnsChineseName(DigimonPersonality personality, string expectedName)
    {
        var result = personality.ToDisplayName();

        Assert.Equal(expectedName, result);
    }

    [Theory]
    [InlineData(DigimonPersonality.Brave, EmotionType.Courage)]
    [InlineData(DigimonPersonality.Friendly, EmotionType.Friendship)]
    [InlineData(DigimonPersonality.Gentle, EmotionType.Love)]
    [InlineData(DigimonPersonality.Curious, EmotionType.Knowledge)]
    [InlineData(DigimonPersonality.Mischievous, null)]
    [InlineData(DigimonPersonality.Calm, null)]
    public void GetAffinityEmotion_ReturnsExpectedEmotion(DigimonPersonality personality, EmotionType? expected)
    {
        var result = personality.GetAffinityEmotion();

        Assert.Equal(expected, result);
    }
}
