using System.Text.Json;
using System.Text.Json.Nodes;
using MentalGymnastics.Core;

namespace MentalGymnastics.Persistence;

public enum LocalDailyTrainingBlockRole
{
    Practice,
    Load,
    Test,
    Stabilization,
    Maintenance,
    Regression,
    Transfer,
    Recovery,
    Review,
}

public enum LocalDailyTrainingBlockState
{
    Planned,
    Prepared,
    Active,
    Completed,
    Failed,
    Abandoned,
    TimedOut,
    Skipped,
}

public sealed class LocalDailyTrainingBlockRecord
{
    public LocalDailyTrainingBlockRecord(
        string blockId,
        int order,
        BranchCode branch,
        GlobalLevelId level,
        DrillId drill,
        LocalDailyTrainingBlockRole role,
        IEnumerable<LoadVariable> loadVariables,
        LocalDailyTrainingBlockState state,
        string? sessionId = null)
    {
        if (string.IsNullOrWhiteSpace(blockId))
        {
            throw new ArgumentException("Daily training block id is required.", nameof(blockId));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(order);
        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown daily block role.");
        }

        if (!Enum.IsDefined(state))
        {
            throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown daily block state.");
        }

        ArgumentNullException.ThrowIfNull(loadVariables);
        var loads = loadVariables.ToArray();
        if (loads.Length == 0 || loads.Any(load =>
                string.IsNullOrWhiteSpace(load.Name) || string.IsNullOrWhiteSpace(load.Value)))
        {
            throw new ArgumentException("Daily training blocks require named load variables.", nameof(loadVariables));
        }

        var normalizedSessionId = string.IsNullOrWhiteSpace(sessionId) ? null : sessionId.Trim();
        if ((state is LocalDailyTrainingBlockState.Prepared or
                LocalDailyTrainingBlockState.Active or
                LocalDailyTrainingBlockState.Completed or
                LocalDailyTrainingBlockState.Failed or
                LocalDailyTrainingBlockState.Abandoned or
                LocalDailyTrainingBlockState.TimedOut) &&
            normalizedSessionId is null)
        {
            throw new ArgumentException("Started and terminal daily blocks require their runtime session id.", nameof(sessionId));
        }

        if (state is LocalDailyTrainingBlockState.Planned or LocalDailyTrainingBlockState.Skipped &&
            normalizedSessionId is not null)
        {
            throw new ArgumentException("Unstarted daily blocks cannot reference a runtime session.", nameof(sessionId));
        }

        BlockId = blockId;
        Order = order;
        Branch = branch;
        Level = level;
        Drill = drill;
        Role = role;
        LoadVariables = Array.AsReadOnly(loads);
        State = state;
        SessionId = normalizedSessionId;
    }

    public string BlockId { get; }

    public int Order { get; }

    public BranchCode Branch { get; }

    public GlobalLevelId Level { get; }

    public DrillId Drill { get; }

    public LocalDailyTrainingBlockRole Role { get; }

    public IReadOnlyList<LoadVariable> LoadVariables { get; }

    public LocalDailyTrainingBlockState State { get; }

    public string? SessionId { get; }

    public bool IsTerminal => State is
        LocalDailyTrainingBlockState.Completed or
        LocalDailyTrainingBlockState.Failed or
        LocalDailyTrainingBlockState.Abandoned or
        LocalDailyTrainingBlockState.TimedOut;
}

