using DigimonBot.Core.Models;
using DigimonBot.Core.Services;
using Xunit;

namespace DigimonBot.Core.Tests.Services;

public class EvolutionEngineTests
{
    private readonly EvolutionEngine _engine;
    private readonly Dictionary<string, DigimonDefinition> _digimonDb;

    public EvolutionEngineTests()
    {
        _engine = new EvolutionEngine();
        _digimonDb = CreateTestDigimonDatabase();
    }

    private Dictionary<string, DigimonDefinition> CreateTestDigimonDatabase()
    {
        return new Dictionary<string, DigimonDefinition>
        {
            ["baby"] = new DigimonDefinition
            {
                Id = "baby",
                Name = "幼年期",
                Stage = DigimonStage.Baby2,
                NextEvolutions = new List<EvolutionOption>
                {
                    new EvolutionOption
                    {
                        TargetId = "child_courage",
                        Requirements = new EmotionValues { Courage = 10 },
                        MinTokens = 1000,
                        Priority = 1
                    },
                    new EvolutionOption
                    {
                        TargetId = "child_friendship",
                        Requirements = new EmotionValues { Friendship = 10 },
                        MinTokens = 1000,
                        Priority = 1
                    }
                }
            },
            ["child_courage"] = new DigimonDefinition
            {
                Id = "child_courage",
                Name = "勇气成长期",
                Stage = DigimonStage.Child
            },
            ["child_friendship"] = new DigimonDefinition
            {
                Id = "child_friendship",
                Name = "友情成长期",
                Stage = DigimonStage.Child
            },
            ["ultimate"] = new DigimonDefinition
            {
                Id = "ultimate",
                Name = "究极体",
                Stage = DigimonStage.Ultimate,
                NextEvolutions = new List<EvolutionOption>
                {
                    new EvolutionOption
                    {
                        TargetId = "egg",
                        Requirements = new EmotionValues(),
                        MinTokens = 1000,
                        Priority = 1
                    }
                }
            },
            ["egg"] = new DigimonDefinition
            {
                Id = "egg",
                Name = "数码蛋",
                Stage = DigimonStage.Baby1
            }
        };
    }

    [Fact]
    public async Task CheckAndEvolveAsync_TokenNotEnough_NoEvolution()
    {
        var userDigimon = new UserDigimon
        {
            CurrentDigimonId = "baby",
            Emotions = new EmotionValues { Courage = 20 }, // 情感满足
            TotalTokensConsumed = 500 // Token不足
        };

        var result = await _engine.CheckAndEvolveAsync(userDigimon, _digimonDb);

        Assert.Null(result);
    }

    [Fact]
    public async Task CheckAndEvolveAsync_EmotionNotEnough_NoEvolution()
    {
        var userDigimon = new UserDigimon
        {
            CurrentDigimonId = "baby",
            Emotions = new EmotionValues { Courage = 5 }, // 情感不足
            TotalTokensConsumed = 2000 // Token满足
        };

        var result = await _engine.CheckAndEvolveAsync(userDigimon, _digimonDb);

        Assert.Null(result);
    }

    [Fact]
    public async Task CheckAndEvolveAsync_AllConditionsMet_Evolves()
    {
        var userDigimon = new UserDigimon
        {
            CurrentDigimonId = "baby",
            Emotions = new EmotionValues { Courage = 20 },
            TotalTokensConsumed = 2000
        };

        var result = await _engine.CheckAndEvolveAsync(userDigimon, _digimonDb);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("baby", result.OldDigimonId);
        Assert.Equal("child_courage", result.NewDigimonId);
    }

    [Fact]
    public async Task CheckAndEvolveAsync_MultipleOptions_SelectsByComplexity()
    {
        // 修改数据库，添加更复杂的进化选项
        _digimonDb["baby"].NextEvolutions.Add(new EvolutionOption
        {
            TargetId = "child_complex",
            Requirements = new EmotionValues { Courage = 10, Friendship = 10, Love = 10 },
            MinTokens = 1000,
            Priority = 1
        });
        _digimonDb["child_complex"] = new DigimonDefinition
        {
            Id = "child_complex",
            Name = "复杂成长期",
            Stage = DigimonStage.Child
        };

        var userDigimon = new UserDigimon
        {
            CurrentDigimonId = "baby",
            Emotions = new EmotionValues { Courage = 20, Friendship = 20, Love = 20 },
            TotalTokensConsumed = 2000
        };

        var result = await _engine.CheckAndEvolveAsync(userDigimon, _digimonDb);

        Assert.NotNull(result);
        // 应该选择更复杂的进化路线
        Assert.Equal("child_complex", result.NewDigimonId);
    }

    [Fact]
    public async Task CheckAndEvolveAsync_FinalForm_Rebirths()
    {
        var userDigimon = new UserDigimon
        {
            CurrentDigimonId = "ultimate",
            Emotions = new EmotionValues(),
            TotalTokensConsumed = 2000
        };

        var result = await _engine.CheckAndEvolveAsync(userDigimon, _digimonDb);

        Assert.NotNull(result);
        Assert.True(result.IsRebirth);
        Assert.Equal("egg", result.NewDigimonId);
    }

    [Fact]
    public void GetProgress_ReturnsCorrectValues()
    {
        var userDigimon = new UserDigimon
        {
            CurrentDigimonId = "baby",
            Emotions = new EmotionValues { Courage = 5 },
            TotalTokensConsumed = 500
        };

        var progress = _engine.GetProgress(userDigimon, _digimonDb["baby"]);

        Assert.Equal(500, progress.CurrentTokens);
        Assert.Equal(1000, progress.RequiredTokens);
        Assert.Equal(50, progress.TokenProgressPercent);
    }

    [Fact]
    public void GetPossibleEvolutions_ReturnsAllOptions()
    {
        var userDigimon = new UserDigimon
        {
            CurrentDigimonId = "baby",
            Emotions = new EmotionValues { Courage = 20, Friendship = 20 },
            TotalTokensConsumed = 2000
        };

        var possible = _engine.GetPossibleEvolutions(userDigimon, _digimonDb["baby"], _digimonDb);

        Assert.Equal(2, possible.Count);
    }
}
