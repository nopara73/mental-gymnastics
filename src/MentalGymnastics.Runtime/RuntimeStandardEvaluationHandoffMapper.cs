using System.Globalization;
using System.Text.RegularExpressions;
using MentalGymnastics.Core;

namespace MentalGymnastics.Runtime;

public sealed record RuntimeScoringMaterial(
    string Kind,
    string Name,
    string Value);

public static class RuntimeStandardEvaluationHandoffMapper
{
    private static readonly char[] AnswerSeparators = ['|', ';', ',', '\n', '\r'];

    public static RuntimeStandardEvaluationHandoffInput Map(
        RuntimeSessionCompletionResult result,
        IEnumerable<RuntimeScoringMaterial> scoringMaterials)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(scoringMaterials);

        var materials = scoringMaterials.ToArray();
        var definition = ExecutableStandardCatalog.Get(result.Branch, result.Level);
        if (definition.Drill != result.Drill)
        {
            throw new ArgumentException(
                "Runtime result drill does not match the executable branch-level standard.",
                nameof(result));
        }

        var standard = definition.EvaluatedStandard;
        var evidence = new Evidence(result, materials);
        var measurements = standard.NumericThresholds
            .Select(threshold => new NumericMeasurement(
                threshold.MeasurementName,
                evidence.Measurement(threshold.MeasurementName)))
            .ToArray();
        var constraints = standard.CriticalConstraints
            .Select(constraint => new CriticalConstraintCheck(
                constraint.Id,
                evidence.Constraint(constraint.Id)))
            .ToArray();

