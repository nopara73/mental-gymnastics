using MentalGymnastics.Core;

namespace MentalGymnastics.Persistence;

public interface IStableDomainIdentifierMap<TDomain>
    where TDomain : struct, Enum
{
    IReadOnlyDictionary<TDomain, string> PersistedIds { get; }

    string ToPersistedId(TDomain value);

    bool TryFromPersistedId(string persistedId, out TDomain value);

    TDomain FromPersistedId(string persistedId);
}

public static class StableDomainIdentifiers
{
    public static IStableDomainIdentifierMap<BranchCode> Branches { get; } =
        new StableDomainIdentifierMap<BranchCode>(new Dictionary<BranchCode, string>
        {
            [BranchCode.FH] = "FH",
            [BranchCode.FS] = "FS",
            [BranchCode.WM] = "WM",
            [BranchCode.IR] = "IR",
            [BranchCode.DE] = "DE",
            [BranchCode.CO] = "CO",
            [BranchCode.AI] = "AI",
            [BranchCode.TI] = "TI",
        });

    public static IStableDomainIdentifierMap<GlobalLevelId> Levels { get; } =
        new StableDomainIdentifierMap<GlobalLevelId>(new Dictionary<GlobalLevelId, string>
        {
            [GlobalLevelId.L1] = "L1",
            [GlobalLevelId.L2] = "L2",
            [GlobalLevelId.L3] = "L3",
            [GlobalLevelId.L4] = "L4",
            [GlobalLevelId.L5] = "L5",
        });

    public static IStableDomainIdentifierMap<DrillId> Drills { get; } =
        new StableDomainIdentifierMap<DrillId>(new Dictionary<DrillId, string>
        {
            [DrillId.FH1TargetHold] = "FH1TargetHold",
            [DrillId.FH2DistractorHold] = "FH2DistractorHold",
            [DrillId.FS1CueSwitch] = "FS1CueSwitch",
            [DrillId.FS2InvalidCueFilter] = "FS2InvalidCueFilter",
            [DrillId.WM1DelayedReconstruction] = "WM1DelayedReconstruction",
            [DrillId.WM2MentalTransform] = "WM2MentalTransform",
            [DrillId.IR1GoNoGoRule] = "IR1GoNoGoRule",
            [DrillId.IR2ExceptionRule] = "IR2ExceptionRule",
            [DrillId.DE1PairDiscrimination] = "DE1PairDiscrimination",
            [DrillId.DE2SeededAudit] = "DE2SeededAudit",
            [DrillId.CO1RuleExtraction] = "CO1RuleExtraction",
            [DrillId.CO2StructureMapping] = "CO2StructureMapping",
            [DrillId.AI1PressureRepeat] = "AI1PressureRepeat",
            [DrillId.AI2DisruptionRecovery] = "AI2DisruptionRecovery",
            [DrillId.TI1CompositeTask] = "TI1CompositeTask",
            [DrillId.TI2GlobalReviewTask] = "TI2GlobalReviewTask",
        });

    public static IStableDomainIdentifierMap<SessionType> SessionTypes { get; } =
        new StableDomainIdentifierMap<SessionType>(new Dictionary<SessionType, string>
        {
            [SessionType.Practice] = "Practice",
            [SessionType.Load] = "Load",
            [SessionType.Test] = "Test",
            [SessionType.Stabilization] = "Stabilization",
            [SessionType.Regression] = "Regression",
            [SessionType.Transfer] = "Transfer",
            [SessionType.Recovery] = "Recovery",
        });

    public static IStableDomainIdentifierMap<GateOutcome> GateOutcomes { get; } =
        new StableDomainIdentifierMap<GateOutcome>(new Dictionary<GateOutcome, string>
        {
            [GateOutcome.Fail] = "Fail",
            [GateOutcome.PassOnce] = "PassOnce",
            [GateOutcome.Stabilize] = "Stabilize",
            [GateOutcome.Own] = "Own",
            [GateOutcome.Maintain] = "Maintain",
            [GateOutcome.Regress] = "Regress",
            [GateOutcome.Review] = "Review",
        });

