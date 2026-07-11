using MentalGymnastics.Core;
using MentalGymnastics.Persistence;

namespace MentalGymnastics.App;

internal static class PersistedGlobalReviewResultFactory
{
    public static GlobalReviewResult? From(LocalProgramReviewCadenceFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (!facts.LastCompletedReviewPassed.HasValue)
        {
            return null;
        }

        // A failed whole-program review cannot certify any component for TI advancement.
        return new GlobalReviewResult(
            facts.LastCompletedReviewPassed.Value,
            Enum.GetValues<BranchCode>().Select(branch => new GlobalReviewComponentScore(
                branch,
                facts.LastCompletedReviewPassed.Value)));
    }
}