        return new RuntimeStandardEvaluationHandoffInput(
            measurements,
            constraints,
            evidence.OutputComplete,
            evidence.RubricOutcome(standard.RequiredRubric));
    }

    private sealed class Evidence
    {
        private readonly RuntimeSessionCompletionResult result;
        private readonly IReadOnlyList<RuntimeScoringMaterial> materials;
        private readonly IReadOnlyList<RuntimeEvent> events;
        private readonly string allAnswers;
        private readonly CueScore cueScore;
        private readonly PairScore pairScore;
        private readonly ReconstructionScore reconstruction;
        private readonly AuditScore audit;
        private readonly IndexedScore classifications;
        private readonly IndexedScore mappings;
        private readonly ComponentScore components;

        public Evidence(
            RuntimeSessionCompletionResult result,
            IReadOnlyList<RuntimeScoringMaterial> materials)
        {
            this.result = result;
            this.materials = materials;
            events = result.RuntimeEvents;
            allAnswers = string.Join(
                Environment.NewLine,
                events
                    .Where(runtimeEvent => runtimeEvent.Kind == RuntimeEventKind.AnswerSubmitted)
                    .Select(runtimeEvent => Fact(runtimeEvent, "answer_reference"))
                    .Where(answer => !string.IsNullOrWhiteSpace(answer)));
            cueScore = ScoreCues(events);
            pairScore = ScorePairs(materials, allAnswers);
            reconstruction = ScoreReconstruction(materials, allAnswers);
            audit = ScoreAudit(materials, allAnswers);
            classifications = ScoreIndexed(
                materials,
                "ExpectedClassification",
                allAnswers,
                ClassificationToken);
            mappings = ScoreIndexed(
                materials,
                "ExpectedMapping",
                allAnswers,
                MappingToken);
            components = ScoreComponents(materials, allAnswers, ErrorCount(events));
        }

        public bool OutputComplete =>
            result.CompletionStatus == RuntimeSessionCompletionStatus.Completed &&
            RequiredOutputPresent();

        public decimal Measurement(string name)
        {
            return name switch
            {
                TrainingStandardMeasurements.ActiveDurationSeconds => ActiveDurationSeconds(),
                TrainingStandardMeasurements.SetCount => WorkingSetCount(),
                TrainingStandardMeasurements.MarkedDriftCount => EventCount(RuntimeEventKind.DriftMarked),
                TrainingStandardMeasurements.UnreturnedDriftCount => UnreturnedDriftCount(),
                TrainingStandardMeasurements.LateReturnCount => FactCount("return_timing_outcome", "late"),
                TrainingStandardMeasurements.AverageReturnSeconds => AverageDurationFact("recovery_time"),
                TrainingStandardMeasurements.TargetSubstitutionCount => FactCount("error_kind", "target_substitution"),
                TrainingStandardMeasurements.DistractorResponseCount => cueScore.NoResponseCueResponses,
                TrainingStandardMeasurements.CorrectResponsePercent => cueScore.RequiredAccuracyPercent,
                TrainingStandardMeasurements.ValidCueAccuracyPercent => cueScore.RequiredAccuracyPercent,
                TrainingStandardMeasurements.InvalidCueInhibitionPercent => cueScore.InhibitionAccuracyPercent,
                TrainingStandardMeasurements.AnticipatoryResponseCount => FactCount("error_kind", "anticipatory_switch"),
                TrainingStandardMeasurements.UnrecoveredErrorCount => cueScore.UnrecoveredErrorCount,
                TrainingStandardMeasurements.ExactItemCount => reconstruction.ExactCount,
                TrainingStandardMeasurements.ReconstructionAccuracyPercent => reconstruction.AccuracyPercent,
                TrainingStandardMeasurements.InventedItemCount => reconstruction.InventedCount,
                TrainingStandardMeasurements.TransformAccuracyPercent => reconstruction.AccuracyPercent,
                TrainingStandardMeasurements.RuleExplanationCorrect => RuleExplanationCorrect() ? 1 : 0,
                TrainingStandardMeasurements.DelaySeconds => DelaySeconds(),
                TrainingStandardMeasurements.HiddenNoteCount => FactCount("error_kind", "hidden_intermediate_note"),
                TrainingStandardMeasurements.CriticalOmissionCount => reconstruction.MissingCount,
                TrainingStandardMeasurements.PrematureResponseCount =>
                    FactCount("error_kind", "premature_response") + cueScore.NoResponseCueResponses,
                TrainingStandardMeasurements.ExceptionStatementPercent =>
                    HasMaterial("ExceptionDefinition") ? 100 : 0,
                TrainingStandardMeasurements.UnmarkedRuleDriftCount => FactCount("error_kind", "rule_drift"),
                TrainingStandardMeasurements.MaximumCorrectionDistance => MaximumCorrectionDistance(),
                TrainingStandardMeasurements.AccuracyPercent => AccuracyPercent(),
                TrainingStandardMeasurements.ComparisonCount => pairScore.Total,
                TrainingStandardMeasurements.UnmarkedGuessCount => FactCount("error_kind", "unmarked_guess"),
                TrainingStandardMeasurements.FalsePositiveCount => pairScore.FalsePositiveCount,
                TrainingStandardMeasurements.FalseNegativeCount => pairScore.FalseNegativeCount,
                TrainingStandardMeasurements.SeededErrorDetectionPercent => audit.DetectionPercent,
                TrainingStandardMeasurements.FalseCorrectionCount => audit.FalseCorrectionCount,
                TrainingStandardMeasurements.CriticalErrorDetectionPercent => audit.CriticalDetectionPercent,
                TrainingStandardMeasurements.NoncriticalErrorDetectionPercent => audit.NoncriticalDetectionPercent,
                TrainingStandardMeasurements.UnseenClassificationAccuracyPercent => classifications.AccuracyPercent,
                TrainingStandardMeasurements.RuleChangedAfterFeedbackCount => FactCount("error_kind", "rule_changed_after_feedback"),
                TrainingStandardMeasurements.RelationPreservationPercent => mappings.AccuracyPercent,
                TrainingStandardMeasurements.SurfaceOnlyMappingCount => SurfaceOnlyMappingCount(),
                TrainingStandardMeasurements.SourceStandardPassed => SourceStandardPassed() ? 1 : 0,
                TrainingStandardMeasurements.CriticalConstraintBreachCount => CriticalConstraintBreachCount(),
                TrainingStandardMeasurements.UnmarkedUncertaintyCount => UncertaintyMarked() ? 0 : 1,
                TrainingStandardMeasurements.RecoverySeconds => RecoverySeconds(),
                TrainingStandardMeasurements.StandardLoweringCount => FactCount("error_kind", "standard_lowering"),
                TrainingStandardMeasurements.ComponentCount => components.Total,
                TrainingStandardMeasurements.ComponentPassPercent => components.PassPercent,
                TrainingStandardMeasurements.ComponentEvidencePercent => components.EvidencePercent,
                TrainingStandardMeasurements.BottleneckComponentPassed => components.AllPassed ? 1 : 0,
                TrainingStandardMeasurements.AdvancedDemandActive => AdvancedDemandActive() ? 1 : 0,
                TrainingStandardMeasurements.DelayedArtifactComplete => HasAnswerInPhase(RuntimeSessionPhaseKind.ReconstructionInput) ? 1 : 0,
                TrainingStandardMeasurements.CompositePassed => components.AllPassed ? 1 : 0,
                TrainingStandardMeasurements.AuditPassed => AuditPassed() ? 1 : 0,
                TrainingStandardMeasurements.ReconstructionPassed => ReconstructionPassed() ? 1 : 0,
                TrainingStandardMeasurements.PressureRuleIntact => StandardNotLowered() ? 1 : 0,
                _ => 0,
            };
        }

        public bool Constraint(string id)
        {
            return id switch
            {
                TrainingStandardConstraints.TargetStatedBeforeSet =>
                    HasMaterial("TargetStatement") && HasStartedWork(),
                TrainingStandardConstraints.TargetUnchanged =>
                    FactCount("error_kind", "target_substitution") == 0,
                TrainingStandardConstraints.DriftMarked => HasMaterial("DriftMarkingEvidenceShape"),
                TrainingStandardConstraints.SwitchOnlyOnCue =>
                    FactCount("error_kind", "anticipatory_switch") == 0 && cueScore.NoResponseCueResponses == 0,
                TrainingStandardConstraints.NoRereading => FactCount("error_kind", "reread_after_encode") == 0,
                TrainingStandardConstraints.NoIntermediateNotes => FactCount("error_kind", "hidden_intermediate_note") == 0,
                TrainingStandardConstraints.RuleStatedBeforeSet =>
                    HasMaterial("RuleStatement") && HasStartedWork(),
                TrainingStandardConstraints.ExceptionsStatedBeforeSet => HasMaterial("ExceptionDefinition"),
                TrainingStandardConstraints.GuessesMarked => FactCount("error_kind", "unmarked_guess") == 0,
                TrainingStandardConstraints.OriginalOutputLocked =>
                    HasMaterial("LockedOriginalOutput") && FactCount("error_kind", "original_output_edit") == 0,
                TrainingStandardConstraints.RuleStatedBeforeUnseen => RuleWasSubmittedBeforeUnseenTest(),
                TrainingStandardConstraints.RelationsNamed => RelationsWereNamedBeforeMapping(),
                TrainingStandardConstraints.SourceStandardVisible => HasMaterial("SourceBranchStandard"),
                TrainingStandardConstraints.StandardNotLowered => StandardNotLowered(),
                TrainingStandardConstraints.FullRestartProhibited => FactCount("error_kind", "full_restart") == 0,
                TrainingStandardConstraints.BranchEvidenceSeparated => components.EvidencePercent >= 100,
                TrainingStandardConstraints.UncertaintyMarked => UncertaintyMarked(),
                TrainingStandardConstraints.PredictionTested => ContainsAny(allAnswers, "predict", "prediction", "test result"),
                TrainingStandardConstraints.CriticalAssumptionsNamed => ContainsAny(allAnswers, "assumption", "depends on", "limit"),
                _ => false,
            };
        }

        public RubricOutcome? RubricOutcome(RubricOutcome? required)
        {
            if (!required.HasValue)
            {
                return null;
            }

            var passing = result.Branch switch
            {
                BranchCode.WM => reconstruction.AccuracyPercent >= 80 && reconstruction.InventedCount == 0,
                BranchCode.DE => audit.CriticalDetectionPercent >= 100 && audit.FalseCorrectionCount <= 2,
                BranchCode.CO when result.Drill == DrillId.CO1RuleExtraction =>
                    classifications.AccuracyPercent >= 82 && RuleWasSubmittedBeforeUnseenTest(),
                BranchCode.CO => mappings.AccuracyPercent >= 80 && SurfaceOnlyMappingCount() == 0,
                BranchCode.TI => components.AllPassed && AuditPassed() && ReconstructionPassed(),
                _ => false,
            };

            return passing
                ? MentalGymnastics.Core.RubricOutcome.Pass
                : MentalGymnastics.Core.RubricOutcome.Fail;
        }

        private bool RequiredOutputPresent()
        {
            if (result.Branch == BranchCode.AI && result.SessionDefinition.SourceDrill is { } sourceDrill)
            {
                return sourceDrill == DrillId.FH2DistractorHold
                    ? HasStartedWork() && UnreturnedDriftCount() == 0
                    : cueScore.Total > 0 && cueScore.AllCuesResolved;
            }

            if (result.Drill is DrillId.FH1TargetHold or DrillId.FH2DistractorHold)
            {
                return HasStartedWork() && UnreturnedDriftCount() == 0;
            }

            if (result.Drill is DrillId.FS1CueSwitch or DrillId.FS2InvalidCueFilter or
                DrillId.IR1GoNoGoRule or DrillId.IR2ExceptionRule)
            {
                return cueScore.Total > 0 && cueScore.AllCuesResolved;
            }

            if (result.Drill == DrillId.TI2GlobalReviewTask)
            {
                return HasAnswerInPhase(RuntimeSessionPhaseKind.ActiveWork) &&
                    HasAnswerInPhase(RuntimeSessionPhaseKind.Audit) &&
                    HasAnswerInPhase(RuntimeSessionPhaseKind.ReconstructionInput);
            }

            return !string.IsNullOrWhiteSpace(allAnswers);
        }

        private decimal ActiveDurationSeconds()
        {
            return Convert.ToDecimal(
                result.PhaseHistory
                    .Where(phase => phase.Definition.Kind is
                        RuntimeSessionPhaseKind.ActiveWork or
                        RuntimeSessionPhaseKind.CueResponse or
                        RuntimeSessionPhaseKind.Recovery)
                    .Sum(phase => phase.ActualDuration.Value.TotalSeconds),
                CultureInfo.InvariantCulture);
        }

        private decimal DelaySeconds()
        {
            return Convert.ToDecimal(
                result.PhaseHistory
                    .Where(phase => phase.Definition.Kind == RuntimeSessionPhaseKind.DelayWindow)
                    .Sum(phase => phase.ActualDuration.Value.TotalSeconds),
                CultureInfo.InvariantCulture);
        }

        private decimal WorkingSetCount()
        {
            var count = result.PhaseHistory.Count(phase => phase.Definition.Kind is
                RuntimeSessionPhaseKind.ActiveWork or RuntimeSessionPhaseKind.CueResponse);
            return Math.Max(count, HasStartedWork() ? 1 : 0);
        }

        private decimal AccuracyPercent()
        {
            return result.Drill switch
            {
                DrillId.IR1GoNoGoRule or DrillId.IR2ExceptionRule => cueScore.TotalAccuracyPercent,
                DrillId.DE1PairDiscrimination => pairScore.AccuracyPercent,
                _ => Math.Max(reconstruction.AccuracyPercent, cueScore.TotalAccuracyPercent),
            };
        }

        private decimal UnreturnedDriftCount()
        {
            var returned = events
                .Where(runtimeEvent => runtimeEvent.Kind == RuntimeEventKind.RecoveryCompleted)
                .Select(runtimeEvent => Fact(runtimeEvent, "drift_id"))
                .Where(value => value is not null)
                .ToHashSet(StringComparer.Ordinal);
            return events.Count(runtimeEvent =>
                runtimeEvent.Kind == RuntimeEventKind.DriftMarked &&
                (Fact(runtimeEvent, "drift_id") is not { } id || !returned.Contains(id)));
        }

        private decimal AverageDurationFact(string factName)
        {
            var durations = events
                .Select(runtimeEvent => Fact(runtimeEvent, factName))
                .Where(value => value is not null)
                .Select(value => TimeSpan.TryParseExact(value, "c", CultureInfo.InvariantCulture, out var parsed)
                    ? parsed.TotalSeconds
                    : 0)
                .ToArray();
            return durations.Length == 0
                ? 0
                : Convert.ToDecimal(durations.Average(), CultureInfo.InvariantCulture);
        }

        private decimal MaximumCorrectionDistance()
        {
            var errorIndices = events
                .Select((runtimeEvent, index) => (runtimeEvent, index))
                .Where(item => item.runtimeEvent.Kind == RuntimeEventKind.ErrorRecorded)
                .Select(item => item.index)
                .ToArray();
            if (errorIndices.Length == 0)
            {
                return 0;
            }

            var maximum = 0;
            foreach (var errorIndex in errorIndices)
            {
                var correctionIndex = events
                    .Select((runtimeEvent, index) => (runtimeEvent, index))
                    .FirstOrDefault(item =>
                        item.index > errorIndex && item.runtimeEvent.Kind == RuntimeEventKind.CorrectionSubmitted)
                    .index;
                if (correctionIndex == 0)
                {
                    return decimal.MaxValue;
                }

                var distance = events
                    .Skip(errorIndex + 1)
                    .Take(correctionIndex - errorIndex)
                    .Count(runtimeEvent => runtimeEvent.Kind is
                        RuntimeEventKind.CueEmitted or RuntimeEventKind.AnswerSubmitted);
                maximum = Math.Max(maximum, distance);
            }

            return maximum;
        }

        private bool RuleExplanationCorrect()
        {
            var rule = materials.FirstOrDefault(material => material.Kind == "TransformRule")?.Value;
            return !string.IsNullOrWhiteSpace(rule) && SignificantTokens(rule).Count(token =>
                Normalize(allAnswers).Contains(token, StringComparison.Ordinal)) >= 2;
        }

        private decimal SurfaceOnlyMappingCount()
        {
            var normalized = Normalize(allAnswers);
            return materials
                .Where(material => material.Kind == "SurfaceLure")
                .Count(material => TrailingNumber(material.Name) is { } index && Regex.IsMatch(
                    normalized,
                    $@"\bsurface\s*-?\s*lure\s*-?\s*{index}\s*=\s*(use|used|accept|accepted|match|matched)\b",
                    RegexOptions.IgnoreCase));
        }

        private bool SourceStandardPassed()
        {
            return OutputComplete &&
                ErrorCount(events) == 0 &&
                cueScore.IncorrectCount == 0 &&
                UnreturnedDriftCount() == 0 &&
                FactCount("error_kind", "target_substitution") == 0;
        }

        private decimal CriticalConstraintBreachCount()
        {
            return events.Count(runtimeEvent => runtimeEvent.Facts.Any(fact =>
                fact.Name == "failed_constraint" ||
                fact.Name == "critical_constraint_breach" ||
                fact.Name == "standard_lowering_attempt"));
        }

        private bool UncertaintyMarked()
        {
            return FactCount("error_kind", "unmarked_uncertainty") == 0;
        }

        private decimal RecoverySeconds()
        {
            var disruption = events.LastOrDefault(runtimeEvent =>
                runtimeEvent.Kind == RuntimeEventKind.CueResponseSubmitted &&
                Fact(runtimeEvent, "cue_kind") == "interruption");
            var value = disruption is null ? null : Fact(disruption, "response_time");
            return value is not null && TimeSpan.TryParseExact(value, "c", CultureInfo.InvariantCulture, out var parsed)
                ? Convert.ToDecimal(parsed.TotalSeconds, CultureInfo.InvariantCulture)
                : decimal.MaxValue;
        }

        private bool AdvancedDemandActive()
        {
            return materials.Any(material => material.Kind == "ComponentPayload" &&
                (material.Value.Contains("branch CO", StringComparison.Ordinal) ||
                    material.Value.Contains("branch AI", StringComparison.Ordinal)));
        }

        private bool AuditPassed()
        {
            if (result.Drill == DrillId.TI2GlobalReviewTask)
            {
                return HasAnswerInPhase(RuntimeSessionPhaseKind.Audit) && ErrorCount(events) == 0;
            }

            if (result.Drill == DrillId.CO2StructureMapping)
            {
                var expected = materials.FirstOrDefault(material =>
                    material.Kind == "ExpectedFinding" &&
                    string.Equals(material.Name, "model-audit-key", StringComparison.Ordinal));
                var auditAnswer = AnswersForPhase(RuntimeSessionPhaseKind.Audit);
                return expected is not null &&
                    SplitAnswer(expected.Value).All(token =>
                        Normalize(auditAnswer).Contains(token, StringComparison.Ordinal)) &&
                    ErrorCount(events) == 0;
            }

            return audit.CriticalDetectionPercent >= 100 && audit.FalseCorrectionCount <= 2;
        }

        private bool ReconstructionPassed()
        {
            return result.Drill == DrillId.TI2GlobalReviewTask
                ? HasAnswerInPhase(RuntimeSessionPhaseKind.ReconstructionInput) && ErrorCount(events) == 0
                : reconstruction.AccuracyPercent >= 80 && reconstruction.InventedCount == 0;
        }

        private bool StandardNotLowered()
        {
            return FactCount("error_kind", "standard_lowering") == 0 &&
                FactCount("standard_lowering_attempt", "true") == 0;
        }

        private bool RuleWasSubmittedBeforeUnseenTest()
        {
            var ruleSubmission = events.FirstOrDefault(runtimeEvent =>
                runtimeEvent.Kind == RuntimeEventKind.AnswerSubmitted &&
                (runtimeEvent.PhaseKind?.ToString().Contains("Rule", StringComparison.OrdinalIgnoreCase) == true ||
                    Fact(runtimeEvent, "answer_id")?.Contains("rule", StringComparison.OrdinalIgnoreCase) == true));
            var unseenStart = events.FirstOrDefault(runtimeEvent =>
                runtimeEvent.Kind == RuntimeEventKind.PhaseStarted &&
                runtimeEvent.PhaseId?.Contains("unseen", StringComparison.OrdinalIgnoreCase) == true);

            return ruleSubmission is not null &&
                (unseenStart is null || ruleSubmission.SequenceNumber < unseenStart.SequenceNumber);
        }

        private bool RelationsWereNamedBeforeMapping()
        {
            var relationSubmission = events.FirstOrDefault(runtimeEvent =>
                runtimeEvent.Kind == RuntimeEventKind.AnswerSubmitted &&
                (runtimeEvent.PhaseKind?.ToString().Contains("Relation", StringComparison.OrdinalIgnoreCase) == true ||
                    Fact(runtimeEvent, "answer_id")?.Contains("relation", StringComparison.OrdinalIgnoreCase) == true));
            var mappingStart = events.FirstOrDefault(runtimeEvent =>
                runtimeEvent.Kind == RuntimeEventKind.PhaseStarted &&
                runtimeEvent.PhaseId?.Contains("mapping", StringComparison.OrdinalIgnoreCase) == true);

            return relationSubmission is not null &&
                (mappingStart is null || relationSubmission.SequenceNumber < mappingStart.SequenceNumber);
        }

        private bool HasStartedWork()
        {
            return events.Any(runtimeEvent =>
                runtimeEvent.Kind == RuntimeEventKind.PhaseStarted &&
                runtimeEvent.PhaseKind is not null and not RuntimeSessionPhaseKind.InstructionPrep);
        }

        private bool HasAnswerInPhase(RuntimeSessionPhaseKind phase)
        {
            return events.Any(runtimeEvent =>
                runtimeEvent.Kind == RuntimeEventKind.AnswerSubmitted && runtimeEvent.PhaseKind == phase);
        }

        private string AnswersForPhase(RuntimeSessionPhaseKind phase)
        {
            return string.Join(
                Environment.NewLine,
                events
                    .Where(runtimeEvent =>
                        runtimeEvent.Kind == RuntimeEventKind.AnswerSubmitted &&
                        runtimeEvent.PhaseKind == phase)
                    .Select(runtimeEvent => Fact(runtimeEvent, "answer_reference"))
                    .Where(answer => !string.IsNullOrWhiteSpace(answer)));
        }

        private bool HasMaterial(string kind) => materials.Any(material => material.Kind == kind);

        private decimal EventCount(RuntimeEventKind kind) => events.Count(runtimeEvent => runtimeEvent.Kind == kind);

        private decimal FactCount(string name, string value)
        {
            return events.Count(runtimeEvent => runtimeEvent.Facts.Any(fact =>
                string.Equals(fact.Name, name, StringComparison.Ordinal) &&
                string.Equals(fact.Value, value, StringComparison.Ordinal)));
        }
    }

    private sealed record CueScore(
        int Total,
        int RequiredCount,
        int RequiredCorrect,
        int InhibitionCount,
        int InhibitionCorrect,
        int NoResponseCueResponses,
        int IncorrectCount,
        int UnrecoveredErrorCount,
        bool AllCuesResolved)
    {
        public decimal RequiredAccuracyPercent => Percent(RequiredCorrect, RequiredCount);

        public decimal InhibitionAccuracyPercent => Percent(InhibitionCorrect, InhibitionCount);

        public decimal TotalAccuracyPercent => Percent(RequiredCorrect + InhibitionCorrect, Total);
    }

    private sealed record PairScore(
        int Total,
        int Correct,
        int FalsePositiveCount,
        int FalseNegativeCount)
    {
        public decimal AccuracyPercent => Percent(Correct, Total);
    }

    private sealed record ReconstructionScore(
        int ExpectedCount,
        int ExactCount,
        int InventedCount)
    {
        public int MissingCount => Math.Max(0, ExpectedCount - ExactCount);

        public decimal AccuracyPercent => Percent(ExactCount, ExpectedCount);
    }

    private sealed record AuditScore(
        int ExpectedCount,
        int FoundCount,
        int CriticalCount,
        int CriticalFound,
        int NoncriticalCount,
        int NoncriticalFound,
        int FalseCorrectionCount)
    {
        public decimal DetectionPercent => Percent(FoundCount, ExpectedCount);

        public decimal CriticalDetectionPercent => Percent(CriticalFound, CriticalCount);

        public decimal NoncriticalDetectionPercent => Percent(NoncriticalFound, NoncriticalCount);
    }

    private sealed record IndexedScore(int Total, int Correct)
    {
        public decimal AccuracyPercent => Percent(Correct, Total);
    }

    private sealed record ComponentScore(int Total, int WithEvidence, int Passed)
    {
        public decimal EvidencePercent => Percent(WithEvidence, Total);

        public decimal PassPercent => Percent(Passed, Total);

        public bool AllPassed => Total > 0 && Passed == Total;
    }

    private static CueScore ScoreCues(IReadOnlyList<RuntimeEvent> events)
    {
        var emitted = events
            .Where(runtimeEvent => runtimeEvent.Kind == RuntimeEventKind.CueEmitted)
            .Select(runtimeEvent => new
            {
                Id = Fact(runtimeEvent, "cue_id") ?? string.Empty,
                Expectation = Fact(runtimeEvent, "response_expectation") ?? string.Empty,
            })
            .Where(cue => cue.Id.Length > 0)
            .ToArray();
        var responses = events
            .Where(runtimeEvent => runtimeEvent.Kind == RuntimeEventKind.CueResponseSubmitted)
            .GroupBy(runtimeEvent => Fact(runtimeEvent, "cue_id") ?? string.Empty, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.Ordinal);
        var required = emitted.Where(cue => cue.Expectation == "response_required").ToArray();
        var inhibition = emitted.Where(cue => cue.Expectation == "no_response_expected").ToArray();
        var requiredCorrect = required.Count(cue =>
            responses.TryGetValue(cue.Id, out var response) && Fact(response, "response_outcome") == "correct");
        var inhibitionCorrect = inhibition.Count(cue => !responses.ContainsKey(cue.Id));
        var noResponseCueResponses = inhibition.Length - inhibitionCorrect;
        var incorrect = required.Length - requiredCorrect + noResponseCueResponses;
        var allResolved = emitted.Length > 0 && required.All(cue => responses.ContainsKey(cue.Id));

        return new CueScore(
            emitted.Length,
            required.Length,
            requiredCorrect,
            inhibition.Length,
            inhibitionCorrect,
            noResponseCueResponses,
            incorrect,
            incorrect,
            allResolved);
    }

    private static PairScore ScorePairs(
        IReadOnlyList<RuntimeScoringMaterial> materials,
        string answer)
    {
        var expected = materials.Where(material => material.Kind == "MatchTruth").ToArray();
        var normalized = Normalize(answer);
        var correct = 0;
        var falsePositives = 0;
        var falseNegatives = 0;
        foreach (var material in expected)
        {
            var pairId = Regex.Match(material.Value, @"pair-(\d+)", RegexOptions.IgnoreCase).Groups[1].Value;
            var expectedMatch = material.Value.Contains(": match", StringComparison.OrdinalIgnoreCase);
            var response = Regex.Match(
                normalized,
                $@"pair\s*-?\s*{Regex.Escape(pairId)}\s*=\s*(same|different|match|mismatch)",
                RegexOptions.IgnoreCase).Groups[1].Value;
            var responseMatch = response is "same" or "match";
            if (response.Length > 0 && responseMatch == expectedMatch)
            {
                correct++;
            }
            else if (!expectedMatch && responseMatch)
            {
                falseNegatives++;
            }
            else if (expectedMatch && response.Length > 0 && !responseMatch)
            {
                falsePositives++;
            }
        }

        return new PairScore(expected.Length, correct, falsePositives, falseNegatives);
    }

    private static ReconstructionScore ScoreReconstruction(
        IReadOnlyList<RuntimeScoringMaterial> materials,
        string answer)
    {
        var expectedMaterials = materials.Where(material => material.Kind is
            "ExpectedReconstruction" or "FinalExpectedOutput").ToArray();
        var expected = expectedMaterials
            .SelectMany(material => SplitAnswer(material.Value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var actual = SplitAnswer(answer).Distinct(StringComparer.Ordinal).ToArray();
        var exact = expected.Count(item => actual.Contains(item, StringComparer.Ordinal));
        var invented = actual.Count(item =>
            item.Length > 0 &&
            !expected.Contains(item, StringComparer.Ordinal) &&
            !IsExplanationFragment(item));

        return new ReconstructionScore(expected.Length, exact, invented);
    }

    private static AuditScore ScoreAudit(
        IReadOnlyList<RuntimeScoringMaterial> materials,
        string answer)
    {
        var seeded = materials.Where(material => material.Kind == "SeededError").ToArray();
        var normalized = Normalize(answer);
        var found = 0;
        var criticalCount = 0;
        var criticalFound = 0;
        var noncriticalCount = 0;
        var noncriticalFound = 0;
        foreach (var error in seeded)
        {
            var id = Regex.Match(error.Value, @"seeded error\s+([^:;]+)", RegexOptions.IgnoreCase).Groups[1].Value.Trim();
            var line = Regex.Match(error.Value, @"line\s+(\d+)", RegexOptions.IgnoreCase).Groups[1].Value;
            var isCritical = error.Value.Contains("criticality critical", StringComparison.OrdinalIgnoreCase);
            var isFound = (id.Length > 0 && normalized.Contains(Normalize(id), StringComparison.Ordinal)) ||
                (line.Length > 0 && Regex.IsMatch(normalized, $@"(?:line\s*)?{Regex.Escape(line)}\b"));
            if (isFound)
            {
                found++;
            }

            if (isCritical)
            {
                criticalCount++;
                if (isFound)
                {
                    criticalFound++;
                }
            }
            else
            {
                noncriticalCount++;
                if (isFound)
                {
                    noncriticalFound++;
                }
            }
        }

        var falseCorrections = materials
            .Where(material => material.Kind == "NonErrorDistractor")
            .Count(material => ContainsSignificantPhrase(normalized, material.Value));
        return new AuditScore(
            seeded.Length,
            found,
            criticalCount,
            criticalFound,
            noncriticalCount,
            noncriticalFound,
            falseCorrections);
    }

    private static IndexedScore ScoreIndexed(
        IReadOnlyList<RuntimeScoringMaterial> materials,
        string kind,
        string answer,
        Func<string, string> expectedToken)
    {
        var expected = materials.Where(material => material.Kind == kind).ToArray();
        var normalized = Normalize(answer);
        var correct = expected.Count(material =>
        {
            var index = TrailingNumber(material.Name);
            var token = Normalize(expectedToken(material.Value));
            if (token.Length == 0)
            {
                return false;
            }

            var indexPresent = index is null || Regex.IsMatch(normalized, $@"\b{index.Value}\b");
            return indexPresent && normalized.Contains(token, StringComparison.Ordinal);
        });

        return new IndexedScore(expected.Length, correct);
    }

    private static ComponentScore ScoreComponents(
        IReadOnlyList<RuntimeScoringMaterial> materials,
        string answer,
        int errorCount)
    {
        var components = materials.Where(material => material.Kind == "ComponentPayload").ToArray();
        var withEvidence = 0;
        var correct = 0;
        foreach (var component in components)
        {
            var branch = ComponentBranch(component);
            if (branch.Length == 0)
            {
                continue;
            }

            var actual = ComponentResponse(answer, branch);
            if (actual.Length == 0)
            {
                continue;
            }

            withEvidence++;
            var scoringKey = materials.FirstOrDefault(material =>
                material.Kind == "BranchScoringKey" &&
                string.Equals(ComponentBranch(material), branch, StringComparison.OrdinalIgnoreCase));
            var expected = scoringKey is null ? string.Empty : ExpectedComponentResponse(scoringKey.Value);
            if (expected.Length > 0 &&
                string.Equals(NormalizeComponentResponse(actual), NormalizeComponentResponse(expected), StringComparison.Ordinal))
            {
                correct++;
            }
        }

        var passed = errorCount == 0 ? correct : 0;
        return new ComponentScore(components.Length, withEvidence, passed);
    }

    private static string ComponentBranch(RuntimeScoringMaterial material)
    {
        return Regex.Match(
            material.Value,
            @"(?:component\s+)?branch\s+([A-Z]{2})\b",
            RegexOptions.IgnoreCase).Groups[1].Value.ToUpperInvariant();
    }

    private static string ComponentResponse(string answer, string branch)
    {
        var match = Regex.Match(
            answer,
            $@"(?:^|[|;,\r\n])\s*{Regex.Escape(branch)}\s*=\s*([^|;,\r\n]+)",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }

    private static string ExpectedComponentResponse(string scoringKey)
    {
        return Regex.Match(
            scoringKey,
            @"expected\s+response\s+([^;]+)",
            RegexOptions.IgnoreCase).Groups[1].Value.Trim();
    }

    private static string NormalizeComponentResponse(string value)
    {
        return Regex.Replace(value.Trim().ToUpperInvariant(), @"\s+", string.Empty);
    }

    private static int ErrorCount(IReadOnlyList<RuntimeEvent> events)
    {
        return events.Count(runtimeEvent => runtimeEvent.Kind == RuntimeEventKind.ErrorRecorded);
    }

    private static string ClassificationToken(string value)
    {
        var colon = value.IndexOf(':');
        var semicolon = value.IndexOf(';', colon + 1);
        return colon < 0
            ? value
            : (semicolon < 0 ? value[(colon + 1)..] : value[(colon + 1)..semicolon]).Trim();
    }

    private static string MappingToken(string value)
    {
        var match = Regex.Match(value, @"'([^']+)'", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : value;
    }

    private static IReadOnlyList<string> SplitAnswer(string value)
    {
        return value
            .Split(AnswerSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Normalize)
            .Where(item => item.Length > 0)
            .ToArray();
    }

    private static bool IsExplanationFragment(string value)
    {
        return value.Contains("rule", StringComparison.Ordinal) ||
            value.Contains("because", StringComparison.Ordinal) ||
            value.Contains("then", StringComparison.Ordinal);
    }

    private static int? TrailingNumber(string value)
    {
        var match = Regex.Match(value, @"(\d+)$");
        return match.Success && int.TryParse(match.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            ? number
            : null;
    }

    private static bool ContainsAny(string value, params string[] candidates)
    {
        var normalized = Normalize(value);
        return candidates.Any(candidate => normalized.Contains(Normalize(candidate), StringComparison.Ordinal));
    }

    private static bool ContainsSignificantPhrase(string value, string phrase)
    {
        var normalized = Normalize(value);
        var tokens = SignificantTokens(phrase);
        return tokens.Count >= 2 && tokens.Count(token => normalized.Contains(token, StringComparison.Ordinal)) >= 2;
    }

    private static IReadOnlyList<string> SignificantTokens(string value)
    {
        return Regex.Matches(Normalize(value), @"[a-z0-9]+")
            .Cast<Match>()
            .Select(match => match.Value)
            .Where(token => token.Length >= 4)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string Normalize(string value)
    {
        return Regex.Replace(value.Trim().ToLowerInvariant(), @"\s+", " ");
    }

    private static string? Fact(RuntimeEvent runtimeEvent, string name)
    {
        return runtimeEvent.Facts.LastOrDefault(fact =>
            string.Equals(fact.Name, name, StringComparison.Ordinal))?.Value;
    }

    private static decimal Percent(int numerator, int denominator)
    {
        return denominator <= 0
            ? 0
            : Math.Round((decimal)numerator * 100m / denominator, 2, MidpointRounding.AwayFromZero);
    }
}
