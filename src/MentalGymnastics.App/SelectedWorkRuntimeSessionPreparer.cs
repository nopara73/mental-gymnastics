using MentalGymnastics.Content;
using MentalGymnastics.Core;
using MentalGymnastics.Runtime;

namespace MentalGymnastics.App;

public enum SelectedWorkRuntimeSessionPreparationStatus
{
    Prepared,
    Rejected,
}

public enum SelectedWorkRuntimeSessionRejectionKind
{
    GeneratedContentNotPrepared,
    RuntimeDefinitionRejected,
    PhasePlanRejected,
    CueScheduleRejected,
}

public sealed record SelectedWorkRuntimeSessionRejection(
    SelectedWorkRuntimeSessionRejectionKind Kind,
    string Detail);

public sealed class SelectedWorkRuntimeSessionPreparationRequest
{
    public SelectedWorkRuntimeSessionPreparationRequest(
        string sessionId,
        SelectedWorkGeneratedContentPreparationResult generatedContent,
        RuntimeInputCommandOptions? inputOptions = null)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ArgumentException("Runtime session preparation requires a session id.", nameof(sessionId));
        }

        ArgumentNullException.ThrowIfNull(generatedContent);

        SessionId = sessionId;
        GeneratedContent = generatedContent;
        InputOptions = inputOptions ?? RuntimeInputCommandOptions.Default;
    }

    public string SessionId { get; }

    public SelectedWorkGeneratedContentPreparationResult GeneratedContent { get; }

    public RuntimeInputCommandOptions InputOptions { get; }
}

