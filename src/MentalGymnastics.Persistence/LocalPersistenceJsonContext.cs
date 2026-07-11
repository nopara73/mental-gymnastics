using System.Text.Json.Serialization;

namespace MentalGymnastics.Persistence;

[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(LocalActiveRuntimeSessionSnapshotRecord))]
internal sealed partial class LocalPersistenceJsonContext : JsonSerializerContext;
