using MentalGymnastics.Content;
using MentalGymnastics.Core;

namespace MentalGymnastics.App;

public enum SelectedWorkGeneratedContentPreparationStatus
{
    Prepared,
    Rejected,
}

public enum SelectedWorkGeneratedContentRejectionKind
{
    InvalidSelectedWork,
    ContentSelectionRejected,
    RuntimePackagingRejected,
    PersistenceHandoffRejected,
}

public sealed record SelectedWorkGeneratedContentRejection(
    SelectedWorkGeneratedContentRejectionKind Kind,
    string Detail);

public sealed class SelectedWorkGeneratedContentPreparationRequest
{
    public SelectedWorkGeneratedContentPreparationRequest(
        SelectedTrainingWork selectedWork,
        PromptContentKind contentKind,
        string equivalenceClass,
        PromptFreshnessPolicy freshnessPolicy,
        GeneratedContentSeed seed,
        TrainingDate generatedOn,
        IEnumerable<string>? previouslyUsedContentIds = null,
        IEnumerable<CriticalConstraint>? additionalCriticalConstraints = null,
        LoadChangeMode loadChangeMode = LoadChangeMode.Acquisition,
        bool increasedVariablesStableSeparately = false,
        CapacityId? transferCapacity = null,
        string? transferDistance = null)
    {
        ArgumentNullException.ThrowIfNull(selectedWork);
        EnsureDefined(contentKind, nameof(contentKind));
        EnsureDefined(freshnessPolicy, nameof(freshnessPolicy));
        ArgumentNullException.ThrowIfNull(seed);
        EnsureDefined(loadChangeMode, nameof(loadChangeMode));

        if (string.IsNullOrWhiteSpace(equivalenceClass))
        {
            throw new ArgumentException(
                "Selected work content preparation requires an equivalence class.",
                nameof(equivalenceClass));
        }

        if (generatedOn.Year <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(generatedOn),
                generatedOn,
                "Generated content preparation requires a valid generated date.");
        }

        if (transferCapacity.HasValue)
        {
            EnsureDefined(transferCapacity.Value, nameof(transferCapacity));
        }

        SelectedWork = selectedWork;
        ContentKind = contentKind;
        EquivalenceClass = equivalenceClass;
        FreshnessPolicy = freshnessPolicy;
        Seed = seed;
        GeneratedOn = generatedOn;
        PreviouslyUsedContentIds = (previouslyUsedContentIds ?? [])
            .Where(contentId => !string.IsNullOrWhiteSpace(contentId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        AdditionalCriticalConstraints = (additionalCriticalConstraints ?? [])
            .Where(constraint => !string.IsNullOrWhiteSpace(constraint.Description))
            .ToArray();
        LoadChangeMode = loadChangeMode;
        IncreasedVariablesStableSeparately = increasedVariablesStableSeparately;
        TransferCapacity = transferCapacity;
        TransferDistance = transferDistance;
    }

    public SelectedTrainingWork SelectedWork { get; }

    public PromptContentKind ContentKind { get; }

    public string EquivalenceClass { get; }

    public PromptFreshnessPolicy FreshnessPolicy { get; }

    public GeneratedContentSeed Seed { get; }

    public TrainingDate GeneratedOn { get; }

    public IReadOnlyList<string> PreviouslyUsedContentIds { get; }

    public IReadOnlyList<CriticalConstraint> AdditionalCriticalConstraints { get; }

    public LoadChangeMode LoadChangeMode { get; }

    public bool IncreasedVariablesStableSeparately { get; }

    public CapacityId? TransferCapacity { get; }

    public string? TransferDistance { get; }

    private static void EnsureDefined<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Unknown program identifier.");
        }
    }
}

public sealed class SelectedWorkGeneratedContentPreparationResult
{
    private SelectedWorkGeneratedContentPreparationResult(
        SelectedWorkGeneratedContentPreparationStatus status,
        SelectedTrainingWork selectedWork,
        SessionType contentSessionType,
        GeneratedContentSelectionResult? generatedContent,
        GeneratedContentRuntimePackage? runtimePackage,
        GeneratedContentPersistenceHandoff? persistenceHandoff,
        IEnumerable<SelectedWorkGeneratedContentRejection> rejections)
    {
        ArgumentNullException.ThrowIfNull(selectedWork);
        ArgumentNullException.ThrowIfNull(rejections);

        var rejectionArray = rejections.ToArray();
        if (rejectionArray.Any(rejection => string.IsNullOrWhiteSpace(rejection.Detail)))
        {
            throw new ArgumentException(
                "Selected work generated content rejections must include details.",
                nameof(rejections));
        }

        Status = status;
        SelectedWork = selectedWork;
        AppSessionType = selectedWork.SessionType;
        ContentSessionType = contentSessionType;
        GeneratedContent = generatedContent;
        RuntimePackage = runtimePackage;
        PersistenceHandoff = persistenceHandoff;
        Rejections = Array.AsReadOnly(rejectionArray);
    }

    public SelectedWorkGeneratedContentPreparationStatus Status { get; }

