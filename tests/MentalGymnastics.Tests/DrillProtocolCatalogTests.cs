using MentalGymnastics.Core;

namespace MentalGymnastics.Tests;

public sealed class DrillProtocolCatalogTests
{
    [Fact]
    public void ExposesStandardDrillProtocolsInProgramOrder()
    {
        Assert.Equal(
            new[]
            {
                (DrillId.FH1TargetHold, "FH-1", "Target Hold"),
                (DrillId.FH2DistractorHold, "FH-2", "Distractor Hold"),
                (DrillId.FS1CueSwitch, "FS-1", "Cue Switch"),
                (DrillId.FS2InvalidCueFilter, "FS-2", "Invalid Cue Filter"),
                (DrillId.WM1DelayedReconstruction, "WM-1", "Delayed Reconstruction"),
                (DrillId.WM2MentalTransform, "WM-2", "Mental Transform"),
                (DrillId.IR1GoNoGoRule, "IR-1", "Go/No-Go Rule"),
                (DrillId.IR2ExceptionRule, "IR-2", "Exception Rule"),
                (DrillId.DE1PairDiscrimination, "DE-1", "Pair Discrimination"),
                (DrillId.DE2SeededAudit, "DE-2", "Seeded Audit"),
                (DrillId.CO1RuleExtraction, "CO-1", "Rule Extraction"),
                (DrillId.CO2StructureMapping, "CO-2", "Structure Mapping"),
                (DrillId.AI1PressureRepeat, "AI-1", "Pressure Repeat"),
                (DrillId.AI2DisruptionRecovery, "AI-2", "Disruption Recovery"),
                (DrillId.TI1CompositeTask, "TI-1", "Composite Task"),
                (DrillId.TI2GlobalReviewTask, "TI-2", "Global Review Task"),
            },
            DrillProtocolCatalog.StandardDrills.Select(protocol => (protocol.Id, protocol.Code, protocol.Name)));
    }

    [Fact]
    public void EachStandardDrillProtocolPreservesRequiredProgrammingFields()
    {
        Assert.All(
            DrillProtocolCatalog.StandardDrills,
            protocol =>
            {
                Assert.False(string.IsNullOrWhiteSpace(protocol.Purpose));
                Assert.NotEmpty(protocol.CapacitiesTrained);
                Assert.NotEmpty(protocol.LoadApplied);
                Assert.NotEmpty(protocol.HonestyConstraints);
                Assert.False(string.IsNullOrWhiteSpace(protocol.CleanPerformance));
                Assert.NotEmpty(protocol.FailureModes);
                Assert.False(string.IsNullOrWhiteSpace(protocol.Regression.Description));
                Assert.NotEmpty(protocol.Regression.PreservedHonestyConstraints);
            });
    }

    [Fact]
    public void RepresentsFocusHoldTargetHoldProtocol()
    {
        var protocol = DrillProtocolCatalog.StandardDrills.Single(
            drill => drill.Id == DrillId.FH1TargetHold);

        Assert.Equal("Build selective hold.", protocol.Purpose);
        Assert.Equal(
            [CapacityId.SelectiveHold, CapacityId.ReturnAfterDrift],
            protocol.CapacitiesTrained);
        Assert.Equal(
            ["Duration", "target subtlety", "distractor salience."],
            protocol.LoadApplied.Select(load => load.Description));
        Assert.Equal(
            ["Target is stated before set", "every drift is marked."],
            protocol.HonestyConstraints.Select(constraint => constraint.Description));
        Assert.Equal(
            "Target maintained within drift threshold; returns inside time window.",
            protocol.CleanPerformance);
        Assert.Equal(
            ["Unmarked drift", "target substitution", "restarting after drift."],
            protocol.FailureModes.Select(mode => mode.Description));
        Assert.Equal("Shorter duration with same marking rule.", protocol.Regression.Description);
        Assert.Equal(protocol.HonestyConstraints, protocol.Regression.PreservedHonestyConstraints);
    }

    [Fact]
    public void RepresentsTransferIntegrationGlobalReviewProtocol()
    {
        var protocol = DrillProtocolCatalog.StandardDrills.Single(
            drill => drill.Id == DrillId.TI2GlobalReviewTask);

        Assert.Equal("Test integrated performance over review cycle.", protocol.Purpose);
        Assert.Equal(
            [CapacityId.IntegratedTaskControl, CapacityId.ErrorAudit, CapacityId.EncodingFidelity],
            protocol.CapacitiesTrained);
        Assert.Equal(
            ["Task length", "pressure", "ambiguity", "delay."],
            protocol.LoadApplied.Select(load => load.Description));
        Assert.Equal(
            ["Audit and delayed reconstruction are required", "no rereading after encode window."],
            protocol.HonestyConstraints.Select(constraint => constraint.Description));
        Assert.Equal(
            "Composite, audit, and delayed reconstruction all pass.",
            protocol.CleanPerformance);
        Assert.Equal(
            ["Good product with failed audit", "memory gap", "rule drift under pressure."],
            protocol.FailureModes.Select(mode => mode.Description));
        Assert.Equal(
            "Shorter composite with same audit and delay requirements.",
            protocol.Regression.Description);
    }

    [Fact]
    public void ProtocolCatalogMatchesDocumentedDrillDefinitions()
    {
        var protocolPairs = DrillProtocolCatalog.StandardDrills
            .Zip(ProgramCatalog.Drills)
            .ToArray();

        Assert.Equal(ProgramCatalog.Drills.Count, protocolPairs.Length);
        Assert.All(
            protocolPairs,
            pair =>
            {
                var protocol = pair.First;
                var definition = pair.Second;

                Assert.Equal(definition.Id, protocol.Id);
                Assert.Equal(definition.Code, protocol.Code);
                Assert.Equal(definition.Name, protocol.Name);
                Assert.Equal(definition.Purpose, protocol.Purpose);
                Assert.Equal(definition.CapacityTrained, protocol.CapacitiesTrained);
                Assert.Equal(definition.LoadApplied, string.Join(", ", protocol.LoadApplied.Select(load => load.Description)));
                Assert.Equal(
                    definition.HonestyConstraint,
                    string.Join("; ", protocol.HonestyConstraints.Select(constraint => constraint.Description)));
                Assert.Equal(definition.CleanPerformance, protocol.CleanPerformance);
                Assert.Equal(
                    definition.FailureModes,
                    string.Join(", ", protocol.FailureModes.Select(mode => mode.Description)));
                Assert.Equal(definition.Regression, protocol.Regression.Description);
            });
    }
}