    public static IStableDomainIdentifierMap<FailureType> FailureTypes { get; } =
        new StableDomainIdentifierMap<FailureType>(new Dictionary<FailureType, string>
        {
            [FailureType.TechnicalFailure] = "TechnicalFailure",
            [FailureType.EffortFailure] = "EffortFailure",
            [FailureType.Overload] = "Overload",
            [FailureType.BadProgramming] = "BadProgramming",
        });

    public static IStableDomainIdentifierMap<MaintenanceCurrencyState> MaintenanceStates { get; } =
        new StableDomainIdentifierMap<MaintenanceCurrencyState>(new Dictionary<MaintenanceCurrencyState, string>
        {
            [MaintenanceCurrencyState.Current] = "Current",
            [MaintenanceCurrencyState.Due] = "Due",
            [MaintenanceCurrencyState.Warning] = "Warning",
            [MaintenanceCurrencyState.Failed] = "Failed",
        });

    public static IStableDomainIdentifierMap<BranchLevelState> BranchLevelStates { get; } =
        new StableDomainIdentifierMap<BranchLevelState>(new Dictionary<BranchLevelState, string>
        {
            [BranchLevelState.Unopened] = "Unopened",
            [BranchLevelState.Training] = "Training",
            [BranchLevelState.TestReady] = "TestReady",
            [BranchLevelState.PassedOnce] = "PassedOnce",
            [BranchLevelState.Stabilizing] = "Stabilizing",
            [BranchLevelState.Owned] = "Owned",
            [BranchLevelState.Maintenance] = "Maintenance",
            [BranchLevelState.Decayed] = "Decayed",
        });

    public static IStableDomainIdentifierMap<EvidenceArtifactCategory> EvidenceArtifactCategories { get; } =
        new StableDomainIdentifierMap<EvidenceArtifactCategory>(new Dictionary<EvidenceArtifactCategory, string>
        {
            [EvidenceArtifactCategory.Practice] = "Practice",
            [EvidenceArtifactCategory.Load] = "Load",
            [EvidenceArtifactCategory.Test] = "Test",
            [EvidenceArtifactCategory.Stabilization] = "Stabilization",
            [EvidenceArtifactCategory.Transfer] = "Transfer",
            [EvidenceArtifactCategory.Maintenance] = "Maintenance",
            [EvidenceArtifactCategory.GlobalReview] = "GlobalReview",
        });

    public static IStableDomainIdentifierMap<ObservableEvidenceKind> ObservableEvidenceKinds { get; } =
        new StableDomainIdentifierMap<ObservableEvidenceKind>(new Dictionary<ObservableEvidenceKind, string>
        {
            [ObservableEvidenceKind.Score] = "Score",
            [ObservableEvidenceKind.Time] = "Time",
            [ObservableEvidenceKind.ErrorCount] = "ErrorCount",
            [ObservableEvidenceKind.Reconstruction] = "Reconstruction",
            [ObservableEvidenceKind.Comparison] = "Comparison",
            [ObservableEvidenceKind.RuleExplanation] = "RuleExplanation",
            [ObservableEvidenceKind.FailedItemList] = "FailedItemList",
            [ObservableEvidenceKind.RepeatabilityRecord] = "RepeatabilityRecord",
            [ObservableEvidenceKind.OutputSample] = "OutputSample",
            [ObservableEvidenceKind.BranchMapping] = "BranchMapping",
            [ObservableEvidenceKind.CriticalConstraintRecord] = "CriticalConstraintRecord",
            [ObservableEvidenceKind.LoadVariableRecord] = "LoadVariableRecord",
            [ObservableEvidenceKind.BottleneckNote] = "BottleneckNote",
            [ObservableEvidenceKind.AuditResult] = "AuditResult",
            [ObservableEvidenceKind.DelayedReconstruction] = "DelayedReconstruction",
            [ObservableEvidenceKind.MaintenanceCheck] = "MaintenanceCheck",
            [ObservableEvidenceKind.GlobalReviewSummary] = "GlobalReviewSummary",
        });

    public static IStableDomainIdentifierMap<TestResultEvidenceKind> TestResultEvidenceKinds { get; } =
        new StableDomainIdentifierMap<TestResultEvidenceKind>(new Dictionary<TestResultEvidenceKind, string>
        {
            [TestResultEvidenceKind.Score] = "Score",
            [TestResultEvidenceKind.Rubric] = "Rubric",
            [TestResultEvidenceKind.PassFail] = "PassFail",
        });

