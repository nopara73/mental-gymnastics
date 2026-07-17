using System.Text.Json;
using System.Text.Json.Nodes;
using MentalGymnastics.Core;

namespace MentalGymnastics.Persistence;

public sealed class LocalProgrammingEventTransaction
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly LocalDatabaseOptions options;
    private readonly LocalDatabaseInitializer initializer;

    public LocalProgrammingEventTransaction(LocalDatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        this.options = options;
        initializer = new LocalDatabaseInitializer(options);
    }

    public async ValueTask CommitAsync(
        Func<LocalProgrammingEventTransactionContext, ValueTask> update,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);
        cancellationToken.ThrowIfCancellationRequested();

        var document = await ReadInitializedDocumentAsync(cancellationToken).ConfigureAwait(false);
        var context = new LocalProgrammingEventTransactionContext(document);

        await update(context).ConfigureAwait(false);

        LocalPersistenceIntegrityValidator.ThrowIfInvalid(document, "committing a local programming event");

        await ReplaceDatabaseAsync(document, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<JsonObject> ReadInitializedDocumentAsync(CancellationToken cancellationToken)
    {
        await initializer.InitializeAsync(cancellationToken).ConfigureAwait(false);

        await using var stream = new FileStream(
            options.DatabasePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);

        var document = await LocalJsonDocumentIO.ReadObjectAsync(stream, cancellationToken)
            .ConfigureAwait(false);

        if (document is null ||
            !document.TryGetPropertyValue("Kind", out var kindNode) ||
            kindNode?.GetValue<string>() != LocalDatabaseSchema.MetadataKind)
        {
            throw new InvalidOperationException("The local database metadata is missing or invalid.");
        }

        LocalDatabaseDocument.ReadSchemaVersion(document);
        return document;
    }

    private async ValueTask ReplaceDatabaseAsync(JsonObject document, CancellationToken cancellationToken)
    {
        var tempPath = $"{options.DatabasePath}.{Guid.NewGuid():N}.tmp";

        try
        {
            await WriteDocumentAsync(tempPath, document, cancellationToken).ConfigureAwait(false);
            File.Move(tempPath, options.DatabasePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static async ValueTask WriteDocumentAsync(
        string path,
        JsonObject document,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            useAsync: true);

        await LocalJsonDocumentIO.WriteObjectAsync(stream, document, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}

public sealed class LocalProgrammingEventTransactionContext
{
    private const string PractitionerStatePropertyName = "PractitionerState";
    private const string PractitionerBranchLevelsPropertyName = "BranchLevels";
    private const string EvidenceArtifactsPropertyName = "EvidenceArtifacts";
    private const string FormalTestAttemptsPropertyName = "FormalTestAttempts";
    private const string CompletedSessionsPropertyName = "CompletedSessions";
    private const string StabilizationPassesPropertyName = "StabilizationPasses";
    private const string MaintenanceChecksPropertyName = "MaintenanceChecks";
    private const string RestorationChecksPropertyName = "RestorationChecks";
    private const string GeneratedDrillInstancesPropertyName = "GeneratedDrillInstances";
    private const string ActiveRuntimeSessionSnapshotsPropertyName = "ActiveRuntimeSessionSnapshots";
    private const string ArtifactIdPropertyName = "ArtifactId";
    private const string EventPropertyName = "Event";
    private const string EventIdPropertyName = "EventId";
    private const string EventKindPropertyName = "EventKind";
    private const string ArtifactPropertyName = "Artifact";
    private const string AttemptIdPropertyName = "AttemptId";
    private const string PassIdPropertyName = "PassId";
    private const string CheckIdPropertyName = "CheckId";
    private const string InstanceIdPropertyName = "InstanceId";
    private const string EvidenceArtifactIdPropertyName = "EvidenceArtifactId";
    private const string AttemptPropertyName = "Attempt";
    private const string SessionIdPropertyName = "SessionId";
    private const string DatePropertyName = "Date";
    private const string YearPropertyName = "Year";
    private const string MonthPropertyName = "Month";
    private const string DayPropertyName = "Day";
    private const string BranchPropertyName = "Branch";
    private const string LevelPropertyName = "Level";
    private const string StatePropertyName = "State";
    private const string DrillPropertyName = "Drill";
    private const string CategoryPropertyName = "Category";
    private const string ObservableEvidencePropertyName = "ObservableEvidence";
    private const string KindPropertyName = "Kind";
    private const string DescriptionPropertyName = "Description";
    private const string SummaryOrReferencePropertyName = "SummaryOrReference";
    private const string SubjectiveNotePropertyName = "SubjectiveNote";
    private const string TaskPropertyName = "Task";
    private const string TransferTaskPropertyName = "TransferTask";
    private const string LoadVariablesPropertyName = "LoadVariables";
    private const string NamePropertyName = "Name";
    private const string ValuePropertyName = "Value";
    private const string StandardPropertyName = "Standard";
    private const string CriticalConstraintsPropertyName = "CriticalConstraints";
    private const string ResultEvidencePropertyName = "ResultEvidence";
    private const string ResultKindPropertyName = "ResultKind";
    private const string ResultValuePropertyName = "ResultValue";
    private const string FailureTypePropertyName = "FailureType";
    private const string PassStatePropertyName = "PassState";
    private const string SessionTypePropertyName = "SessionType";
    private const string SessionBranchLevelsPropertyName = "BranchLevels";
    private const string IntensityPropertyName = "Intensity";
    private const string CleanPerformancePropertyName = "CleanPerformance";
    private const string NotesPropertyName = "Notes";
    private const string RecoveryMarkedPropertyName = "RecoveryMarked";
    private const string DeloadMarkedPropertyName = "DeloadMarked";
    private const string EvidenceArtifactIdsPropertyName = "EvidenceArtifactIds";

    private static readonly IStableDomainIdentifierMap<LocalCompletedSessionType> CompletedSessionTypes =
        new StableDomainIdentifierMap<LocalCompletedSessionType>(new Dictionary<LocalCompletedSessionType, string>
        {
            [LocalCompletedSessionType.Practice] = "Practice",
            [LocalCompletedSessionType.Load] = "Load",
            [LocalCompletedSessionType.Test] = "Test",
            [LocalCompletedSessionType.Stabilization] = "Stabilization",
            [LocalCompletedSessionType.Regression] = "Regression",
            [LocalCompletedSessionType.Transfer] = "Transfer",
            [LocalCompletedSessionType.Recovery] = "Recovery",
            [LocalCompletedSessionType.Maintenance] = "Maintenance",
            [LocalCompletedSessionType.Review] = "Review",
        });

    private static readonly IStableDomainIdentifierMap<LocalSessionIntensity> SessionIntensities =
        new StableDomainIdentifierMap<LocalSessionIntensity>(new Dictionary<LocalSessionIntensity, string>
        {
            [LocalSessionIntensity.Low] = "Low",
            [LocalSessionIntensity.Moderate] = "Moderate",
            [LocalSessionIntensity.High] = "High",
        });

    private static readonly IReadOnlyDictionary<LocalProgrammingEventKind, string> EventKindIds =
        new Dictionary<LocalProgrammingEventKind, string>
        {
            [LocalProgrammingEventKind.Practice] = "Practice",
            [LocalProgrammingEventKind.Load] = "Load",
            [LocalProgrammingEventKind.FormalTest] = "FormalTest",
            [LocalProgrammingEventKind.Stabilization] = "Stabilization",
            [LocalProgrammingEventKind.Transfer] = "Transfer",
            [LocalProgrammingEventKind.Maintenance] = "Maintenance",
            [LocalProgrammingEventKind.GlobalReview] = "GlobalReview",
        };

    private static readonly IReadOnlyDictionary<LocalProgrammingEventKind, EvidenceArtifactCategory> ExpectedCategoryByEventKind =
        new Dictionary<LocalProgrammingEventKind, EvidenceArtifactCategory>
        {
            [LocalProgrammingEventKind.Practice] = EvidenceArtifactCategory.Practice,
            [LocalProgrammingEventKind.Load] = EvidenceArtifactCategory.Load,
            [LocalProgrammingEventKind.FormalTest] = EvidenceArtifactCategory.Test,
            [LocalProgrammingEventKind.Stabilization] = EvidenceArtifactCategory.Stabilization,
            [LocalProgrammingEventKind.Transfer] = EvidenceArtifactCategory.Transfer,
            [LocalProgrammingEventKind.Maintenance] = EvidenceArtifactCategory.Maintenance,
            [LocalProgrammingEventKind.GlobalReview] = EvidenceArtifactCategory.GlobalReview,
        };

    private static readonly string[] VagueEvidencePhrases =
    [
        "good effort",
        "great job",
        "nice work",
        "keep going",
        "felt focused",
        "felt meaningful",
        "felt good",
    ];

    private readonly JsonObject document;

    internal LocalProgrammingEventTransactionContext(JsonObject document)
    {
        this.document = document;
    }

    public void SetPractitionerState(PractitionerState practitionerState)
    {
        ArgumentNullException.ThrowIfNull(practitionerState);

        document[PractitionerStatePropertyName] = WritePractitionerState(practitionerState);
    }

    public void SaveEvidenceArtifact(LocalEvidenceArtifactRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        ValidateEvidenceArtifactRecord(record);

        UpsertByStringId(
            EvidenceArtifactsPropertyName,
            ArtifactIdPropertyName,
            record.ArtifactId,
            WriteEvidenceArtifactRecord(record),
            "The stored evidence artifact list is invalid.");
    }

    public void SaveFormalTestAttempt(LocalFormalTestAttemptRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        UpsertByStringId(
            FormalTestAttemptsPropertyName,
            AttemptIdPropertyName,
            record.AttemptId,
            WriteFormalTestAttemptRecord(record),
            "The stored formal test attempt history is invalid.");
    }

    public void SaveCompletedSession(LocalSessionHistoryRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        UpsertByStringId(
            CompletedSessionsPropertyName,
            SessionIdPropertyName,
            record.SessionId,
            WriteCompletedSessionRecord(record),
            "The stored completed session history is invalid.");
    }

    public void SaveStabilizationPass(LocalStabilizationPassRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        UpsertByStringId(
            StabilizationPassesPropertyName,
            PassIdPropertyName,
            record.PassId,
            LocalStabilizationPassStore.WriteRecord(record),
            "The stored stabilization pass history is invalid.");
    }

    public void SaveMaintenanceCheck(LocalMaintenanceCheckRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        UpsertByStringId(
            MaintenanceChecksPropertyName,
            CheckIdPropertyName,
            record.CheckId,
            LocalMaintenanceCheckStore.WriteMaintenanceRecord(record),
            "The stored maintenance check history is invalid.");
    }

    public void SaveRestorationCheck(LocalRestorationCheckRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        UpsertByStringId(
            RestorationChecksPropertyName,
            CheckIdPropertyName,
            record.CheckId,
            LocalMaintenanceCheckStore.WriteRestorationRecord(record),
            "The stored restoration check history is invalid.");
    }

    public void SaveGeneratedDrillInstance(LocalGeneratedDrillInstanceRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        UpsertByStringId(
            GeneratedDrillInstancesPropertyName,
            InstanceIdPropertyName,
            record.InstanceId,
            LocalGeneratedDrillInstanceStore.WriteRecord(record),
            "The stored generated drill instance history is invalid.");
    }

    public void SaveDailyTrainingPrescription(LocalDailyTrainingPrescriptionRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        LocalDailyTrainingPrescriptionStore.Upsert(document, record);
    }

    public void DeleteActiveRuntimeSessionSnapshot(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ArgumentException("Runtime session id is required.", nameof(sessionId));
        }

        var snapshots = ReadArray(
            ActiveRuntimeSessionSnapshotsPropertyName,
            "The stored active runtime session snapshot list is invalid.");
        var index = FindRecordIndex(snapshots, SessionIdPropertyName, sessionId);
        if (index >= 0)
        {
            snapshots.RemoveAt(index);
            document[ActiveRuntimeSessionSnapshotsPropertyName] = snapshots;
        }
    }

    public void ClearActiveRuntimeSessionSnapshots()
    {
        document[ActiveRuntimeSessionSnapshotsPropertyName] = new JsonArray();
    }

    private void UpsertByStringId(
        string collectionPropertyName,
        string idPropertyName,
        string id,
        JsonObject recordObject,
        string invalidCollectionMessage)
    {
        var records = ReadArray(collectionPropertyName, invalidCollectionMessage);
        var replacementIndex = FindRecordIndex(records, idPropertyName, id);

        if (replacementIndex >= 0)
        {
            records[replacementIndex] = recordObject;
        }
        else
        {
            records.AddNode(recordObject);
        }

        document[collectionPropertyName] = records;
    }

    private JsonArray ReadArray(
        string collectionPropertyName,
        string invalidCollectionMessage)
    {
        if (!document.TryGetPropertyValue(collectionPropertyName, out var collectionNode) ||
            collectionNode is null)
        {
            return [];
        }

        if (collectionNode is JsonArray records)
        {
            return records;
        }

        throw new InvalidOperationException(invalidCollectionMessage);
    }

    private static int FindRecordIndex(
        JsonArray records,
        string idPropertyName,
        string id)
    {
        for (var index = 0; index < records.Count; index++)
        {
            if (records[index] is JsonObject recordObject &&
                recordObject.TryGetPropertyValue(idPropertyName, out var idNode) &&
                idNode?.GetValue<string>() == id)
            {
                return index;
            }
        }

        return -1;
    }

    private static void ValidateEvidenceArtifactRecord(LocalEvidenceArtifactRecord record)
    {
        var expectedCategory = ExpectedCategoryByEventKind[record.Event.Kind];
        if (record.Artifact.Category != expectedCategory)
        {
            throw new ArgumentException(
                $"Evidence artifact category {record.Artifact.Category} does not match programming event kind {record.Event.Kind}.",
                nameof(record));
        }

        foreach (var evidence in record.Artifact.ObservableEvidence)
        {
            if (string.IsNullOrWhiteSpace(evidence.Description))
            {
                throw new ArgumentException(
                    "Observable evidence description is required.",
                    nameof(record));
            }

            if (VagueEvidencePhrases.Any(phrase => evidence.Description.Equals(phrase, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException(
                    "Observable evidence must constrain interpretation; vague encouragement cannot be persisted as evidence.",
                    nameof(record));
            }
        }
    }

    private static JsonObject WritePractitionerState(PractitionerState practitionerState)
    {
        var branchLevels = new JsonArray();

        foreach (var branchLevel in practitionerState.BranchLevels)
        {
            branchLevels.AddNode(new JsonObject
            {
                [BranchPropertyName] = StableDomainIdentifiers.Branches.ToPersistedId(branchLevel.Branch),
                [LevelPropertyName] = StableDomainIdentifiers.Levels.ToPersistedId(branchLevel.Level),
                [StatePropertyName] = StableDomainIdentifiers.BranchLevelStates.ToPersistedId(branchLevel.State),
            });
        }

        return new JsonObject
        {
            [PractitionerBranchLevelsPropertyName] = branchLevels,
        };
    }

    private static JsonObject WriteEvidenceArtifactRecord(LocalEvidenceArtifactRecord record)
    {
        return new JsonObject
        {
            [ArtifactIdPropertyName] = record.ArtifactId,
            [EventPropertyName] = WriteEvent(record.Event),
            [ArtifactPropertyName] = WriteArtifact(record.Artifact),
        };
    }

    private static JsonObject WriteEvent(LocalProgrammingEventReference eventReference)
    {
        var eventObject = new JsonObject
        {
            [EventIdPropertyName] = eventReference.EventId,
            [EventKindPropertyName] = EventKindIds[eventReference.Kind],
        };

        if (eventReference.Branch is { } branch)
        {
            eventObject[BranchPropertyName] = StableDomainIdentifiers.Branches.ToPersistedId(branch);
        }

        if (eventReference.Level is { } level)
        {
            eventObject[LevelPropertyName] = StableDomainIdentifiers.Levels.ToPersistedId(level);
        }

        if (eventReference.Drill is { } drill)
        {
            eventObject[DrillPropertyName] = StableDomainIdentifiers.Drills.ToPersistedId(drill);
        }

        return eventObject;
    }

    private static JsonObject WriteFormalTestAttemptRecord(LocalFormalTestAttemptRecord record)
    {
        var recordObject = new JsonObject
        {
            [AttemptIdPropertyName] = record.AttemptId,
            [AttemptPropertyName] = WriteAttempt(record.Attempt),
        };

        if (record.EvidenceArtifactId is not null)
        {
            recordObject[EvidenceArtifactIdPropertyName] = record.EvidenceArtifactId;
        }

        return recordObject;
    }

    private static JsonObject WriteAttempt(FormalTestAttempt attempt)
    {
        var attemptObject = new JsonObject
        {
            [BranchPropertyName] = StableDomainIdentifiers.Branches.ToPersistedId(attempt.Branch),
            [LevelPropertyName] = StableDomainIdentifiers.Levels.ToPersistedId(attempt.Level),
            [DatePropertyName] = WriteDate(attempt.Date),
            [TaskPropertyName] = WriteTask(attempt.Task),
            [LoadVariablesPropertyName] = WriteLoadVariables(attempt.LoadVariables),
            [StandardPropertyName] = attempt.Standard,
            [CriticalConstraintsPropertyName] = WriteCriticalConstraints(attempt.CriticalConstraints),
            [ResultEvidencePropertyName] = WriteResultEvidence(attempt.ResultEvidence),
            [PassStatePropertyName] = StableDomainIdentifiers.FormalTestPassStates.ToPersistedId(attempt.PassState),
            [ArtifactPropertyName] = WriteArtifact(attempt.Artifact),
        };

        if (attempt.FailureType is { } failureType)
        {
            attemptObject[FailureTypePropertyName] = StableDomainIdentifiers.FailureTypes.ToPersistedId(failureType);
        }

        return attemptObject;
    }

    private static JsonObject WriteTask(TestTask task)
    {
        var taskObject = new JsonObject();

        if (task.Drill is { } drill)
        {
            taskObject[DrillPropertyName] = StableDomainIdentifiers.Drills.ToPersistedId(drill);
        }

        if (task.TransferTask is not null)
        {
            taskObject[TransferTaskPropertyName] = task.TransferTask;
        }

        return taskObject;
    }

    private static JsonObject WriteResultEvidence(TestResultEvidence resultEvidence)
    {
        return new JsonObject
        {
            [ResultKindPropertyName] = StableDomainIdentifiers.TestResultEvidenceKinds.ToPersistedId(resultEvidence.Kind),
            [ResultValuePropertyName] = resultEvidence.Value,
        };
    }

    private static JsonArray WriteCriticalConstraints(IEnumerable<CriticalConstraint> constraints)
    {
        var constraintArray = new JsonArray();
        foreach (var constraint in constraints)
        {
            constraintArray.AddNode(new JsonObject
            {
                [DescriptionPropertyName] = constraint.Description,
            });
        }

        return constraintArray;
    }

    private static JsonObject WriteCompletedSessionRecord(LocalSessionHistoryRecord record)
    {
        var recordObject = new JsonObject
        {
            [SessionIdPropertyName] = record.SessionId,
            [DatePropertyName] = WriteDate(record.Date),
            [SessionTypePropertyName] = CompletedSessionTypes.ToPersistedId(record.SessionType),
            [SessionBranchLevelsPropertyName] = WriteSessionBranchLevels(record.BranchLevels),
            [IntensityPropertyName] = SessionIntensities.ToPersistedId(record.Intensity),
            [LoadVariablesPropertyName] = WriteLoadVariables(record.LoadVariables),
            [CleanPerformancePropertyName] = record.CleanPerformance,
            [NotesPropertyName] = record.Notes,
            [RecoveryMarkedPropertyName] = record.RecoveryMarked,
            [DeloadMarkedPropertyName] = record.DeloadMarked,
            [EvidenceArtifactIdsPropertyName] = WriteEvidenceArtifactIds(record.EvidenceArtifactIds),
        };

        if (record.Drill is { } drill)
        {
            recordObject[DrillPropertyName] = StableDomainIdentifiers.Drills.ToPersistedId(drill);
        }

        if (record.TransferTask is not null)
        {
            recordObject[TransferTaskPropertyName] = record.TransferTask;
        }

        return recordObject;
    }

    private static JsonArray WriteSessionBranchLevels(IEnumerable<LocalSessionBranchLevel> branchLevels)
    {
        var branchLevelArray = new JsonArray();
        foreach (var branchLevel in branchLevels)
        {
            branchLevelArray.AddNode(new JsonObject
            {
                [BranchPropertyName] = StableDomainIdentifiers.Branches.ToPersistedId(branchLevel.Branch),
                [LevelPropertyName] = StableDomainIdentifiers.Levels.ToPersistedId(branchLevel.Level),
            });
        }

        return branchLevelArray;
    }

    private static JsonArray WriteLoadVariables(IEnumerable<LoadVariable> loadVariables)
    {
        var loadVariableArray = new JsonArray();
        foreach (var loadVariable in loadVariables)
        {
            loadVariableArray.AddNode(new JsonObject
            {
                [NamePropertyName] = loadVariable.Name,
                [ValuePropertyName] = loadVariable.Value,
            });
        }

        return loadVariableArray;
    }

    private static JsonArray WriteEvidenceArtifactIds(IEnumerable<string> evidenceArtifactIds)
    {
        var artifactIdArray = new JsonArray();
        foreach (var artifactId in evidenceArtifactIds)
        {
            artifactIdArray.AddString(artifactId);
        }

        return artifactIdArray;
    }

    private static JsonObject WriteArtifact(EvidenceArtifact artifact)
    {
        var evidence = new JsonArray();
        foreach (var item in artifact.ObservableEvidence)
        {
            evidence.AddNode(new JsonObject
            {
                [KindPropertyName] = StableDomainIdentifiers.ObservableEvidenceKinds.ToPersistedId(item.Kind),
                [DescriptionPropertyName] = item.Description,
            });
        }

        var artifactObject = new JsonObject
        {
            [CategoryPropertyName] = StableDomainIdentifiers.EvidenceArtifactCategories.ToPersistedId(artifact.Category),
            [DatePropertyName] = WriteDate(artifact.Date),
            [ObservableEvidencePropertyName] = evidence,
            [SummaryOrReferencePropertyName] = artifact.SummaryOrReference,
        };

        if (artifact.SubjectiveNote is not null)
        {
            artifactObject[SubjectiveNotePropertyName] = artifact.SubjectiveNote;
        }

        return artifactObject;
    }

    private static JsonObject WriteDate(TrainingDate date)
    {
        return new JsonObject
        {
            [YearPropertyName] = date.Year,
            [MonthPropertyName] = date.Month,
            [DayPropertyName] = date.Day,
        };
    }
}
