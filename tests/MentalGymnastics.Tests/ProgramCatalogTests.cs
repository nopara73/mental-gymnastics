using MentalGymnastics.Core;

namespace MentalGymnastics.Tests;

public sealed class ProgramCatalogTests
{
    [Fact]
    public void ExposesFixedBranchesInProgramOrder()
    {
        Assert.Equal(
            new[]
            {
                (BranchCode.FH, "Focus Hold", BranchType.Foundational),
                (BranchCode.FS, "Focus Shift and Recovery", BranchType.Foundational),
                (BranchCode.WM, "Working Memory and Reconstruction", BranchType.Foundational),
                (BranchCode.IR, "Inhibition and Response Control", BranchType.Foundational),
                (BranchCode.DE, "Discrimination and Error Checking", BranchType.Foundational),
                (BranchCode.CO, "Concept Operations", BranchType.Advanced),
                (BranchCode.AI, "Affective Interference Control", BranchType.Advanced),
                (BranchCode.TI, "Transfer Integration", BranchType.Advanced),
            },
            ProgramCatalog.Branches.Select(branch => (branch.Code, branch.Name, branch.Type)));

        Assert.Equal("Universal start.", ProgramCatalog.Branches.Single(branch => branch.Code == BranchCode.FH).UnlockRule);
        Assert.Equal(
            "All foundational L3 owned; at least one CO L2 or AI L2 owned.",
            ProgramCatalog.Branches.Single(branch => branch.Code == BranchCode.TI).UnlockRule);
    }

    [Fact]
    public void ExposesGlobalLevelsInProgramOrder()
    {
        Assert.Equal(
            new[]
            {
                (GlobalLevelId.L1, "Protected Control", "Clean execution in protected drill conditions."),
                (GlobalLevelId.L2, "Duration and Density", "Clean execution across repeated sets."),
                (GlobalLevelId.L3, "Interference and Delay", "Clean execution after adjacent work or interruption."),
                (GlobalLevelId.L4, "Transfer", "Transfer test passed and retested."),
                (GlobalLevelId.L5, "Integration Under Pressure", "Global review plus branch test under pressure."),
            },
            ProgramCatalog.GlobalLevels.Select(level => (level.Id, level.Name, level.TypicalGate)));

        Assert.Contains(
            "multiple branches are active",
            ProgramCatalog.GlobalLevels.Single(level => level.Id == GlobalLevelId.L5).RealIncreaseInDemand);
    }

    [Fact]
    public void StableEnumerationsMatchProgramVocabulary()
    {
        AssertEnumNames<BranchType>("Foundational", "Advanced");
        AssertEnumNames<BranchCode>("FH", "FS", "WM", "IR", "DE", "CO", "AI", "TI");
        AssertEnumNames<GlobalLevelId>("L1", "L2", "L3", "L4", "L5");
        AssertEnumNames<CapacityId>(
            "SelectiveHold",
            "ReturnAfterDrift",
            "DeliberateSwitching",
            "EncodingFidelity",
            "ManipulationInMind",
            "ResponseInhibition",
            "RuleFidelity",
            "FineDiscrimination",
            "ErrorAudit",
            "RuleExtraction",
            "AbstractionMapping",
            "PressureStableExecution",
            "RecoveryAfterDisruption",
            "IntegratedTaskControl");
        AssertEnumNames<DrillId>(
            "FH1TargetHold",
            "FH2DistractorHold",
            "FS1CueSwitch",
            "FS2InvalidCueFilter",
            "WM1DelayedReconstruction",
            "WM2MentalTransform",
            "IR1GoNoGoRule",
            "IR2ExceptionRule",
            "DE1PairDiscrimination",
            "DE2SeededAudit",
            "CO1RuleExtraction",
            "CO2StructureMapping",
            "AI1PressureRepeat",
            "AI2DisruptionRecovery",
            "TI1CompositeTask",
            "TI2GlobalReviewTask");
        AssertEnumNames<SessionType>("Practice", "Load", "Test", "Stabilization", "Regression", "Transfer", "Recovery");
        AssertEnumNames<GateOutcome>("Fail", "PassOnce", "Stabilize", "Own", "Maintain", "Regress", "Review");
        AssertEnumNames<FailureType>("TechnicalFailure", "EffortFailure", "Overload", "BadProgramming");
        AssertEnumNames<BranchLevelState>(
            "Unopened",
            "Training",
            "TestReady",
            "PassedOnce",
            "Stabilizing",
            "Owned",
            "Maintenance",
            "Decayed");
    }

    [Fact]
    public void ExposesCapacityDrillAndStandardCatalogs()
    {
        Assert.Equal(14, ProgramCatalog.Capacities.Count);
        Assert.Equal("Selective hold", ProgramCatalog.Capacities[0].Name);
        Assert.Equal(new[] { BranchCode.FH }, ProgramCatalog.Capacities[0].Branches);
        Assert.Equal("Integrated task control", ProgramCatalog.Capacities[^1].Name);
        Assert.Equal(new[] { BranchCode.TI }, ProgramCatalog.Capacities[^1].Branches);

        Assert.Equal(16, ProgramCatalog.Drills.Count);
        Assert.Equal((DrillId.FH1TargetHold, "FH-1", "Target Hold"), ToDrillTuple(ProgramCatalog.Drills[0]));
        Assert.Equal((DrillId.TI2GlobalReviewTask, "TI-2", "Global Review Task"), ToDrillTuple(ProgramCatalog.Drills[^1]));

        var branchLevelPairs = ProgramCatalog.Standards
            .Select(standard => (standard.Branch, standard.Level))
            .ToArray();

        Assert.Equal(40, branchLevelPairs.Length);
        Assert.Equal(
            ProgramCatalog.Branches.SelectMany(
                _ => ProgramCatalog.GlobalLevels,
                (branch, level) => (branch.Code, level.Id)),
            branchLevelPairs);

        var focusHoldL1 = ProgramCatalog.Standards.Single(
            standard => standard.Branch == BranchCode.FH && standard.Level == GlobalLevelId.L1);
        Assert.Equal("Hold one simple target for 3 minutes.", focusHoldL1.Demand);
        Assert.Equal(
            "No more than 5 marked drifts; each return within 10 seconds; no target change.",
            focusHoldL1.Standard);

        var transferIntegrationL5 = ProgramCatalog.Standards.Single(
            standard => standard.Branch == BranchCode.TI && standard.Level == GlobalLevelId.L5);
        Assert.Equal("Global performance review task.", transferIntegrationL5.Demand);
        Assert.Equal("Global review itself.", transferIntegrationL5.Transfer);
    }

    private static void AssertEnumNames<TEnum>(params string[] expected)
        where TEnum : struct, Enum
    {
        Assert.Equal(expected, Enum.GetNames<TEnum>());
    }

    private static (DrillId Id, string Code, string Name) ToDrillTuple(DrillDefinition drill)
    {
        return (drill.Id, drill.Code, drill.Name);
    }
}
