using Craftiger.Builder.Models.Planner;

namespace Craftiger.Builder.UnitTests;

public sealed class VoltageTierTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(4, 1)]
    [InlineData(32, 1)]
    [InlineData(33, 2)]
    [InlineData(128, 2)]
    [InlineData(512, 3)]
    [InlineData(2048, 4)]
    [InlineData(2049, 5)]
    public void MatchesVoltageLadder(long euT, int expected) =>
        Assert.Equal(expected, TierLadder.VoltageTier(euT));

    [Fact]
    public void GtTierLabelsAreAuthoritative()
    {
        Assert.Equal(1, TierLadder.LabelTier("ULV"));
        Assert.Equal(1, TierLadder.LabelTier("LV"));
        Assert.Equal(2, TierLadder.LabelTier("MV"));
        Assert.Equal(14, TierLadder.LabelTier("MAX"));
        Assert.Null(TierLadder.LabelTier(null));
        Assert.Null(TierLadder.LabelTier("bogus"));
    }
}
