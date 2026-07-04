using MentalGymnastics.Core;

namespace MentalGymnastics.Content;

internal static class GeneratedContentBoundaryHandoffValidation
{
    private static readonly IReadOnlyList<GeneratedContentMaterialKind> RequiredTransferMaterials =
    [
        GeneratedContentMaterialKind.SourceBranchStandard,
        GeneratedContentMaterialKind.TransferTask,
        GeneratedContentMaterialKind.SameDemand,
        GeneratedContentMaterialKind.ChangedContext,
        GeneratedContentMaterialKind.TransferDistance,
        GeneratedContentMaterialKind.RetestRequirement,
    ];

    public static IReadOnlyList<GeneratedContentMaterial> EnsureCanHandoff(
        GeneratedDrillContentResult result,
        IEnumerable<GeneratedContentMaterial> materials)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(materials);

        var materialArray = materials.ToArray();
        if (materialArray.Any(material => material is null))
        {
            throw new ArgumentException(
                "Generated content handoff material collections cannot contain null entries.",
                nameof(materials));
        }

        if (result.SessionType == SessionType.Transfer)
        {
            EnsureTransferContentCanHandoff(result, materialArray);
            return Array.AsReadOnly(materialArray);
        }

        _ = ValidatedGeneratedDrillContent.Create(result, materialArray);
        EnsureAntiSelfDeceptionGuardPasses(result, materialArray);
        return Array.AsReadOnly(materialArray);
    }

    private static void EnsureTransferContentCanHandoff(
        GeneratedDrillContentResult result,
        IReadOnlyCollection<GeneratedContentMaterial> materials)
    {
        var failures = new List<string>();

        if (result.Request.FreshnessPolicy != PromptFreshnessPolicy.FreshEquivalentRequired)
        {
            failures.Add("transfer generated content must use fresh equivalent material.");
        }

        foreach (var requiredMaterial in RequiredTransferMaterials)
        {
            if (materials.Any(material => material.Kind == requiredMaterial))
            {
                continue;
            }

            failures.Add($"transfer generated content is missing {requiredMaterial} material.");
        }

        AddSourceStandardVisibilityFailures(result, materials, failures);

        foreach (var loadVariable in result.Request.LoadVariables)
        {
            if (materials.Any(material =>
                    material.Kind == GeneratedContentMaterialKind.LoadVariable &&
                    string.Equals(material.Name, loadVariable.Name, StringComparison.Ordinal) &&
                    string.Equals(material.Value, loadVariable.Value, StringComparison.Ordinal)))
            {
                continue;
            }

            failures.Add(
                $"transfer generated content is missing requested load variable '{loadVariable.Name}' with value '{loadVariable.Value}'.");
        }

        foreach (var constraint in result.Request.CriticalConstraints)
        {
            if (materials.Any(material =>
                    material.Kind == GeneratedContentMaterialKind.HonestyConstraint &&
                    string.Equals(material.Value, constraint.Description, StringComparison.Ordinal)))
            {
                continue;
            }

            failures.Add(
                $"transfer generated content does not preserve honesty constraint '{constraint.Description}'.");
        }

        if (failures.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            "Transfer generated content cannot be handed off until transfer material validation passes: " +
            string.Join("; ", failures));
    }

    private static void AddSourceStandardVisibilityFailures(
        GeneratedDrillContentResult result,
        IEnumerable<GeneratedContentMaterial> materials,
        ICollection<string> failures)
    {
        var sourceStandard = ProgramCatalog.Standards.Single(standard =>
            standard.Branch == result.Branch &&
            standard.Level == result.Level).Standard;
        var sourceStandardMaterials = materials
            .Where(material => material.Kind == GeneratedContentMaterialKind.SourceBranchStandard)
            .ToArray();

        if (sourceStandardMaterials.Any(material =>
                material.Value.Contains(sourceStandard, StringComparison.OrdinalIgnoreCase) &&
                material.Value.Contains("visible in the transfer artifact", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        failures.Add(
            "transfer generated content must keep the catalog source branch standard visible in the transfer artifact.");
    }

    private static void EnsureAntiSelfDeceptionGuardPasses(
        GeneratedDrillContentResult result,
        IEnumerable<GeneratedContentMaterial> materials)
    {
        var standard = ProgramCatalog.Standards.Single(item =>
            item.Branch == result.Branch &&
            item.Level == result.Level).Standard;
        var loadIntent = string.Join(
            "; ",
            result.Request.LoadVariables.Select(variable => variable.Name + ": " + variable.Value));
        var guard = GeneratedContentAntiSelfDeceptionGuard.Evaluate(
            new GeneratedContentAntiSelfDeceptionGuardRequest(
                result,
                materials,
                new GeneratedContentEquivalenceRequirement(result.Request, standard, loadIntent),
                new GeneratedContentEquivalenceCandidate(result.Instance, standard, loadIntent),
                LoadChangeMode.Acquisition));

        if (guard.IsValid)
        {
            return;
        }

        throw new InvalidOperationException(
            "Generated content cannot be handed off until anti-self-deception guard passes: " +
            string.Join("; ", guard.Findings.Select(finding => finding.Detail)));
    }
}