    public static IStableDomainIdentifierMap<FormalTestPassState> FormalTestPassStates { get; } =
        new StableDomainIdentifierMap<FormalTestPassState>(new Dictionary<FormalTestPassState, string>
        {
            [FormalTestPassState.Fail] = "Fail",
            [FormalTestPassState.PassOnce] = "PassOnce",
            [FormalTestPassState.StabilizationPass] = "StabilizationPass",
            [FormalTestPassState.Owned] = "Owned",
            [FormalTestPassState.MaintenancePass] = "MaintenancePass",
        });

    public static IStableDomainIdentifierMap<PromptContentKind> PromptContentKinds { get; } =
        new StableDomainIdentifierMap<PromptContentKind>(new Dictionary<PromptContentKind, string>
        {
            [PromptContentKind.EquivalentPrompt] = "EquivalentPrompt",
            [PromptContentKind.CueSequence] = "CueSequence",
            [PromptContentKind.DelayedReconstructionTask] = "DelayedReconstructionTask",
            [PromptContentKind.DiscriminationItemSet] = "DiscriminationItemSet",
            [PromptContentKind.RuleExampleSet] = "RuleExampleSet",
        });
}

internal sealed class StableDomainIdentifierMap<TDomain> : IStableDomainIdentifierMap<TDomain>
    where TDomain : struct, Enum
{
    private readonly IReadOnlyDictionary<TDomain, string> persistedIds;
    private readonly IReadOnlyDictionary<string, TDomain> domainValues;

    public StableDomainIdentifierMap(IReadOnlyDictionary<TDomain, string> persistedIds)
    {
        ArgumentNullException.ThrowIfNull(persistedIds);

        var enumValues = Enum.GetValues<TDomain>();
        var expectedValues = enumValues.ToHashSet();
        var copiedPersistedIds = new Dictionary<TDomain, string>();
        var copiedDomainValues = new Dictionary<string, TDomain>(StringComparer.Ordinal);

        foreach (var value in enumValues)
        {
            if (!persistedIds.TryGetValue(value, out var persistedId))
            {
                throw new ArgumentException(
                    $"Stable persisted identifier mapping for {typeof(TDomain).Name} is missing {value}.",
                    nameof(persistedIds));
            }

            if (string.IsNullOrWhiteSpace(persistedId))
            {
                throw new ArgumentException(
                    $"Stable persisted identifier mapping for {typeof(TDomain).Name}.{value} cannot be blank.",
                    nameof(persistedIds));
            }

            if (!copiedDomainValues.TryAdd(persistedId, value))
            {
                throw new ArgumentException(
                    $"Stable persisted identifier mapping for {typeof(TDomain).Name} contains duplicate id '{persistedId}'.",
                    nameof(persistedIds));
            }

            copiedPersistedIds.Add(value, persistedId);
        }

        foreach (var value in persistedIds.Keys)
        {
            if (!expectedValues.Contains(value))
            {
                throw new ArgumentException(
                    $"Stable persisted identifier mapping for {typeof(TDomain).Name} contains undefined value {value}.",
                    nameof(persistedIds));
            }
        }

        this.persistedIds = copiedPersistedIds;
        domainValues = copiedDomainValues;
    }

    public IReadOnlyDictionary<TDomain, string> PersistedIds => persistedIds;

    public string ToPersistedId(TDomain value)
    {
        if (persistedIds.TryGetValue(value, out var persistedId))
        {
            return persistedId;
        }

        throw new ArgumentException(
            $"No stable persisted identifier is defined for {typeof(TDomain).Name}.{value}.",
            nameof(value));
    }

    public bool TryFromPersistedId(string persistedId, out TDomain value)
    {
        if (string.IsNullOrWhiteSpace(persistedId))
        {
            value = default;
            return false;
        }

        return domainValues.TryGetValue(persistedId, out value);
    }

    public TDomain FromPersistedId(string persistedId)
    {
        if (TryFromPersistedId(persistedId, out var value))
        {
            return value;
        }

        throw new ArgumentException(
            $"'{persistedId}' is not a stable persisted identifier for {typeof(TDomain).Name}.",
            nameof(persistedId));
    }
}
