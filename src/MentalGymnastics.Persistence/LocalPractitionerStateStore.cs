using System.Text.Json;
using System.Text.Json.Nodes;
using MentalGymnastics.Core;

namespace MentalGymnastics.Persistence;

public sealed class LocalPractitionerStateStore
{
    private const string PractitionerStatePropertyName = "PractitionerState";
    private const string BranchLevelsPropertyName = "BranchLevels";
    private const string BranchPropertyName = "Branch";
    private const string LevelPropertyName = "Level";
    private const string StatePropertyName = "State";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly LocalDatabaseOptions options;
    private readonly LocalDatabaseInitializer initializer;

    public LocalPractitionerStateStore(LocalDatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        this.options = options;
        initializer = new LocalDatabaseInitializer(options);
    }

    public async ValueTask<PractitionerState?> LoadAsync(CancellationToken cancellationToken = default)
    {
        var document = await ReadInitializedDocumentAsync(cancellationToken).ConfigureAwait(false);

        if (!document.TryGetPropertyValue(PractitionerStatePropertyName, out var practitionerStateNode) ||
            practitionerStateNode is null)
        {
            return null;
        }

        if (practitionerStateNode is not JsonObject practitionerStateObject)
        {
            throw new InvalidOperationException("The stored practitioner state is invalid.");
        }

        return ReadPractitionerState(practitionerStateObject);
    }

    public async ValueTask SaveAsync(
        PractitionerState practitionerState,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(practitionerState);

        var document = await ReadInitializedDocumentAsync(cancellationToken).ConfigureAwait(false);
        document[PractitionerStatePropertyName] = WritePractitionerState(practitionerState);

        await ReplaceDatabaseAsync(document, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<PractitionerState> UpdateAsync(
        Func<PractitionerState?, PractitionerState> update,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        var current = await LoadAsync(cancellationToken).ConfigureAwait(false);
        var updated = update(current);
        ArgumentNullException.ThrowIfNull(updated);

        await SaveAsync(updated, cancellationToken).ConfigureAwait(false);
        return updated;
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

        var document = await JsonSerializer.DeserializeAsync<JsonObject>(
            stream,
            JsonOptions,
            cancellationToken).ConfigureAwait(false);

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

        await JsonSerializer.SerializeAsync(stream, document, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static JsonObject WritePractitionerState(PractitionerState practitionerState)
    {
        var branchLevels = new JsonArray();

        foreach (var branchLevel in practitionerState.BranchLevels)
        {
            branchLevels.Add(new JsonObject
            {
                [BranchPropertyName] = StableDomainIdentifiers.Branches.ToPersistedId(branchLevel.Branch),
                [LevelPropertyName] = StableDomainIdentifiers.Levels.ToPersistedId(branchLevel.Level),
                [StatePropertyName] = StableDomainIdentifiers.BranchLevelStates.ToPersistedId(branchLevel.State),
            });
        }

        return new JsonObject
        {
            [BranchLevelsPropertyName] = branchLevels,
        };
    }

    private static PractitionerState ReadPractitionerState(JsonObject practitionerStateObject)
    {
        if (!practitionerStateObject.TryGetPropertyValue(BranchLevelsPropertyName, out var branchLevelsNode) ||
            branchLevelsNode is not JsonArray branchLevelsArray)
        {
            throw new InvalidOperationException("The stored practitioner state is missing branch-level states.");
        }

        var branchLevels = new List<BranchLevelStatus>();
        foreach (var branchLevelNode in branchLevelsArray)
        {
            if (branchLevelNode is not JsonObject branchLevelObject)
            {
                throw new InvalidOperationException("The stored practitioner branch-level state is invalid.");
            }

            branchLevels.Add(new BranchLevelStatus(
                StableDomainIdentifiers.Branches.FromPersistedId(ReadRequiredString(branchLevelObject, BranchPropertyName)),
                StableDomainIdentifiers.Levels.FromPersistedId(ReadRequiredString(branchLevelObject, LevelPropertyName)),
                StableDomainIdentifiers.BranchLevelStates.FromPersistedId(ReadRequiredString(branchLevelObject, StatePropertyName))));
        }

        return new PractitionerState(branchLevels);
    }

    private static string ReadRequiredString(JsonObject jsonObject, string propertyName)
    {
        if (!jsonObject.TryGetPropertyValue(propertyName, out var propertyNode) ||
            propertyNode is null)
        {
            throw new InvalidOperationException($"The stored practitioner branch-level state is missing {propertyName}.");
        }

        try
        {
            return propertyNode.GetValue<string>();
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException(
                $"The stored practitioner branch-level state has an invalid {propertyName}.",
                exception);
        }
    }
}
