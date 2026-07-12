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
            var activeAnswers = RuntimeStandardEvaluationHandoffMapper.AnswersForPhase(
                events,
                RuntimeSessionPhaseKind.ActiveWork);
            var reconstructionAnswers = RuntimeStandardEvaluationHandoffMapper.AnswersForPhase(
                events,
                RuntimeSessionPhaseKind.ReconstructionInput);
            var auditAnswers = RuntimeStandardEvaluationHandoffMapper.AnswersForPhase(
                events,
                RuntimeSessionPhaseKind.Audit);
            cueScore = ScoreCues(events);
            pairScore = ScorePairs(materials, activeAnswers);
            reconstruction = ScoreReconstruction(result.Drill, materials, reconstructionAnswers);
            audit = ScoreAudit(materials, auditAnswers);
            classifications = ScoreIndexed(
                materials,
                "ExpectedClassification",
                reconstructionAnswers,
                ClassificationToken);
            mappings = ScoreMappings(materials, reconstructionAnswers);
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
                TrainingStandardMeasurements.ExceptionStatementPercent => ExceptionStatementPercent(),
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
                TrainingStandardConstraints.RuleStatedBeforeSet => RuleWasDeclaredBeforeCueSet(),
                TrainingStandardConstraints.ExceptionsStatedBeforeSet =>
                    RuleWasDeclaredBeforeCueSet() && ExceptionStatementPercent() >= 100,
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
                TrainingStandardConstraints.PredictionTested => PredictionWasTested(),
                TrainingStandardConstraints.CriticalAssumptionsNamed => CriticalAssumptionWasNamed(),
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
            var componentOutputPresent = !HasMaterial("ComponentPayload") ||
                HasAnswerInPhase(RuntimeSessionPhaseKind.ReconstructionInput);
            if (result.Branch == BranchCode.AI && result.SessionDefinition.SourceDrill is { } sourceDrill)
            {
                var sourceOutputPresent = sourceDrill == DrillId.FH2DistractorHold
                    ? HasStartedWork() && UnreturnedDriftCount() == 0
                    : cueScore.Total > 0 && cueScore.AllCuesResolved;
                return sourceOutputPresent && componentOutputPresent;
            }

            if (result.Drill is DrillId.FH1TargetHold or DrillId.FH2DistractorHold)
            {
                return HasStartedWork() && UnreturnedDriftCount() == 0 && componentOutputPresent;
            }

            if (result.Drill is DrillId.FS1CueSwitch or DrillId.FS2InvalidCueFilter or
                DrillId.IR1GoNoGoRule or DrillId.IR2ExceptionRule)
            {
                return cueScore.Total > 0 && cueScore.AllCuesResolved && componentOutputPresent;
            }

            if (result.Drill == DrillId.TI2GlobalReviewTask)
            {
                return HasAnswerInPhase(RuntimeSessionPhaseKind.ActiveWork) &&
                    HasAnswerInPhase(RuntimeSessionPhaseKind.Audit) &&
                    HasAnswerInPhase(RuntimeSessionPhaseKind.ReconstructionInput);
            }

            return events.Any(runtimeEvent =>
                runtimeEvent.Kind == RuntimeEventKind.AnswerSubmitted &&
                IsSubstantiveAnswer(Fact(runtimeEvent, "answer_reference")));
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
            var errors = events
                .Select((runtimeEvent, index) => (runtimeEvent, index))
                .Where(item => IsCueFailure(item.runtimeEvent))
                .ToArray();
            if (errors.Length == 0)
            {
                return 0;
            }

            var maximum = 0;
            foreach (var error in errors)
            {
                var cueId = Fact(error.runtimeEvent, "cue_id");
                var correction = events
                    .Select((runtimeEvent, index) => (runtimeEvent, index))
                    .FirstOrDefault(item =>
                        item.index > error.index &&
                        item.runtimeEvent.Kind == RuntimeEventKind.CorrectionSubmitted &&
                        CorrectionMatches(item.runtimeEvent, error.runtimeEvent, cueId));
                if (correction.runtimeEvent is null)
                {
                    return decimal.MaxValue;
                }

                if (int.TryParse(
                        Fact(correction.runtimeEvent, "items_after_error"),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var recordedDistance))
                {
                    maximum = Math.Max(maximum, recordedDistance);
                    continue;
                }

                var distance = events
                    .Skip(error.index + 1)
                    .Take(correction.index - error.index)
                    .Count(runtimeEvent => runtimeEvent.Kind is
                        RuntimeEventKind.CueEmitted or RuntimeEventKind.AnswerSubmitted);
                maximum = Math.Max(maximum, distance);
            }

            return maximum;
        }

        private static bool IsCueFailure(RuntimeEvent runtimeEvent)
        {
            return runtimeEvent.Kind is RuntimeEventKind.CueResponseSubmitted or RuntimeEventKind.ErrorRecorded &&
                Fact(runtimeEvent, "cue_id") is not null &&
                Fact(runtimeEvent, "response_outcome") is "incorrect" or "late";
        }

        private static bool CorrectionMatches(
            RuntimeEvent correction,
            RuntimeEvent failedEvent,
            string? cueId)
        {
            var sequence = Fact(correction, "corrected_event_sequence");
            if (sequence is not null &&
                string.Equals(
                    sequence,
                    failedEvent.SequenceNumber.ToString(CultureInfo.InvariantCulture),
                    StringComparison.Ordinal))
            {
                return Fact(correction, "correction_outcome") == "correct";
            }

            return cueId is not null &&
                string.Equals(Fact(correction, "source_cue_id"), cueId, StringComparison.Ordinal) &&
                Fact(correction, "correction_within_required_items") == "true";
        }

        private bool RuleExplanationCorrect()
        {
            var explanation = AssignmentResponse(allAnswers, "RULE");
            var operations = materials
                .Where(material => material.Kind == "OperationStep")
                .ToArray();
            return explanation.Length > 0 && operations.Length > 0 && operations.All(operation =>
                ContainsSignificantPhrase(explanation, operation.Value));
        }

        private bool RuleWasDeclaredBeforeCueSet()
        {
            var declaration = events.FirstOrDefault(runtimeEvent =>
                runtimeEvent.Kind == RuntimeEventKind.AnswerSubmitted &&
                string.Equals(runtimeEvent.PhaseId, "rule-declaration", StringComparison.Ordinal));
            var cueSet = events.FirstOrDefault(runtimeEvent =>
                runtimeEvent.Kind == RuntimeEventKind.PhaseStarted &&
                runtimeEvent.PhaseKind == RuntimeSessionPhaseKind.CueResponse);
            if (declaration is null || cueSet is null || declaration.SequenceNumber >= cueSet.SequenceNumber)
            {
                return false;
            }

            var declaredRule = Fact(declaration, "answer_reference") ?? string.Empty;
            var expectedRule = materials.FirstOrDefault(material => material.Kind == "RuleStatement")?.Value;
            if (string.IsNullOrWhiteSpace(expectedRule))
            {
                return false;
            }

            return RuleDeclarationMatches(expectedRule, declaredRule);
        }

        private static bool RuleDeclarationMatches(string expectedRule, string declaredRule)
        {
            var expected = Normalize(expectedRule);
            var declared = Normalize(declaredRule);
            if (expected.Contains("go cues", StringComparison.Ordinal) &&
                expected.Contains("no-go", StringComparison.Ordinal))
            {
                return ContainsRuleToken(declared, "respond") &&
                    ContainsRuleToken(declared, "go") &&
                    ContainsRuleToken(declared, "withhold") &&
                    ContainsRuleToken(declared, "no-go");
            }

            if (expected.Contains("round symbols", StringComparison.Ordinal) &&
                expected.Contains("angular symbols", StringComparison.Ordinal))
            {
                return ContainsRuleToken(declared, "tap") &&
                    ContainsRuleToken(declared, "round") &&
                    ContainsRuleToken(declared, "withhold") &&
                    ContainsRuleToken(declared, "angular");
            }

            var expectedTokens = SignificantTokens(expectedRule);
            return expectedTokens.Count >= 4 && expectedTokens.Count(token =>
                ContainsRuleToken(declared, token)) >= 4;
        }

        private static bool ContainsRuleToken(string declaration, string token)
        {
            return Regex.IsMatch(
                declaration,
                $@"\b{Regex.Escape(token)}\b",
                RegexOptions.IgnoreCase);
        }

        private decimal ExceptionStatementPercent()
        {
            var exceptions = materials
                .Where(material => material.Kind == "ExceptionDefinition")
                .ToArray();
            if (exceptions.Length == 0)
            {
                return 100;
            }

            var declaration = events.FirstOrDefault(runtimeEvent =>
                runtimeEvent.Kind == RuntimeEventKind.AnswerSubmitted &&
                string.Equals(runtimeEvent.PhaseId, "rule-declaration", StringComparison.Ordinal));
            var answer = declaration is null
                ? string.Empty
                : Normalize(Fact(declaration, "answer_reference") ?? string.Empty);
            var stated = exceptions.Count(exception =>
            {
                var symbol = Regex.Match(
                    exception.Value,
                    @"exception\s+\d+\s*:\s*([^\s]+)",
                    RegexOptions.IgnoreCase).Groups[1].Value;
                var action = Regex.Match(
                    exception.Value,
                    @"->\s*([a-z-]+)",
                    RegexOptions.IgnoreCase).Groups[1].Value;
                return symbol.Length > 0 && action.Length > 0 &&
                    answer.Contains(Normalize(symbol), StringComparison.Ordinal) &&
                    answer.Contains(Normalize(action), StringComparison.Ordinal);
            });
            return Percent(stated, exceptions.Length);
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
            var sourceDrill = result.SessionDefinition.SourceDrill ?? result.Drill;
            var sourceBranch = sourceDrill switch
            {
                DrillId.FH1TargetHold or DrillId.FH2DistractorHold => BranchCode.FH,
                DrillId.FS1CueSwitch or DrillId.FS2InvalidCueFilter => BranchCode.FS,
                DrillId.IR1GoNoGoRule or DrillId.IR2ExceptionRule => BranchCode.IR,
                _ => (BranchCode?)null,
            };
            if (!sourceBranch.HasValue)
            {
                return false;
            }

            var sourceStandard = ExecutableStandardCatalog
                .Get(sourceBranch.Value, GlobalLevelId.L3)
                .EvaluatedStandard;
            var attempt = new StandardEvaluationAttempt(
                sourceStandard.NumericThresholds.Select(threshold =>
                    new NumericMeasurement(threshold.MeasurementName, Measurement(threshold.MeasurementName))),
                sourceStandard.CriticalConstraints.Select(constraint =>
                    new CriticalConstraintCheck(constraint.Id, Constraint(constraint.Id))),
                OutputComplete,
                RubricOutcome(sourceStandard.RequiredRubric));
            return StandardEvaluator.Evaluate(sourceStandard, attempt).Passed;
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
                var expected = materials.FirstOrDefault(material =>
                    material.Kind == "ExpectedFinding" &&
                    string.Equals(material.Name, "global-review-audit-key", StringComparison.Ordinal));
                var expectedTokens = expected is null ? [] : SplitAnswer(expected.Value);
                var actualTokens = SplitAnswer(AnswersForPhase(RuntimeSessionPhaseKind.Audit));
                return expectedTokens.Count > 0 &&
                    actualTokens.Count == expectedTokens.Count &&
                    expectedTokens.All(token => actualTokens.Contains(token, StringComparer.Ordinal)) &&
                    ErrorCount(events) == 0;
            }

            if (result.Drill == DrillId.CO2StructureMapping)
            {
                var expected = materials.FirstOrDefault(material =>
                    material.Kind == "ExpectedFinding" &&
                    string.Equals(material.Name, "model-audit-key", StringComparison.Ordinal));
                var actualTokens = SplitAnswer(AnswersForPhase(RuntimeSessionPhaseKind.Audit));
                return expected is not null &&
                    SplitAnswer(expected.Value).All(token => actualTokens.Contains(token, StringComparer.Ordinal)) &&
                    ErrorCount(events) == 0;
            }

            return audit.CriticalDetectionPercent >= 100 && audit.FalseCorrectionCount <= 2;
        }

        private bool ReconstructionPassed()
        {
            return result.Drill == DrillId.TI2GlobalReviewTask
                ? reconstruction.ExpectedCount > 0 &&
                    reconstruction.AccuracyPercent >= 100 &&
                    reconstruction.InventedCount == 0 &&
                    ErrorCount(events) == 0
                : reconstruction.AccuracyPercent >= 80 && reconstruction.InventedCount == 0;
        }

        private bool StandardNotLowered()
        {
            return FactCount("error_kind", "standard_lowering") == 0 &&
                FactCount("standard_lowering_attempt", "true") == 0;
        }

        private bool PredictionWasTested()
        {
            var auditAnswer = AnswersForPhase(RuntimeSessionPhaseKind.Audit);
            var verdict = Normalize(AssignmentResponse(auditAnswer, "PREDICTION"));
            var test = AssignmentResponse(auditAnswer, "TEST");
            return verdict is "passed" or "failed" && SignificantTokens(test).Count >= 2;
        }

        private bool CriticalAssumptionWasNamed()
        {
            return SignificantTokens(AssignmentResponse(
                AnswersForPhase(RuntimeSessionPhaseKind.Audit),
                "ASSUMPTION")).Count >= 2;
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

            if (ruleSubmission is null ||
                (unseenStart is not null && ruleSubmission.SequenceNumber >= unseenStart.SequenceNumber))
            {
                return false;
            }

            var expectedRule = materials.FirstOrDefault(material => material.Kind == "ExpectedRule")?.Value;
            if (string.IsNullOrWhiteSpace(expectedRule))
            {
                return false;
            }

            var submittedRule = Normalize(Fact(ruleSubmission, "answer_reference") ?? string.Empty);
            var expectedTokens = SignificantTokens(expectedRule);
            var requiredMatches = Math.Min(4, expectedTokens.Count);
            return requiredMatches >= 2 &&
                expectedTokens.Count(token => submittedRule.Contains(token, StringComparison.Ordinal)) >= requiredMatches;
        }

        private bool RelationsWereNamedBeforeMapping()
        {
            var relationSubmission = events.FirstOrDefault(runtimeEvent =>
                runtimeEvent.Kind == RuntimeEventKind.AnswerSubmitted &&
                string.Equals(runtimeEvent.PhaseId, "relation-naming", StringComparison.Ordinal));
            var mappingStart = events.FirstOrDefault(runtimeEvent =>
                runtimeEvent.Kind == RuntimeEventKind.PhaseStarted &&
                string.Equals(runtimeEvent.PhaseId, "mapping-input", StringComparison.Ordinal));

            if (relationSubmission is null || mappingStart is null ||
                relationSubmission.SequenceNumber >= mappingStart.SequenceNumber)
            {
                return false;
            }

            var declaration = Fact(relationSubmission, "answer_reference") ?? string.Empty;
            var expectedSources = materials
                .Where(material => material.Kind == "ExpectedMapping")
                .Select(material => MappingSegment(material.Value, "expected source relation "))
                .Where(value => value.Length > 0)
                .ToArray();
            return expectedSources.Length > 0 && expectedSources.All(source =>
                ContainsSignificantPhrase(declaration, source));
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
                runtimeEvent.Kind == RuntimeEventKind.AnswerSubmitted &&
                runtimeEvent.PhaseKind == phase &&
                IsSubstantiveAnswer(Fact(runtimeEvent, "answer_reference")));
        }

        private string AnswersForPhase(RuntimeSessionPhaseKind phase)
        {
            return RuntimeStandardEvaluationHandoffMapper.AnswersForPhase(events, phase);
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
        var incorrectCueIds = required
            .Where(cue => !responses.TryGetValue(cue.Id, out var response) ||
                Fact(response, "response_outcome") != "correct")
            .Select(cue => cue.Id)
            .Concat(inhibition.Where(cue => responses.ContainsKey(cue.Id)).Select(cue => cue.Id))
            .ToHashSet(StringComparer.Ordinal);
        var recoveredCueIds = events
            .Where(runtimeEvent => runtimeEvent.Kind == RuntimeEventKind.CorrectionSubmitted)
            .SelectMany(runtimeEvent =>
            {
                var correctedCueId = Fact(runtimeEvent, "corrected_cue_id");
                if (correctedCueId is not null && Fact(runtimeEvent, "correction_outcome") == "correct")
                {
                    return new[] { correctedCueId };
                }

                var sourceCueId = Fact(runtimeEvent, "source_cue_id");
                return sourceCueId is not null &&
                    Fact(runtimeEvent, "correction_within_required_items") == "true"
                        ? new[] { sourceCueId }
                        : [];
            })
            .ToHashSet(StringComparer.Ordinal);
        var unrecovered = incorrectCueIds.Count(cueId => !recoveredCueIds.Contains(cueId));
        var allResolved = emitted.Length > 0 && required.All(cue => responses.ContainsKey(cue.Id));

        return new CueScore(
            emitted.Length,
            required.Length,
            requiredCorrect,
            inhibition.Length,
            inhibitionCorrect,
            noResponseCueResponses,
            incorrect,
            unrecovered,
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
                falsePositives++;
            }
            else if (expectedMatch && response.Length > 0 && !responseMatch)
            {
                falseNegatives++;
            }
        }

        return new PairScore(expected.Length, correct, falsePositives, falseNegatives);
    }

    private static ReconstructionScore ScoreReconstruction(
        DrillId drill,
        IReadOnlyList<RuntimeScoringMaterial> materials,
        string answer)
    {
        var expectedMaterials = materials.Where(material => material.Kind is
            "ExpectedReconstruction" or "FinalExpectedOutput").ToArray();
        var expected = expectedMaterials
            .SelectMany(material => SplitAnswer(material.Value))
            .ToArray();
        var resultAssignment = AssignmentResponse(answer, "RESULT");
        var actual = SplitAnswer(resultAssignment.Length > 0 ? resultAssignment : answer).ToArray();
        var exact = drill is DrillId.WM1DelayedReconstruction or DrillId.WM2MentalTransform
            ? Enumerable.Range(0, Math.Min(expected.Length, actual.Length)).Count(index =>
                string.Equals(expected[index], actual[index], StringComparison.Ordinal))
            : expected.Distinct(StringComparer.Ordinal).Count(item =>
                actual.Contains(item, StringComparer.Ordinal));
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
        var submissions = FindingAssignments(answer);
        var matchedSubmissions = new HashSet<int>();
        var found = 0;
        var criticalCount = 0;
        var criticalFound = 0;
        var noncriticalCount = 0;
        var noncriticalFound = 0;
        foreach (var error in seeded)
        {
            var line = Regex.Match(error.Value, @"line\s+(\d+)", RegexOptions.IgnoreCase).Groups[1].Value;
            var type = Regex.Match(error.Value, @"type\s+([^;]+)", RegexOptions.IgnoreCase).Groups[1].Value.Trim();
            var isCritical = error.Value.Contains("criticality critical", StringComparison.OrdinalIgnoreCase);
            var index = TrailingNumber(error.Name);
            var expected = index.HasValue
                ? materials.FirstOrDefault(material =>
                    material.Kind == "ExpectedFinding" && TrailingNumber(material.Name) == index)
                : null;
            var matchedIndex = expected is null
                ? -1
                : Enumerable.Range(0, submissions.Count).FirstOrDefault(
                    submissionIndex => !matchedSubmissions.Contains(submissionIndex) &&
                        FindingMatches(submissions[submissionIndex], line, type, expected.Value),
                    -1);
            var isFound = matchedIndex >= 0;
            if (isFound)
            {
                found++;
                matchedSubmissions.Add(matchedIndex);
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

        var falseCorrections = submissions.Count - matchedSubmissions.Count;
        return new AuditScore(
            seeded.Length,
            found,
            criticalCount,
            criticalFound,
            noncriticalCount,
            noncriticalFound,
            falseCorrections);
    }

    private static IReadOnlyList<string> FindingAssignments(string answer)
    {
        return Regex.Matches(
                answer,
                @"(?:^|;)\s*FINDING-\d+\s*=\s*([^;]+)",
                RegexOptions.IgnoreCase | RegexOptions.Multiline)
            .Cast<Match>()
            .Select(match => match.Groups[1].Value.Trim())
            .Where(value => value.Length > 0)
            .ToArray();
    }

    private static bool FindingMatches(
        string submission,
        string line,
        string type,
        string expectedFinding)
    {
        if (line.Length == 0 ||
            !Regex.IsMatch(
                submission,
                $@"\bline\s*#?\s*{Regex.Escape(line)}\b",
                RegexOptions.IgnoreCase))
        {
            return false;
        }

        var normalized = Normalize(submission);
        var typeTokens = SignificantTokens(type);
        var correctionTokens = AuditCorrectionTokens(expectedFinding);
        return typeTokens.Count > 0 &&
            typeTokens.All(token => ContainsToken(normalized, token)) &&
            correctionTokens.Count > 0 &&
            correctionTokens.All(token => ContainsToken(normalized, token));
    }

    private static IReadOnlyList<string> AuditCorrectionTokens(string expectedFinding)
    {
        var normalized = Normalize(expectedFinding);
        var should = normalized.LastIndexOf(" should ", StringComparison.Ordinal);
        var correction = should >= 0 ? normalized[(should + " should ".Length)..] : normalized;
        var instead = correction.IndexOf(" instead of ", StringComparison.Ordinal);
        if (instead >= 0)
        {
            correction = correction[..instead];
        }

        var ignored = new HashSet<string>(StringComparer.Ordinal)
        {
            "a", "an", "as", "assign", "end", "finding", "line", "mark", "name",
            "report", "say", "should", "the", "to", "with", "code",
        };
        return Regex.Matches(correction, @"[a-z0-9]+")
            .Cast<Match>()
            .Select(match => match.Value)
            .Where(token => !ignored.Contains(token))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static bool ContainsToken(string normalized, string token)
    {
        return Regex.IsMatch(
            normalized,
            $@"\b{Regex.Escape(token)}\b",
            RegexOptions.IgnoreCase);
    }

    private static IndexedScore ScoreIndexed(
        IReadOnlyList<RuntimeScoringMaterial> materials,
        string kind,
        string answer,
        Func<string, string> expectedToken)
    {
        var expected = materials.Where(material => material.Kind == kind).ToArray();
        var correct = expected.Count(material =>
        {
            var index = TrailingNumber(material.Name);
            var token = Normalize(expectedToken(material.Value));
            if (!index.HasValue || token.Length == 0)
            {
                return false;
            }

            var response = Normalize(IndexedResponse(answer, index.Value));
            return string.Equals(response, token, StringComparison.Ordinal);
        });

        return new IndexedScore(expected.Length, correct);
    }

    private static IndexedScore ScoreMappings(
        IReadOnlyList<RuntimeScoringMaterial> materials,
        string answer)
    {
        var expected = materials.Where(material => material.Kind == "ExpectedMapping").ToArray();
        var correct = expected.Count(material =>
        {
            var index = TrailingNumber(material.Name);
            if (!index.HasValue)
            {
                return false;
            }

            var response = IndexedResponse(answer, index.Value);
            var source = MappingSegment(material.Value, "expected source relation ");
            var target = MappingSegment(material.Value, "expected target relation ");
            return response.Length > 0 &&
                source.Length > 0 &&
                target.Length > 0 &&
                ContainsSignificantPhrase(response, source) &&
                ContainsSignificantPhrase(response, target) &&
                ContainsAny(response, "because", "preserve", "relation", "both");
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

    private static string MappingSegment(string value, string marker)
    {
        var start = value.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return string.Empty;
        }

        start += marker.Length;
        var end = value.IndexOf(';', start);
        return (end < 0 ? value[start..] : value[start..end]).Trim().TrimEnd('.');
    }

    private static string IndexedResponse(string answer, int index)
    {
        var match = Regex.Match(
            answer,
            $@"(?:^|[;\r\n])\s*{index.ToString(CultureInfo.InvariantCulture)}\s*=\s*([^;\r\n]+)",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }

    private static string AssignmentResponse(string answer, string key)
    {
        var match = Regex.Match(
            answer,
            $@"(?:^|[;\r\n])\s*{Regex.Escape(key)}\s*=\s*([^;\r\n]+)",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }

    private static string AnswersForPhase(
        IReadOnlyList<RuntimeEvent> events,
        RuntimeSessionPhaseKind phase)
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

    private static IReadOnlyList<string> SplitAnswer(string value)
    {
        return value
            .Split(AnswerSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Normalize)
            .Where(item => item.Length > 0 &&
                !string.Equals(item, Normalize(RuntimeResponseMarkers.Omitted), StringComparison.Ordinal))
            .ToArray();
    }

    private static bool IsSubstantiveAnswer(string? answer)
    {
        return !string.IsNullOrWhiteSpace(answer) &&
            !string.Equals(
                Normalize(answer),
                Normalize(RuntimeResponseMarkers.Omitted),
                StringComparison.Ordinal);
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