public sealed class SelectedWorkRuntimeSessionPreparationResult
{
    private SelectedWorkRuntimeSessionPreparationResult(
        SelectedWorkRuntimeSessionPreparationStatus status,
        string sessionId,
        SelectedTrainingWork selectedWork,
        SelectedWorkGeneratedContentPreparationResult generatedContent,
        RuntimeSessionDefinition? sessionDefinition,
        RuntimeSessionPhasePlan? phasePlan,
        RuntimeCueSchedule? cueSchedule,
        RuntimeInputCommandOptions inputOptions,
        IEnumerable<GeneratedContentMaterial> inputMaterials,
        IEnumerable<GeneratedContentPayloadFact> expectedEvidenceFacts,
        IEnumerable<SelectedWorkRuntimeSessionRejection> rejections)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ArgumentException("Prepared runtime sessions require a session id.", nameof(sessionId));
        }

        ArgumentNullException.ThrowIfNull(selectedWork);
        ArgumentNullException.ThrowIfNull(generatedContent);
        ArgumentNullException.ThrowIfNull(inputOptions);
        ArgumentNullException.ThrowIfNull(inputMaterials);
        ArgumentNullException.ThrowIfNull(expectedEvidenceFacts);
        ArgumentNullException.ThrowIfNull(rejections);

        var materialArray = inputMaterials.ToArray();
        var evidenceFactArray = expectedEvidenceFacts.ToArray();
        var rejectionArray = rejections.ToArray();
        if (rejectionArray.Any(rejection => string.IsNullOrWhiteSpace(rejection.Detail)))
        {
            throw new ArgumentException(
                "Runtime session preparation rejections must include details.",
                nameof(rejections));
        }

        Status = status;
        SessionId = sessionId;
        SelectedWork = selectedWork;
        GeneratedContent = generatedContent;
        SessionDefinition = sessionDefinition;
        PhasePlan = phasePlan;
        CueSchedule = cueSchedule;
        InputOptions = inputOptions;
        InputMaterials = Array.AsReadOnly(materialArray);
        ExpectedEvidenceFacts = Array.AsReadOnly(evidenceFactArray);
        Rejections = Array.AsReadOnly(rejectionArray);
    }

    public SelectedWorkRuntimeSessionPreparationStatus Status { get; }

    public bool IsPrepared => Status == SelectedWorkRuntimeSessionPreparationStatus.Prepared;

    public string SessionId { get; }

    public SelectedTrainingWork SelectedWork { get; }

    public SelectedWorkGeneratedContentPreparationResult GeneratedContent { get; }

    public RuntimeSessionDefinition? SessionDefinition { get; }

    public RuntimeSessionPhasePlan? PhasePlan { get; }

    public RuntimeCueSchedule? CueSchedule { get; }

    public RuntimeInputCommandOptions InputOptions { get; }

    public IReadOnlyList<GeneratedContentMaterial> InputMaterials { get; }

    public IReadOnlyList<GeneratedContentPayloadFact> ExpectedEvidenceFacts { get; }

    public IReadOnlyList<SelectedWorkRuntimeSessionRejection> Rejections { get; }

    public bool CanRenderLiveSession => IsPrepared &&
        SessionDefinition is not null &&
        PhasePlan is not null &&
        InputMaterials.Count > 0;

    public bool OwnsTiming => false;

    public bool OwnsCueScheduling => false;

    public bool OwnsScoring => false;

    public bool GrantsAdvancement => false;

    internal static SelectedWorkRuntimeSessionPreparationResult Prepared(
        string sessionId,
        SelectedWorkGeneratedContentPreparationResult generatedContent,
        RuntimeSessionDefinition sessionDefinition,
        RuntimeSessionPhasePlan phasePlan,
        RuntimeCueSchedule? cueSchedule,
        RuntimeInputCommandOptions inputOptions,
        IEnumerable<GeneratedContentMaterial> inputMaterials,
        IEnumerable<GeneratedContentPayloadFact> expectedEvidenceFacts)
    {
        return new SelectedWorkRuntimeSessionPreparationResult(
            SelectedWorkRuntimeSessionPreparationStatus.Prepared,
            sessionId,
            generatedContent.SelectedWork,
            generatedContent,
            sessionDefinition,
            phasePlan,
            cueSchedule,
            inputOptions,
            inputMaterials,
            expectedEvidenceFacts,
            []);
    }

    internal static SelectedWorkRuntimeSessionPreparationResult Rejected(
        string sessionId,
        SelectedWorkGeneratedContentPreparationResult generatedContent,
        SelectedWorkRuntimeSessionRejection rejection)
    {
        return new SelectedWorkRuntimeSessionPreparationResult(
            SelectedWorkRuntimeSessionPreparationStatus.Rejected,
            sessionId,
            generatedContent.SelectedWork,
            generatedContent,
            sessionDefinition: null,
            phasePlan: null,
            cueSchedule: null,
            RuntimeInputCommandOptions.Default,
            inputMaterials: [],
            expectedEvidenceFacts: [],
            [rejection]);
    }
}

public sealed class SelectedWorkRuntimeSessionPreparer
{
    public SelectedWorkRuntimeSessionPreparationResult Prepare(
        SelectedWorkRuntimeSessionPreparationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var generatedContent = request.GeneratedContent;
        if (!generatedContent.IsPrepared || generatedContent.RuntimePackage is null)
        {
            return Reject(
                request,
                SelectedWorkRuntimeSessionRejectionKind.GeneratedContentNotPrepared,
                "Generated content must be prepared before a runtime session can be prepared.");
        }

        var package = generatedContent.RuntimePackage;
        var runtimeIdentity = new RuntimeGeneratedDrillInstanceIdentity(
            package.GeneratedInstance.InstanceId,
            package.GeneratedInstance.ContentIdentity,
            package.GeneratedInstance.ContentVersion);

        RuntimeSessionDefinition definition;
        try
        {
            definition = new RuntimeSessionDefinition(
                package.SessionType,
                package.Branch,
                package.Level,
                package.Drill,
                package.LoadVariables,
                package.Standard,
                package.CriticalConstraints,
                runtimeIdentity,
                package.SourceDrill);
        }
        catch (ArgumentException exception)
        {
            return Reject(
                request,
                SelectedWorkRuntimeSessionRejectionKind.RuntimeDefinitionRejected,
                exception.Message);
        }

        RuntimeSessionPhasePlan phasePlan;
        try
        {
            phasePlan = new RuntimeSessionPhasePlan(package.Phases.Select(ToRuntimePhase));
        }
        catch (ArgumentException exception)
        {
            return Reject(
                request,
                SelectedWorkRuntimeSessionRejectionKind.PhasePlanRejected,
                exception.Message);
        }

        RuntimeCueSchedule? cueSchedule;
        try
        {
            cueSchedule = CreateCueSchedule(package);
        }
        catch (ArgumentException exception)
        {
            return Reject(
                request,
                SelectedWorkRuntimeSessionRejectionKind.CueScheduleRejected,
                exception.Message);
        }

        return SelectedWorkRuntimeSessionPreparationResult.Prepared(
            request.SessionId,
            generatedContent,
            definition,
            phasePlan,
            cueSchedule,
            request.InputOptions,
            package.InputMaterials,
            package.ExpectedEvidenceFacts);
    }

