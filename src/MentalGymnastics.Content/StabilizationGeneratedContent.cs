using MentalGymnastics.Core;

namespace MentalGymnastics.Content;

public static class StabilizationGeneratedContent
{
    public const string ControlledDistractorId = "stabilization-controlled-distractor";

    public static IReadOnlyList<GeneratedContentMaterial> AddControlledDistractor(
        GeneratedDrillContentResult result,
        IEnumerable<GeneratedContentMaterial> materials)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(materials);

        var materialArray = materials.ToArray();
        if (result.SessionType != SessionType.Stabilization ||
            materialArray.Any(material => string.Equals(
                material.Name,
                ControlledDistractorId,
                StringComparison.Ordinal)))
        {
            return materialArray;
        }

        return
        [
            .. materialArray,
            new GeneratedContentMaterial(
                GeneratedContentMaterialKind.Interference,
                ControlledDistractorId,
                "Irrelevant signal. Keep the current rule and do not respond."),
        ];
    }
}
