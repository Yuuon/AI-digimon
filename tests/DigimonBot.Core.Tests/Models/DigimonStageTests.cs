using DigimonBot.Core.Models;
using Xunit;

namespace DigimonBot.Core.Tests.Models;

public class DigimonStageTests
{
    [Theory]
    [InlineData(DigimonStage.Baby1, 20)]
    [InlineData(DigimonStage.Baby2, 50)]
    [InlineData(DigimonStage.Child, 150)]
    [InlineData(DigimonStage.Adult, 300)]
    [InlineData(DigimonStage.Perfect, 400)]
    [InlineData(DigimonStage.Ultimate, 500)]
    [InlineData(DigimonStage.SuperUltimate, 600)]
    public void GetCapabilities_ReturnsCorrectMaxLength(DigimonStage stage, int expectedLength)
    {
        var caps = stage.GetCapabilities();
        Assert.Equal(expectedLength, caps.MaxResponseLength);
    }

    [Theory]
    [InlineData(DigimonStage.Baby1, false)]
    [InlineData(DigimonStage.Baby2, false)]
    [InlineData(DigimonStage.Child, true)]
    [InlineData(DigimonStage.Adult, true)]
    public void GetCapabilities_ComplexSentenceCapability(DigimonStage stage, bool canUseComplex)
    {
        var caps = stage.GetCapabilities();
        Assert.Equal(canUseComplex, caps.CanUseComplexSentences);
    }

    [Theory]
    [InlineData(DigimonStage.Baby1, false)]
    [InlineData(DigimonStage.Baby2, false)]
    [InlineData(DigimonStage.Child, false)]
    [InlineData(DigimonStage.Adult, true)]
    [InlineData(DigimonStage.Ultimate, true)]
    public void GetCapabilities_AbstractTopicCapability(DigimonStage stage, bool canDiscussAbstract)
    {
        var caps = stage.GetCapabilities();
        Assert.Equal(canDiscussAbstract, caps.CanDiscussAbstractTopics);
    }

    [Theory]
    [InlineData(DigimonStage.Baby1, false)]
    [InlineData(DigimonStage.Baby2, false)]
    [InlineData(DigimonStage.Child, false)]
    [InlineData(DigimonStage.Ultimate, true)]
    [InlineData(DigimonStage.SuperUltimate, true)]
    public void IsFinalForm_ReturnsCorrectValue(DigimonStage stage, bool isFinal)
    {
        Assert.Equal(isFinal, stage.IsFinalForm());
    }
}
