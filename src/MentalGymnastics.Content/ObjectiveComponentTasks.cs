using MentalGymnastics.Core;

namespace MentalGymnastics.Content;

internal sealed record ObjectiveComponentTask(
    BranchCode Branch,
    string Prompt,
    string ExpectedResponse);

internal static class ObjectiveComponentTaskCatalog
{
    private static readonly IReadOnlyDictionary<BranchCode, ObjectiveComponentTask[]> Tasks =
        new Dictionary<BranchCode, ObjectiveComponentTask[]>
        {
            [BranchCode.FH] =
            [
                new(BranchCode.FH, "Hold CEDAR-7 while completing every other component. Report it last.", "CEDAR-7"),
                new(BranchCode.FH, "Hold AMBER-4 while completing every other component. Report it last.", "AMBER-4"),
                new(BranchCode.FH, "Hold NORTH-6 while completing every other component. Report it last.", "NORTH-6"),
            ],
            [BranchCode.FS] =
            [
                new(BranchCode.FS, "Start on A. Apply SWITCH, HOLD, SWITCH. Report the final track.", "A"),
                new(BranchCode.FS, "Start on B. Apply HOLD, SWITCH, SWITCH, HOLD. Report the final track.", "B"),
                new(BranchCode.FS, "Start on A. Apply SWITCH, SWITCH, HOLD, SWITCH. Report the final track.", "B"),
            ],
            [BranchCode.WM] =
            [
                new(BranchCode.WM, "Reverse 2-5-8, then add 1 to each value.", "9-6-3"),
                new(BranchCode.WM, "Rotate 3-7-4 left once, then subtract 1 from each value.", "6-3-2"),
                new(BranchCode.WM, "Reverse 6-1-4, then add 2 to each value.", "6-3-8"),
            ],
            [BranchCode.IR] =
            [
                new(BranchCode.IR, "Enter Y for even values, except 6 is N: 2, 6, 5, 8.", "Y-N-N-Y"),
                new(BranchCode.IR, "Enter Y for vowels, except E is N: A, E, K, O.", "Y-N-N-Y"),
                new(BranchCode.IR, "Enter Y for values above 4, except 7 is N: 5, 3, 7, 9.", "Y-N-N-Y"),
            ],
            [BranchCode.DE] =
            [
                new(BranchCode.DE, "One code differs from K4M7: K4M7, K4N7, K4M7. Report its position.", "2"),
                new(BranchCode.DE, "One code differs from P8R2: P8R2, P8R2, P8K2. Report its position.", "3"),
                new(BranchCode.DE, "One code differs from T3Q9: T8Q9, T3Q9, T3Q9. Report its position.", "1"),
            ],
            [BranchCode.CO] =
            [
                new(BranchCode.CO, "Key opens lock. Password opens what?", "ACCOUNT"),
                new(BranchCode.CO, "Thermometer measures temperature. Scale measures what?", "WEIGHT"),
                new(BranchCode.CO, "Blueprint guides construction. Recipe guides what?", "COOKING"),
            ],
            [BranchCode.AI] =
            [
                new(BranchCode.AI, "Under the stated pressure, sort 8-3-5 ascending without changing the task.", "3-5-8"),
                new(BranchCode.AI, "Under the stated pressure, sort 7-2-9 descending without changing the task.", "9-7-2"),
                new(BranchCode.AI, "Under the stated pressure, double 3-5-2 without changing the task.", "6-10-4"),
            ],
            [BranchCode.TI] =
            [
                new(BranchCode.TI, "Combine 4 + 3, then reverse the digits in 12. Report both results in order.", "7-21"),
                new(BranchCode.TI, "Combine 6 + 2, then reverse the digits in 31. Report both results in order.", "8-13"),
                new(BranchCode.TI, "Combine 5 + 4, then reverse the digits in 24. Report both results in order.", "9-42"),
            ],
        };

    public static ObjectiveComponentTask Select(
        BranchCode branch,
        string seedMaterial,
        int variantOffset = 0)
    {
        GeneratedContentValidation.EnsureDefined(branch, nameof(branch));
        if (string.IsNullOrWhiteSpace(seedMaterial))
        {
            throw new ArgumentException("Component task selection requires seed material.", nameof(seedMaterial));
        }

        var candidates = Tasks[branch];
        var hash = GeneratedContentStableHash.HashSegment($"{seedMaterial}|objective-component|{branch}");
        var baseIndex = Convert.ToInt32(hash[..6], 16);
        return candidates[(baseIndex + variantOffset) % candidates.Length];
    }

    public static string PayloadValue(
        ObjectiveComponentTask task,
        GlobalLevelId level,
        DrillId drill,
        string taskRole,
        string sourceDemand,
        string sourceStandard,
        string honestyConstraint)
    {
        ArgumentNullException.ThrowIfNull(task);

        return $"component branch {task.Branch}: level {level}; drill {drill}; task role {taskRole}; challenge {task.Prompt}; response format {task.Branch}=<answer>; pass criterion exact response matches the hidden key and no {task.Branch} error is recorded; capacity reference {sourceDemand}; owned standard reference {sourceStandard}; component honesty constraint {honestyConstraint}; this integrated check does not replace branch ownership evidence; component boundary and evidence remain separate.";
    }

    public static string ScoringKeyValue(
        ObjectiveComponentTask task,
        string scoringStandard)
    {
        ArgumentNullException.ThrowIfNull(task);

        return $"component branch {task.Branch}; expected response {task.ExpectedResponse}; scoring standard {scoringStandard}; strong branch cannot hide missing or failing {task.Branch} evidence.";
    }

    public static void AddMaterials(
        ICollection<GeneratedContentMaterial> materials,
        IEnumerable<BranchCode> branches,
        string seedMaterial,
        int variantOffset,
        string namePrefix,
        GlobalLevelId componentLevel = GlobalLevelId.L3)
    {
        ArgumentNullException.ThrowIfNull(materials);
        ArgumentNullException.ThrowIfNull(branches);
        if (string.IsNullOrWhiteSpace(seedMaterial))
        {
            throw new ArgumentException("Component material generation requires seed material.", nameof(seedMaterial));
        }

        foreach (var branch in branches.Distinct())
        {
            var standard = ProgramCatalog.Standards.Single(item =>
                item.Branch == branch && item.Level == componentLevel);
            var drillId = ExecutableStandardCatalog.Get(branch, componentLevel).Drill;
            var drill = ProgramCatalog.Drills.Single(item => item.Id == drillId);
            var task = Select(branch, seedMaterial, variantOffset);
            var branchId = branch.ToString().ToLowerInvariant();

            materials.Add(new GeneratedContentMaterial(
                GeneratedContentMaterialKind.ComponentPayload,
                $"{namePrefix}-component-{branchId}",
                PayloadValue(
                    task,
                    componentLevel,
                    drillId,
                    "complete this demand without dropping the primary task",
                    standard.Demand,
                    standard.Standard,
                    drill.HonestyConstraint)));
            materials.Add(new GeneratedContentMaterial(
                GeneratedContentMaterialKind.ComponentEvidenceRequirement,
                $"{namePrefix}-evidence-{branchId}",
                $"branch {branch} requires a separate {branch}=<answer> response and its own error record; missing evidence fails this component."));
            materials.Add(new GeneratedContentMaterial(
                GeneratedContentMaterialKind.BranchScoringKey,
                $"{namePrefix}-scoring-{branchId}",
                ScoringKeyValue(
                    task,
                    $"branch {branch} passes only when its objective response is correct and no component error is recorded")));
        }
    }
}
