using DigimonBot.Core.Models;
using Xunit;

namespace DigimonBot.Core.Tests.Models;

public class EmotionValuesTests
{
    [Fact]
    public void GetValue_ReturnsCorrectValue()
    {
        var emotions = new EmotionValues
        {
            Courage = 10,
            Friendship = 20,
            Love = 30,
            Knowledge = 40
        };

        Assert.Equal(10, emotions.GetValue(EmotionType.Courage));
        Assert.Equal(20, emotions.GetValue(EmotionType.Friendship));
        Assert.Equal(30, emotions.GetValue(EmotionType.Love));
        Assert.Equal(40, emotions.GetValue(EmotionType.Knowledge));
    }

    [Fact]
    public void AddValue_IncreasesValue()
    {
        var emotions = new EmotionValues();
        
        emotions.AddValue(EmotionType.Courage, 5);
        emotions.AddValue(EmotionType.Friendship, 10);

        Assert.Equal(5, emotions.Courage);
        Assert.Equal(10, emotions.Friendship);
    }

    [Theory]
    [InlineData(10, 10, 10, 10, 100)] // 完全满足
    [InlineData(5, 10, 10, 10, 87.5)] // 勇气50%, 其他100%, 平均87.5%
    [InlineData(5, 5, 5, 5, 50)]      // 全部50%
    [InlineData(0, 0, 0, 0, 0)]       // 完全不满足
    public void CalculateMatchScore_ReturnsExpectedScore(int courage, int friendship, int love, int knowledge, double expectedScore)
    {
        var current = new EmotionValues
        {
            Courage = courage,
            Friendship = friendship,
            Love = love,
            Knowledge = knowledge
        };

        var requirements = new EmotionValues
        {
            Courage = 10,
            Friendship = 10,
            Love = 10,
            Knowledge = 10
        };

        var score = current.CalculateMatchScore(requirements);

        Assert.Equal(expectedScore, score * 100, precision: 1);
    }

    [Fact]
    public void MeetsRequirements_AllMet_ReturnsTrue()
    {
        var current = new EmotionValues
        {
            Courage = 20,
            Friendship = 20,
            Love = 20,
            Knowledge = 20
        };

        var requirements = new EmotionValues
        {
            Courage = 10,
            Friendship = 10,
            Love = 10,
            Knowledge = 10
        };

        Assert.True(current.MeetsRequirements(requirements));
    }

    [Fact]
    public void MeetsRequirements_NotMet_ReturnsFalse()
    {
        var current = new EmotionValues
        {
            Courage = 5,
            Friendship = 20,
            Love = 20,
            Knowledge = 20
        };

        var requirements = new EmotionValues
        {
            Courage = 10,
            Friendship = 10,
            Love = 10,
            Knowledge = 10
        };

        Assert.False(current.MeetsRequirements(requirements));
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0)]    // 全0
    [InlineData(10, 0, 0, 0, 20)]  // 单属性
    [InlineData(10, 10, 0, 0, 40)] // 双属性
    [InlineData(5, 5, 5, 5, 60)]   // 四属性
    public void CalculateComplexity_ReturnsExpectedValue(int c, int f, int l, int k, int expected)
    {
        var emotions = new EmotionValues
        {
            Courage = c,
            Friendship = f,
            Love = l,
            Knowledge = k
        };

        Assert.Equal(expected, emotions.CalculateComplexity());
    }

    [Fact]
    public void Clone_CreatesIndependentCopy()
    {
        var original = new EmotionValues
        {
            Courage = 10,
            Friendship = 20,
            Love = 30,
            Knowledge = 40
        };

        var clone = original.Clone();
        
        // 修改原始值
        original.Courage = 100;

        Assert.Equal(10, clone.Courage);
        Assert.Equal(100, original.Courage);
    }
}
