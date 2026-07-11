using System.Text.Json;
using System.Text.Json.Nodes;

namespace MentalGymnastics.Persistence;

internal static class LocalJsonDocumentIO
{
    public static void AddNode(this JsonArray array, JsonNode? node)
    {
        ArgumentNullException.ThrowIfNull(array);
        array.Add(node);
    }

    public static void AddString(this JsonArray array, string? value)
    {
        ArgumentNullException.ThrowIfNull(array);
        array.Add((JsonNode?)JsonValue.Create(value));
    }

    public static async ValueTask<JsonObject?> ReadObjectAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var node = await JsonNode.ParseAsync(
            stream,
            documentOptions: default,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return node as JsonObject;
    }

    public static async ValueTask WriteObjectAsync(
        Stream stream,
        JsonObject document,
        JsonSerializerOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(options);

        await using var writer = new Utf8JsonWriter(
            stream,
            new JsonWriterOptions { Indented = options.WriteIndented });
        document.WriteTo(writer, options);
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
