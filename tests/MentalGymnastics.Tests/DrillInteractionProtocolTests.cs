using MentalGymnastics.Core;

namespace MentalGymnastics.Tests;

public sealed class DrillInteractionProtocolTests
{
    [Fact]
    public void EveryDrillHasOneCompletePhysicalInteractionProtocol()
    {
        Assert.Equal(Enum.GetValues<DrillId>().Length, DrillInteractionProtocolCatalog.Protocols.Count);
        Assert.Equal(
            Enum.GetValues<DrillId>().OrderBy(drill => drill),
            DrillInteractionProtocolCatalog.Protocols.Select(protocol => protocol.Drill).OrderBy(drill => drill));
        Assert.All(DrillInteractionProtocolCatalog.Protocols, protocol =>
        {
            Assert.False(string.IsNullOrWhiteSpace(protocol.AttentionInstruction));
            Assert.False(string.IsNullOrWhiteSpace(protocol.DeviceInstruction));
            Assert.False(string.IsNullOrWhiteSpace(protocol.ActionInstruction));
            Assert.False(string.IsNullOrWhiteSpace(protocol.ScreenBehavior));
            Assert.Equal(3, protocol.Steps.Count);
        });
    }

    [Fact]
    public void FocusHoldExplicitlyKeepsEyesOpenAndProvidesReachableAcknowledgedControls()
    {
        var protocol = DrillInteractionProtocolCatalog.Get(DrillId.FH1TargetHold);

        Assert.Equal(DrillInteractionInputKind.FocusWanderSurface, protocol.InputKind);
        Assert.Contains("Eyes open", protocol.AttentionInstruction, StringComparison.Ordinal);
        Assert.Equal("FOCUS", protocol.Steps[0].Label);
        Assert.Equal("WANDERED", protocol.Steps[1].Label);
        Assert.Equal("RESUME", protocol.Steps[2].Label);
        Assert.Contains("tap anywhere", protocol.DeviceInstruction, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tap anywhere once", protocol.ActionInstruction, StringComparison.Ordinal);
        Assert.Contains("no second action", protocol.ActionInstruction, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("No return timer or second tap", protocol.Steps[2].Instruction, StringComparison.Ordinal);
        Assert.DoesNotContain("position", protocol.AttentionInstruction, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("position", protocol.Steps[0].Instruction, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(DrillInteractionAcknowledgement.Haptic, protocol.Acknowledgement);
    }

    [Theory]
    [InlineData(DrillId.IR1GoNoGoRule)]
    [InlineData(DrillId.IR2ExceptionRule)]
    public void InhibitionDefinesNoTouchAsTheWithholdResponse(DrillId drill)
    {
        var protocol = DrillInteractionProtocolCatalog.Get(drill);

        Assert.Equal(DrillInteractionInputKind.GoNoGoPad, protocol.InputKind);
        Assert.Contains("do not", protocol.ActionInstruction, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("WITHHOLD", protocol.ActionInstruction, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(DrillId.WM1DelayedReconstruction)]
    [InlineData(DrillId.WM2MentalTransform)]
    public void WorkingMemoryDefinesTheScreenHideAndNoNotesBoundary(DrillId drill)
    {
        var protocol = DrillInteractionProtocolCatalog.Get(drill);

        Assert.Contains("hidden", protocol.AttentionInstruction, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("notes", protocol.DeviceInstruction, StringComparison.OrdinalIgnoreCase);
    }
}