    private static RuntimeSessionPhaseDefinition ToRuntimePhase(
        GeneratedRuntimePhaseDefinition phase)
    {
        ArgumentNullException.ThrowIfNull(phase);

        var kind = Enum.Parse<RuntimeSessionPhaseKind>(phase.Kind.ToString());
        var duration = phase.ScheduledDuration.HasValue
            ? new RuntimeDuration(phase.ScheduledDuration.Value)
            : (RuntimeDuration?)null;

        return phase.CompletionRule switch
        {
            GeneratedRuntimePhaseCompletionRule.Manual => RuntimeSessionPhaseDefinition.Manual(phase.Id, kind),
            GeneratedRuntimePhaseCompletionRule.Timed => RuntimeSessionPhaseDefinition.Timed(phase.Id, kind, duration!.Value),
            GeneratedRuntimePhaseCompletionRule.ManualOrTimed => RuntimeSessionPhaseDefinition.ManualOrTimed(phase.Id, kind, duration),
            _ => throw new ArgumentOutOfRangeException(nameof(phase), phase.CompletionRule, "Unknown generated phase completion rule."),
        };
    }

    private static RuntimeScheduledCue ToRuntimeCue(GeneratedRuntimeCueDefinition cue)
    {
        ArgumentNullException.ThrowIfNull(cue);

        var kind = Enum.Parse<RuntimeCueKind>(cue.Kind.ToString());
        var expectation = cue.ResponseExpectation == GeneratedRuntimeCueResponseExpectation.ResponseRequired
            ? RuntimeCueResponseExpectation.ResponseRequired
            : RuntimeCueResponseExpectation.NoResponseExpected;

        return new RuntimeScheduledCue(
            cue.Id,
            kind,
            cue.Value,
            new RuntimeInstant(cue.ScheduledAtOffset),
            new RuntimeDuration(cue.ResponseWindow),
            expectation,
            cue.ExpectedResponse);
    }

    internal static RuntimeCueSchedule? CreateCueSchedule(GeneratedContentRuntimePackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        if (package.Cues.Count == 0)
        {
            return null;
        }

        var identity = new RuntimeGeneratedDrillInstanceIdentity(
            package.GeneratedInstance.InstanceId,
            package.GeneratedInstance.ContentIdentity,
            package.GeneratedInstance.ContentVersion);
        return new RuntimeCueSchedule(identity, package.Cues.Select(ToRuntimeCue));
    }

    private static SelectedWorkRuntimeSessionPreparationResult Reject(
        SelectedWorkRuntimeSessionPreparationRequest request,
        SelectedWorkRuntimeSessionRejectionKind kind,
        string detail)
    {
        return SelectedWorkRuntimeSessionPreparationResult.Rejected(
            request.SessionId,
            request.GeneratedContent,
            new SelectedWorkRuntimeSessionRejection(kind, detail));
    }
}