public sealed class LocalDailyTrainingPrescriptionRecord
{
    public LocalDailyTrainingPrescriptionRecord(
        string prescriptionId,
        TrainingDate date,
        TrainingDate cycleAnchor,
        int cycleDay,
        WeeklySessionKind weeklySession,
        DailyTrainingDoseState state,
        IEnumerable<LocalDailyTrainingBlockRecord> blocks)
    {
        if (string.IsNullOrWhiteSpace(prescriptionId))
        {
            throw new ArgumentException("Daily prescription id is required.", nameof(prescriptionId));
        }

        if (cycleDay is < 1 or > 7)
        {
            throw new ArgumentOutOfRangeException(nameof(cycleDay), cycleDay, "Cycle day must be between one and seven.");
        }

        if (!Enum.IsDefined(weeklySession))
        {
            throw new ArgumentOutOfRangeException(nameof(weeklySession), weeklySession, "Unknown weekly session kind.");
        }

        if (!Enum.IsDefined(state))
        {
            throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown daily dose state.");
        }

        ArgumentNullException.ThrowIfNull(blocks);
        var blockArray = blocks.OrderBy(block => block.Order).ToArray();
        if (blockArray.Select(block => block.BlockId).Distinct(StringComparer.Ordinal).Count() != blockArray.Length ||
            !blockArray.Select(block => block.Order).SequenceEqual(Enumerable.Range(1, blockArray.Length)))
        {
            throw new ArgumentException("Daily block ids and one-based order values must be unique and contiguous.", nameof(blocks));
        }

        var activeCount = blockArray.Count(block => block.State == LocalDailyTrainingBlockState.Active);
        var preparedCount = blockArray.Count(block => block.State == LocalDailyTrainingBlockState.Prepared);
        if (activeCount > 1)
        {
            throw new ArgumentException("A daily prescription cannot have more than one active block.", nameof(blocks));
        }


        if (preparedCount > 1 || preparedCount > 0 && activeCount > 0)
        {
            throw new ArgumentException("A daily prescription can have only one prepared or active block.", nameof(blocks));
        }

        var isOff = (weeklySession is WeeklySessionKind.Off or WeeklySessionKind.OffOrRecovery) &&
            blockArray.Length == 0;
        if (blockArray.Length == 0 && (!isOff || state != DailyTrainingDoseState.Completed))
        {
            throw new ArgumentException("Only a completed off-day prescription may contain no blocks.", nameof(blocks));
        }

        var terminalCount = blockArray.Count(block => block.IsTerminal);
        _ = blockArray.Length == 0
            ? DailyTrainingDoseProgress.OffDay()
            : new DailyTrainingDoseProgress(blockArray.Length, terminalCount, state);

        if (state == DailyTrainingDoseState.Planned && blockArray.Any(block =>
                block.State is not LocalDailyTrainingBlockState.Planned and not LocalDailyTrainingBlockState.Prepared))
        {
            throw new ArgumentException("A planned daily prescription can contain only planned or prepared blocks.", nameof(blocks));
        }

        if (state == DailyTrainingDoseState.Active && activeCount == 0 && terminalCount == 0)
        {
            throw new ArgumentException("An active daily prescription requires an active or terminal block.", nameof(blocks));
        }

        if (state == DailyTrainingDoseState.Completed && blockArray.Any(block => !block.IsTerminal))
        {
            throw new ArgumentException("A completed daily prescription requires every block to be terminal.", nameof(blocks));
        }

        if (state == DailyTrainingDoseState.Stopped && blockArray.Any(block =>
                !block.IsTerminal && block.State != LocalDailyTrainingBlockState.Skipped))
        {
            throw new ArgumentException("A stopped daily prescription requires every remaining block to be skipped.", nameof(blocks));
        }

        PrescriptionId = prescriptionId;
        Date = date;
        CycleAnchor = cycleAnchor;
        CycleDay = cycleDay;
        WeeklySession = weeklySession;
        State = state;
        Blocks = Array.AsReadOnly(blockArray);
    }

    public string PrescriptionId { get; }

    public TrainingDate Date { get; }

    public TrainingDate CycleAnchor { get; }

    public int CycleDay { get; }

    public WeeklySessionKind WeeklySession { get; }

    public DailyTrainingDoseState State { get; }

    public IReadOnlyList<LocalDailyTrainingBlockRecord> Blocks { get; }

    public bool IsTerminal => State is DailyTrainingDoseState.Completed or DailyTrainingDoseState.Stopped;
}

public sealed class LocalDailyTrainingPrescriptionStore
{
    internal const string PropertyName = "DailyTrainingPrescriptions";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly IStableDomainIdentifierMap<LocalDailyTrainingBlockRole> Roles =
        Map<LocalDailyTrainingBlockRole>();
    private static readonly IStableDomainIdentifierMap<LocalDailyTrainingBlockState> BlockStates =
        Map<LocalDailyTrainingBlockState>();
    private static readonly IStableDomainIdentifierMap<DailyTrainingDoseState> DoseStates =
        Map<DailyTrainingDoseState>();
    private static readonly IStableDomainIdentifierMap<WeeklySessionKind> WeeklySessions =
        Map<WeeklySessionKind>();