    public bool IsPrepared => Status == SelectedWorkGeneratedContentPreparationStatus.Prepared;

    public SelectedTrainingWork SelectedWork { get; }

    public AppTrainingSessionType AppSessionType { get; }

    public SessionType ContentSessionType { get; }

    public GeneratedContentSelectionResult? GeneratedContent { get; }

    public GeneratedContentRuntimePackage? RuntimePackage { get; }

    public GeneratedContentPersistenceHandoff? PersistenceHandoff { get; }

    public IReadOnlyList<SelectedWorkGeneratedContentRejection> Rejections { get; }

    internal static SelectedWorkGeneratedContentPreparationResult Prepared(
        SelectedTrainingWork selectedWork,
        SessionType contentSessionType,
        GeneratedContentSelectionResult generatedContent,
        GeneratedContentRuntimePackage runtimePackage,
        GeneratedContentPersistenceHandoff persistenceHandoff)
    {
        return new SelectedWorkGeneratedContentPreparationResult(
            SelectedWorkGeneratedContentPreparationStatus.Prepared,
            selectedWork,
            contentSessionType,
            generatedContent,
            runtimePackage,
            persistenceHandoff,
            []);
    }

    internal static SelectedWorkGeneratedContentPreparationResult Rejected(
        SelectedTrainingWork selectedWork,
        SessionType contentSessionType,
        SelectedWorkGeneratedContentRejection rejection)
    {
        return new SelectedWorkGeneratedContentPreparationResult(
            SelectedWorkGeneratedContentPreparationStatus.Rejected,
            selectedWork,
            contentSessionType,
            generatedContent: null,
            runtimePackage: null,
            persistenceHandoff: null,
            [rejection]);
    }
}

public sealed class SelectedWorkGeneratedContentPreparer
{
    private readonly IReadOnlyList<LocalContentBank> localContentBanks;

    public SelectedWorkGeneratedContentPreparer(
        IEnumerable<LocalContentBank>? localContentBanks = null)
    {
        var bankArray = (localContentBanks ?? [MvpLocalContentBank.Create()]).ToArray();
        if (bankArray.Any(bank => bank is null))
        {
            throw new ArgumentException(
                "Selected work generated content preparation cannot use null local content banks.",
                nameof(localContentBanks));
        }

        this.localContentBanks = Array.AsReadOnly(bankArray);
    }

