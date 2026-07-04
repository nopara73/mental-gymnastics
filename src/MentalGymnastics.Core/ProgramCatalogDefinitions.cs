namespace MentalGymnastics.Core;

public sealed record BranchDefinition(
    BranchCode Code,
    string Name,
    BranchType Type,
    string ReasonToExist,
    string RelationshipToTree,
    string UnlockRule);

public sealed record BranchLevelRequirement(
    BranchCode Branch,
    GlobalLevelId Level,
    BranchLevelState RequiredState);

public sealed record BranchLevelRequirementGroup(
    IReadOnlyList<BranchLevelRequirement> Requirements);

public sealed record BranchUnlockDefinition(
    BranchCode Branch,
    IReadOnlyList<BranchCode> PrerequisiteBranches,
    IReadOnlyList<BranchLevelRequirement> RequiredLevels,
    IReadOnlyList<BranchLevelRequirementGroup> AnyOfLevelGroups);

public sealed record GlobalLevelDefinition(
    GlobalLevelId Id,
    string Name,
    string RealIncreaseInDemand,
    string TypicalGate);

public sealed record CapacityDefinition(
    CapacityId Id,
    string Name,
    IReadOnlyList<BranchCode> Branches,
    string WhatIsTrained,
    string LoadedBy,
    string ConstrainedBy,
    string TestedBy);

public sealed record DrillDefinition(
    DrillId Id,
    string Code,
    string Name,
    string Purpose,
    IReadOnlyList<CapacityId> CapacityTrained,
    string LoadApplied,
    string HonestyConstraint,
    string CleanPerformance,
    string FailureModes,
    string Regression);

public sealed record BranchLevelStandard(
    BranchCode Branch,
    GlobalLevelId Level,
    string Demand,
    string Standard,
    string Gate,
    string Stabilization,
    string Transfer);
