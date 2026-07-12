using MentalGymnastics.Content;

namespace MentalGymnastics.Content.Tests;

public sealed class VisualStimulusCodecTests
{
    [Fact]
    public void StimulusRoundTripsThroughCanonicalVersionedFormat()
    {
        var stimulus = new VisualStimulusSpec(
            VisualStimulusShape.Triangle,
            VisualStimulusColor.Amber,
            VisualStimulusFill.Striped,
            VisualStimulusSize.Large,
            Mark: VisualStimulusMark.Notch,
            MarkPosition: VisualStimulusPosition.Left,
            MarkCount: 2,
            MarkOrientation: VisualStimulusOrientation.DiagonalRight,
            Border: VisualStimulusBorder.Heavy);

        var encoded = VisualStimulusCodec.Encode(stimulus);

        Assert.Equal(
            "visual-stimulus-v1;shape=Triangle;color=Amber;fill=Striped;size=Large;" +
            "direction=None;mark=Notch;mark-position=Left;mark-count=2;" +
            "mark-orientation=DiagonalRight;border=Heavy",
            encoded);
        Assert.Equal(stimulus, VisualStimulusCodec.Decode(encoded));
        Assert.True(VisualStimulusCodec.TryDecode(encoded, out var decoded));
        Assert.Equal(stimulus, decoded);
        Assert.Equal(encoded, VisualStimulusCodec.Encode(decoded!));
    }

    [Fact]
    public void PairRoundTripsAndDerivesTruthFromExplicitRelevantFeature()
    {
        var first = new VisualStimulusSpec(
            VisualStimulusShape.Square,
            VisualStimulusColor.Blue,
            Mark: VisualStimulusMark.Dot,
            MarkCount: 2,
            Border: VisualStimulusBorder.Light);
        var second = first with
        {
            Color = VisualStimulusColor.Green,
            Border = VisualStimulusBorder.Heavy,
        };
        var pair = new VisualStimulusPairSpec(
            first,
            second,
            VisualStimulusFeature.MarkCount);

        var encoded = VisualStimulusCodec.EncodePair(pair);

        Assert.StartsWith(
            "visual-stimulus-pair-v1;feature=MarkCount;first.shape=Square;",
            encoded,
            StringComparison.Ordinal);
        Assert.True(pair.RelevantFeatureMatches);
        Assert.Equal("mark count", pair.RelevantFeatureName);
        Assert.Equal(pair, VisualStimulusCodec.DecodePair(encoded));
        Assert.True(VisualStimulusCodec.TryDecodePair(encoded, out var decoded));
        Assert.Equal(pair, decoded);
        Assert.Equal(encoded, VisualStimulusCodec.EncodePair(decoded!));

        var mismatch = pair with
        {
            Second = second with { MarkCount = 3 },
        };
        Assert.False(mismatch.RelevantFeatureMatches);
    }

    [Fact]
    public void ExceptionRoundTripsWithoutBreakingLegacyRuntimeTokenShape()
    {
        var exception = new VisualStimulusExceptionSpec(
            2,
            new VisualStimulusSpec(
                VisualStimulusShape.Diamond,
                VisualStimulusColor.Blue),
            VisualStimulusResponseAction.Tap,
            "blue diamond overrides the angular base rule");

        var encoded = VisualStimulusCodec.EncodeException(exception);

        Assert.StartsWith(
            "exception 2: exception-2 -> tap instead; visual-stimulus-exception-v1;" +
            "stimulus visual-stimulus-v1;",
            encoded,
            StringComparison.Ordinal);
        Assert.Contains(";reason blue diamond", encoded, StringComparison.Ordinal);
        Assert.Equal(exception, VisualStimulusCodec.DecodeException(encoded));
        Assert.True(VisualStimulusCodec.TryDecodeException(encoded, out var decoded));
        Assert.Equal(exception, decoded);
        Assert.Equal(encoded, VisualStimulusCodec.EncodeException(decoded!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("visual-stimulus-v2;shape=Square")]
    [InlineData("visual-stimulus-v1;shape=Hexagon;color=Blue;fill=Solid;size=Medium;direction=None;mark=None;mark-position=Center;mark-count=0;mark-orientation=None;border=Regular")]
    [InlineData("visual-stimulus-v1;shape=Square;color=Blue;fill=Solid;size=Medium;direction=None;mark=None;mark-position=Center;mark-count=1;mark-orientation=None;border=Regular")]
    public void InvalidStimulusMaterialIsRejected(string encoded)
    {
        Assert.False(VisualStimulusCodec.TryDecode(encoded, out var stimulus));
        Assert.Null(stimulus);
        Assert.ThrowsAny<Exception>(() => VisualStimulusCodec.Decode(encoded));
    }

    [Theory]
    [InlineData("")]
    [InlineData("visual-stimulus-pair-v2;feature=Shape")]
    [InlineData("visual-stimulus-pair-v1;feature=Texture")]
    public void InvalidPairMaterialIsRejected(string encoded)
    {
        Assert.False(VisualStimulusCodec.TryDecodePair(encoded, out var pair));
        Assert.Null(pair);
        Assert.ThrowsAny<Exception>(() => VisualStimulusCodec.DecodePair(encoded));
    }

    [Theory]
    [InlineData("")]
    [InlineData("exception 1: blue-diamond -> tap instead; prose is not a visual stimulus")]
    [InlineData("exception 1: exception-1 -> tap instead; visual-stimulus-exception-v2;stimulus visual-stimulus-v1;shape=Diamond;reason unknown wrapper version")]
    [InlineData("exception 1: exception-1 -> tap instead; visual-stimulus-exception-v1;stimulus visual-stimulus-v2;shape=Diamond;reason unknown stimulus version")]
    [InlineData("exception 1: exception-1 -> blink instead; visual-stimulus-exception-v1;stimulus visual-stimulus-v1;shape=Diamond;color=Blue;fill=Solid;size=Medium;direction=None;mark=None;mark-position=Center;mark-count=0;mark-orientation=None;border=Regular;reason unsupported action")]
    public void InvalidExceptionMaterialIsRejected(string encoded)
    {
        Assert.False(VisualStimulusCodec.TryDecodeException(encoded, out var exception));
        Assert.Null(exception);
        Assert.ThrowsAny<Exception>(() => VisualStimulusCodec.DecodeException(encoded));
    }

    [Fact]
    public void InvalidInMemorySpecificationsCannotBeEncoded()
    {
        var mismatchedMark = new VisualStimulusSpec(
            VisualStimulusShape.Square,
            VisualStimulusColor.Blue,
            MarkCount: 1);
        var directionOnNonArrow = new VisualStimulusSpec(
            VisualStimulusShape.Circle,
            VisualStimulusColor.Green,
            Direction: VisualStimulusDirection.East);
        var directionlessArrow = new VisualStimulusSpec(
            VisualStimulusShape.Arrow,
            VisualStimulusColor.Black);

        Assert.Throws<ArgumentException>(() => VisualStimulusCodec.Encode(mismatchedMark));
        Assert.Throws<ArgumentException>(() => VisualStimulusCodec.Encode(directionOnNonArrow));
        Assert.Throws<ArgumentException>(() => VisualStimulusCodec.Encode(directionlessArrow));
    }
}