    public SelectedWorkGeneratedContentPreparationResult Prepare(
        SelectedWorkGeneratedContentPreparationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var selectedWork = request.SelectedWork;
        var contentSessionType = ToContentSessionType(selectedWork.SessionType);
        var standard = StandardFor(selectedWork.Branch, selectedWork.Level);
        var drill = DrillFor(selectedWork.Drill);
        if (!string.Equals(selectedWork.Demand, standard.Demand, StringComparison.Ordinal) ||
            !string.Equals(selectedWork.Standard, standard.Standard, StringComparison.Ordinal) ||
            !string.Equals(selectedWork.HonestyConstraint, drill.HonestyConstraint, StringComparison.Ordinal))
        {
            return Reject(
                selectedWork,
                contentSessionType,
                SelectedWorkGeneratedContentRejectionKind.InvalidSelectedWork,
                "Selected work demand, standard, and honesty constraint must match core catalog definitions before content can be prepared.");
        }

        GeneratedContentSelectionResult selection;
        try
        {
            var contentRequest = BuildContentRequest(request, contentSessionType);
            var need = BuildSelectionNeed(request, contentRequest, contentSessionType);
            selection = GeneratedContentSelectionCoordinator.Select(need, request.Seed, localContentBanks);

            if (!selection.IsValid)
            {
                return Reject(
                    selectedWork,
                    contentSessionType,
                    SelectedWorkGeneratedContentRejectionKind.ContentSelectionRejected,
                    FailureSummary(selection));
            }
        }
        catch (ArgumentException exception)
        {
            return Reject(
                selectedWork,
                contentSessionType,
                SelectedWorkGeneratedContentRejectionKind.ContentSelectionRejected,
                exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return Reject(
                selectedWork,
                contentSessionType,
                SelectedWorkGeneratedContentRejectionKind.ContentSelectionRejected,
                exception.Message);
        }

        GeneratedContentRuntimePackage runtimePackage;
        var executionMaterials = StabilizationGeneratedContent.AddControlledDistractor(
            selection.Result,
            selection.Materials);
        try
        {
            runtimePackage = GeneratedContentRuntimePackager.Package(
                selection.Result,
                executionMaterials,
                standard);
        }
        catch (ArgumentException exception)
        {
            return Reject(
                selectedWork,
                contentSessionType,
                SelectedWorkGeneratedContentRejectionKind.RuntimePackagingRejected,
                exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return Reject(
                selectedWork,
                contentSessionType,
                SelectedWorkGeneratedContentRejectionKind.RuntimePackagingRejected,
                exception.Message);
        }

        try
        {
            var persistenceHandoff = GeneratedContentPersistenceHandoffMapper.Create(
                selection.Result,
                executionMaterials,
                request.GeneratedOn);

            return SelectedWorkGeneratedContentPreparationResult.Prepared(
                selectedWork,
                contentSessionType,
                selection,
                runtimePackage,
                persistenceHandoff);
        }
        catch (ArgumentException exception)
        {
            return Reject(
                selectedWork,
                contentSessionType,
                SelectedWorkGeneratedContentRejectionKind.PersistenceHandoffRejected,
                exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return Reject(
                selectedWork,
                contentSessionType,
                SelectedWorkGeneratedContentRejectionKind.PersistenceHandoffRejected,
                exception.Message);
        }
    }

    private static GeneratedDrillContentRequest BuildContentRequest(
        SelectedWorkGeneratedContentPreparationRequest request,
        SessionType contentSessionType)
    {
        return new GeneratedDrillContentRequest(
            request.SelectedWork.Branch,
            request.SelectedWork.Level,
            request.SelectedWork.Drill,
            contentSessionType,
            request.ContentKind,
            request.EquivalenceClass,
            request.FreshnessPolicy,
            request.SelectedWork.LoadVariables,
            CriticalConstraintsFor(request),
            request.PreviouslyUsedContentIds);
    }

    private static GeneratedContentSelectionNeed BuildSelectionNeed(
        SelectedWorkGeneratedContentPreparationRequest request,
        GeneratedDrillContentRequest contentRequest,
        SessionType contentSessionType)
    {
        if (contentSessionType != SessionType.Transfer)
        {
            return GeneratedContentSelectionNeed.ForStandardContent(
                contentRequest,
                request.LoadChangeMode,
                request.IncreasedVariablesStableSeparately);
        }

        if (!request.TransferCapacity.HasValue ||
            string.IsNullOrWhiteSpace(request.TransferDistance))
        {
            throw new ArgumentException(
                "Transfer selected work requires transfer capacity and transfer distance before content preparation.");
        }

        return GeneratedContentSelectionNeed.ForTransferContent(
            new TransferContentGenerationRequest(
                contentRequest,
                request.TransferCapacity.Value,
                request.TransferDistance));
    }

    private static IReadOnlyList<CriticalConstraint> CriticalConstraintsFor(
        SelectedWorkGeneratedContentPreparationRequest request)
    {
        return ProtocolCriticalConstraintsFor(request.SelectedWork.Drill)
            .Concat(request.AdditionalCriticalConstraints)
            .Where(constraint => !string.IsNullOrWhiteSpace(constraint.Description))
            .Distinct()
            .ToArray();
    }

    private static IEnumerable<CriticalConstraint> ProtocolCriticalConstraintsFor(
        DrillId drill)
    {
        return DrillProtocolCatalog.StandardDrills
            .Single(protocol => protocol.Id == drill)
            .HonestyConstraints
            .Select(constraint => new CriticalConstraint(constraint.Description));
    }

    private static SessionType ToContentSessionType(AppTrainingSessionType sessionType)
    {
        return sessionType switch
        {
            AppTrainingSessionType.Practice => SessionType.Practice,
            AppTrainingSessionType.Load => SessionType.Load,
            AppTrainingSessionType.Test => SessionType.Test,
            AppTrainingSessionType.Stabilization => SessionType.Stabilization,
            AppTrainingSessionType.Regression => SessionType.Regression,
            AppTrainingSessionType.Transfer => SessionType.Transfer,
            AppTrainingSessionType.Recovery => SessionType.Recovery,
            AppTrainingSessionType.Maintenance => SessionType.Practice,
            _ => throw new ArgumentOutOfRangeException(nameof(sessionType), sessionType, "Unknown app session type."),
        };
    }

    private static BranchLevelStandard StandardFor(BranchCode branch, GlobalLevelId level)
    {
        return ProgramCatalog.Standards.Single(standard =>
            standard.Branch == branch &&
            standard.Level == level);
    }

    private static DrillDefinition DrillFor(DrillId drill)
    {
        return ProgramCatalog.Drills.Single(definition => definition.Id == drill);
    }

    private static SelectedWorkGeneratedContentPreparationResult Reject(
        SelectedTrainingWork selectedWork,
        SessionType contentSessionType,
        SelectedWorkGeneratedContentRejectionKind kind,
        string detail)
    {
        return SelectedWorkGeneratedContentPreparationResult.Rejected(
            selectedWork,
            contentSessionType,
            new SelectedWorkGeneratedContentRejection(kind, detail));
    }

    private static string FailureSummary(GeneratedContentSelectionResult selection)
    {
        var details = selection.FreshnessValidation.Failures.Select(failure => failure.Detail)
            .Concat(selection.AntiSelfDeceptionGuard.Findings.Select(finding => finding.Detail))
            .Concat(selection.DifficultyAudit?.Failures.Select(failure => failure.Detail) ?? [])
            .Concat(selection.TransferValidation?.Failures.Select(failure => failure.Detail) ?? []);

        var summary = string.Join("; ", details);
        return string.IsNullOrWhiteSpace(summary)
            ? "Generated content selection was not valid for the selected work."
            : summary;
    }
}