    private readonly LocalDatabaseOptions options;
    private readonly LocalDatabaseInitializer initializer;

    public LocalDailyTrainingPrescriptionStore(LocalDatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.options = options;
        initializer = new LocalDatabaseInitializer(options);
    }

    public async ValueTask SaveAsync(
        LocalDailyTrainingPrescriptionRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        var document = await ReadInitializedDocumentAsync(cancellationToken).ConfigureAwait(false);
        Upsert(document, record);
        await ReplaceDatabaseAsync(document, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<LocalDailyTrainingPrescriptionRecord?> LoadByDateAsync(
        TrainingDate date,
        CancellationToken cancellationToken = default)
    {
        var records = await ListAsync(cancellationToken).ConfigureAwait(false);
        return records.SingleOrDefault(record => record.Date == date);
    }

    public async ValueTask<LocalDailyTrainingPrescriptionRecord?> LoadLatestAsync(
        CancellationToken cancellationToken = default)
    {
        var records = await ListAsync(cancellationToken).ConfigureAwait(false);
        return records.LastOrDefault();
    }

    public async ValueTask<IReadOnlyList<LocalDailyTrainingPrescriptionRecord>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        var document = await ReadInitializedDocumentAsync(cancellationToken).ConfigureAwait(false);
        return ReadArray(document)
            .Select(ReadRecord)
            .OrderBy(record => record.Date.Year)
            .ThenBy(record => record.Date.Month)
            .ThenBy(record => record.Date.Day)
            .ToArray();
    }

    internal static void Upsert(JsonObject document, LocalDailyTrainingPrescriptionRecord record)
    {
        var records = ReadArray(document);
        var sameDate = records
            .Select((node, index) => (Node: node as JsonObject, Index: index))
            .FirstOrDefault(item => item.Node is not null && ReadDate(item.Node, "Date") == record.Date);
        if (sameDate.Node is not null)
        {
            var existingId = RequiredString(sameDate.Node, "PrescriptionId");
            if (!string.Equals(existingId, record.PrescriptionId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("A different daily prescription already owns this program date.");
            }

            records[sameDate.Index] = WriteRecord(record);
        }
        else
        {
            records.AddNode(WriteRecord(record));
        }

        document[PropertyName] = records;
    }

    internal static IReadOnlyList<LocalDailyTrainingPrescriptionRecord> ReadFrom(JsonObject document)
    {
        return ReadArray(document).Select(ReadRecord).ToArray();
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
        var document = await LocalJsonDocumentIO.ReadObjectAsync(stream, cancellationToken).ConfigureAwait(false);
        if (document is null ||
            !document.TryGetPropertyValue("Kind", out var kind) ||
            kind?.GetValue<string>() != LocalDatabaseSchema.MetadataKind)
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
            await using (var stream = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                useAsync: true))
            {
                await LocalJsonDocumentIO.WriteObjectAsync(stream, document, JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

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

    private static JsonArray ReadArray(JsonObject document)
    {
        if (!document.TryGetPropertyValue(PropertyName, out var node) || node is null)
        {
            return [];
        }

        return node as JsonArray ?? throw new InvalidOperationException("Daily training prescriptions must be a JSON array.");
    }

    private static JsonObject WriteRecord(LocalDailyTrainingPrescriptionRecord record)
    {
        var blocks = new JsonArray();
        foreach (var block in record.Blocks)
        {
            var loads = new JsonArray();
            foreach (var load in block.LoadVariables)
            {
                loads.AddNode(new JsonObject
                {
                    ["Name"] = load.Name,
                    ["Value"] = load.Value,
                });
            }

            blocks.AddNode(new JsonObject
            {
                ["BlockId"] = block.BlockId,
                ["Order"] = block.Order,
                ["Branch"] = StableDomainIdentifiers.Branches.ToPersistedId(block.Branch),
                ["Level"] = StableDomainIdentifiers.Levels.ToPersistedId(block.Level),
                ["Drill"] = StableDomainIdentifiers.Drills.ToPersistedId(block.Drill),
                ["Role"] = Roles.ToPersistedId(block.Role),
                ["LoadVariables"] = loads,
                ["State"] = BlockStates.ToPersistedId(block.State),
                ["SessionId"] = block.SessionId,
            });
        }

        return new JsonObject
        {
            ["PrescriptionId"] = record.PrescriptionId,
            ["Date"] = WriteDate(record.Date),
            ["CycleAnchor"] = WriteDate(record.CycleAnchor),
            ["CycleDay"] = record.CycleDay,
            ["WeeklySession"] = WeeklySessions.ToPersistedId(record.WeeklySession),
            ["State"] = DoseStates.ToPersistedId(record.State),
            ["Blocks"] = blocks,
        };
    }

    private static LocalDailyTrainingPrescriptionRecord ReadRecord(JsonNode? node)
    {
        var record = node as JsonObject ?? throw new InvalidOperationException("Daily prescription record must be an object.");
        var blocks = RequiredArray(record, "Blocks")
            .Select(ReadBlock)
            .ToArray();
        return new LocalDailyTrainingPrescriptionRecord(
            RequiredString(record, "PrescriptionId"),
            ReadDate(record, "Date"),
            ReadDate(record, "CycleAnchor"),
            RequiredInt(record, "CycleDay"),
            WeeklySessions.FromPersistedId(RequiredString(record, "WeeklySession")),
            DoseStates.FromPersistedId(RequiredString(record, "State")),
            blocks);
    }

    private static LocalDailyTrainingBlockRecord ReadBlock(JsonNode? node)
    {
        var block = node as JsonObject ?? throw new InvalidOperationException("Daily block record must be an object.");
        var loads = RequiredArray(block, "LoadVariables")
            .Select(loadNode =>
            {
                var load = loadNode as JsonObject ?? throw new InvalidOperationException("Daily block load must be an object.");
                return new LoadVariable(RequiredString(load, "Name"), RequiredString(load, "Value"));
            })
            .ToArray();
        return new LocalDailyTrainingBlockRecord(
            RequiredString(block, "BlockId"),
            RequiredInt(block, "Order"),
            StableDomainIdentifiers.Branches.FromPersistedId(RequiredString(block, "Branch")),
            StableDomainIdentifiers.Levels.FromPersistedId(RequiredString(block, "Level")),
            StableDomainIdentifiers.Drills.FromPersistedId(RequiredString(block, "Drill")),
            Roles.FromPersistedId(RequiredString(block, "Role")),
            loads,
            BlockStates.FromPersistedId(RequiredString(block, "State")),
            OptionalString(block, "SessionId"));
    }

    private static JsonObject WriteDate(TrainingDate date) => new()
    {
        ["Year"] = date.Year,
        ["Month"] = date.Month,
        ["Day"] = date.Day,
    };

    private static TrainingDate ReadDate(JsonObject parent, string property)
    {
        var value = parent[property] as JsonObject ??
            throw new InvalidOperationException($"Daily prescription {property} is missing or invalid.");
        return TrainingDate.From(
            RequiredInt(value, "Year"),
            RequiredInt(value, "Month"),
            RequiredInt(value, "Day"));
    }

    private static JsonArray RequiredArray(JsonObject parent, string property) =>
        parent[property] as JsonArray ?? throw new InvalidOperationException($"Daily prescription {property} is missing or invalid.");

    private static string RequiredString(JsonObject parent, string property) =>
        parent[property]?.GetValue<string>() is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Daily prescription {property} is missing or invalid.");

    private static string? OptionalString(JsonObject parent, string property) =>
        parent[property] is null ? null : parent[property]!.GetValue<string>();

    private static int RequiredInt(JsonObject parent, string property) =>
        parent[property]?.GetValue<int>() ??
        throw new InvalidOperationException($"Daily prescription {property} is missing or invalid.");

    private static IStableDomainIdentifierMap<TEnum> Map<TEnum>()
        where TEnum : struct, Enum
    {
        return new StableDomainIdentifierMap<TEnum>(
            Enum.GetValues<TEnum>().ToDictionary(value => value, value => value.ToString()));
    }
}
