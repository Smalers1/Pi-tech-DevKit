using System.Collections.Generic;

namespace Pitech.XR.Analytics
{
    // ---------- The grade engine: the canonical reducer (map sec-11.8, RATIFIED 2026-06-26) ----------
    // WS B2.1. Pure function: (LabConfig + captured SessionEventStream) -> GradeResult. NO Unity scene
    // refs, NO Stopwatch (the stream is already portable ms), NO allocation concerns (runs ONCE at
    // session stop, never per-frame). This is the DevKit-CANONICAL reducer (decision 38); the Web Portal
    // (B2.3) is a MIRROR that must compute an identical GradeResult from the same raw (config + events).
    // The equivalence golden fixture (AnalyticsEquivalenceFixture) keeps the two in lockstep.
    //
    // The ratified formula (Objective.cs:14-20):
    //   metric    x_m = clamp01(1 - Penalty_m(rawValue))
    //   analytic  X_A = sum(w.x) / sum(w)       over APPLICABLE metrics
    //   objective X_o = sum(sw.X_A) / sum(sw)   over APPLICABLE analytics feeding o
    //   grade     G   = sum(W.X_o) / sum(W)     over APPLICABLE objectives ("incomplete" if denom 0)
    //   passed_o  = X_o >= target_o             (pass-bar LABEL only, never a divisor)
    //
    // Penalty per kind:
    //   ceiling (StepDuration / TotalDuration): the penaltyWeight of the HIGHEST-threshold band the raw
    //     value crosses. A penalty band with threshold <= 0 is INACTIVE (unset) - so an un-thresholded
    //     duration metric scores 1 (no footgun zeroing). Author sets thresholds (seconds) to activate.
    //   count (Drop / WrongInteraction / Order / Signal): per-occurrence sum of the band penaltyWeight for
    //     each occurrence's DERIVED severity, then clamp01. Threshold is unused for count kinds at launch.
    //     Drop/Wrong/Order match by event kind; Signal is its OWN kind, matched by id (event.signalId ==
    //     metric.id) so authored failures score on a typed SignalMetric, never on an unrelated count metric.
    //
    // Severity derivation (B2.2 Step 5 default table, author-overridable via the band penaltyWeights):
    //   relevant-subject drop -> Error · distractor drop -> Warning · wrong-interaction (distractor) ->
    //   Warning, else Error · out-of-order -> Warning · authored Signal -> Error.
    //
    // Applicability mask: a metric whose step was never entered (step-scoped), or whose bracket never
    // closed (TotalDuration), or whose kind does not match its analytic scope, is MASKED (Applicable =
    // false) and dropped from the parent weighted mean. All-masked at any level propagates up; an
    // all-masked grade is "incomplete" - never 0, never "passed".

    /// <summary>The canonical analytics reducer (map sec-11.8). Stateless; call <see cref="Compute"/>.</summary>
    public static class AnalyticsGradeEngine
    {
        /// <summary>Compute the grade for one Participant attempt from the config + captured stream.
        /// Pure and deterministic - the same inputs always produce the same GradeResult (the cloud
        /// mirror must match). <paramref name="role"/> only stamps the result; gating (whether to grade
        /// at all) is the recorder's job.</summary>
        public static GradeResult Compute(LabConfig config, SessionEventStream stream, SessionRole role)
        {
            var result = new GradeResult { role = role, isComplete = false, grade = 0f };
            if (config == null || stream == null) return result;

            var ctx = new ReduceContext(config, stream);

            float gradeNum = 0f, gradeDen = 0f;

            var objectives = config.objectives;
            if (objectives != null)
            {
                for (int i = 0; i < objectives.Count; i++)
                {
                    Objective o = objectives[i];
                    if (o == null) continue;

                    ObjectiveScoreResult or = ComputeObjective(o, ctx);
                    result.objectives.Add(or);

                    if (or.applicable)
                    {
                        float w = o.weight;
                        gradeNum += w * or.score;
                        gradeDen += w;
                    }
                }
            }

            // "Complete" requires BOTH a closed bracket AND at least one applicable objective. An
            // unclosed bracket (crash/quit) is "incomplete" even if some scene-scoped objective happens
            // to be applicable - never present a grade for a session that never reached SessionStop. The
            // cloud mirror applies the same rule (stream.IsComplete is in the bundled raw events).
            if (gradeDen > 0f && stream.IsComplete)
            {
                result.isComplete = true;
                result.grade = Clamp01(gradeNum / gradeDen);
            }
            // else: masked or unclosed -> "incomplete" (isComplete stays false, grade stays 0).
            return result;
        }

        static ObjectiveScoreResult ComputeObjective(Objective o, ReduceContext ctx)
        {
            var or = new ObjectiveScoreResult
            {
                id = o.id,
                label = o.label,
                target = o.target,
                applicable = false,
                score = 0f,
                passed = false
            };

            float num = 0f, den = 0f;
            if (o.inputs != null)
            {
                for (int i = 0; i < o.inputs.Count; i++)
                {
                    ObjectiveInput input = o.inputs[i];
                    if (input == null) continue;

                    Analytic a = ctx.FindAnalytic(input.analyticId);
                    if (a == null) continue;

                    AnalyticScoreResult ar = ComputeAnalytic(a, ctx);
                    or.analytics.Add(ar);

                    if (ar.applicable)
                    {
                        float sw = input.subWeight;
                        num += sw * ar.score;
                        den += sw;
                    }
                }
            }

            if (den > 0f)
            {
                or.applicable = true;
                or.score = Clamp01(num / den);
                or.passed = or.score >= o.target;
            }
            return or;
        }

        static AnalyticScoreResult ComputeAnalytic(Analytic a, ReduceContext ctx)
        {
            var ar = new AnalyticScoreResult
            {
                id = a.id,
                label = a.label,
                kind = a.Kind,
                applicable = false,
                score = 0f
            };

            string stepGuid = (a is StepAnalytic sa) ? sa.stepGuid : null;
            bool isStepScope = a is StepAnalytic;

            float num = 0f, den = 0f;
            if (a.metrics != null)
            {
                for (int i = 0; i < a.metrics.Count; i++)
                {
                    AnalyticsMetric m = a.metrics[i];
                    if (m == null) continue;

                    MetricScoreResult mr = ComputeMetric(m, isStepScope, stepGuid, ctx);
                    ar.metrics.Add(mr);

                    if (mr.applicable)
                    {
                        float w = m.weight;
                        num += w * mr.score;
                        den += w;
                    }
                }
            }

            if (den > 0f)
            {
                ar.applicable = true;
                ar.score = Clamp01(num / den);
            }
            return ar;
        }

        static MetricScoreResult ComputeMetric(AnalyticsMetric m, bool isStepScope, string stepGuid, ReduceContext ctx)
        {
            var mr = new MetricScoreResult
            {
                id = m.id,
                label = m.label,
                kind = m.Kind,
                applicable = false,
                rawValue = 0f,
                score = 0f,
                worstSeverity = BandSeverity.None
            };

            switch (m.Kind)
            {
                case StepDurationMetric.KindId:
                {
                    // Step-scoped ceiling kind. Applicable iff the owning step was entered AND completed.
                    if (!isStepScope || string.IsNullOrEmpty(stepGuid)) return mr;
                    if (!ctx.TryStepDurationSeconds(stepGuid, out float seconds)) return mr;
                    mr.applicable = true;
                    mr.rawValue = seconds;
                    float penalty = CeilingPenalty(m, seconds, out BandSeverity worst);
                    mr.worstSeverity = worst;
                    mr.score = Clamp01(1f - penalty);
                    return mr;
                }

                case TotalDurationMetric.KindId:
                {
                    // Scene-wide ceiling kind. Applicable iff the bracket closed.
                    if (!ctx.Stream.IsComplete) return mr;
                    float seconds = (float)(ctx.Stream.DurationMs / 1000.0);
                    mr.applicable = true;
                    mr.rawValue = seconds;
                    float penalty = CeilingPenalty(m, seconds, out BandSeverity worst);
                    mr.worstSeverity = worst;
                    mr.score = Clamp01(1f - penalty);
                    return mr;
                }

                case DropMetric.KindId:
                    return ComputeCount(m, AnalyticsEventKind.Drop, isStepScope, stepGuid, ctx, mr);

                case WrongInteractionMetric.KindId:
                    return ComputeCount(m, AnalyticsEventKind.WrongInteraction, isStepScope, stepGuid, ctx, mr);

                case OrderMetric.KindId:
                    return ComputeCount(m, AnalyticsEventKind.OrderViolation, isStepScope, stepGuid, ctx, mr);

                case SignalMetric.KindId:
                    // Authored-failure kind: count Signal events whose signalId matches THIS metric's id.
                    return ComputeCount(m, AnalyticsEventKind.Signal, isStepScope, stepGuid, ctx, mr, signalById: true);

                default:
                    // Unknown metric kind: mask it (do not silently score 0/1).
                    return mr;
            }
        }

        static MetricScoreResult ComputeCount(AnalyticsMetric m, AnalyticsEventKind kind, bool isStepScope,
            string stepGuid, ReduceContext ctx, MetricScoreResult mr, bool signalById = false)
        {
            // Step-scoped count metric is applicable only if its step ran; scene-scoped is applicable
            // whenever the bracket ran (we have a stream). Zero occurrences -> score 1 (map sec-11.x).
            if (isStepScope)
            {
                if (string.IsNullOrEmpty(stepGuid) || !ctx.StepEntered(stepGuid)) return mr;
            }
            mr.applicable = true;

            int count = 0;
            float penalty = 0f;
            BandSeverity worst = BandSeverity.None;

            var events = ctx.Stream.events;
            for (int i = 0; i < events.Count; i++)
            {
                AnalyticsEvent e = events[i];
                // SignalMetric matches authored signals by id; the derived kinds match by event kind. The
                // two are disjoint - a signal NEVER counts toward a Drop/Wrong/Order metric and vice versa.
                bool match = signalById
                    ? (e.kind == AnalyticsEventKind.Signal && e.signalId == m.id)
                    : (e.kind == kind);
                if (!match) continue;
                if (isStepScope && e.stepGuid != stepGuid) continue;

                BandSeverity sev = DeriveSeverity(e, ctx);
                penalty += BandPenaltyFor(m, sev);
                if (sev > worst) worst = sev;
                count++;
            }

            mr.rawValue = count;
            mr.worstSeverity = worst;
            mr.score = Clamp01(1f - Clamp01(penalty));
            return mr;
        }

        /// <summary>The default severity table (B2.2 Step 5), author-overridable through the band weights.</summary>
        static BandSeverity DeriveSeverity(AnalyticsEvent e, ReduceContext ctx)
        {
            switch (e.kind)
            {
                case AnalyticsEventKind.Drop:
                    return ctx.IsRelevant(e.subjectId) ? BandSeverity.Error : BandSeverity.Warning;
                case AnalyticsEventKind.WrongInteraction:
                    // A known distractor is a soft slip; anything else (unknown / wrong target) is hard.
                    return ctx.IsKnownDistractor(e.subjectId) ? BandSeverity.Warning : BandSeverity.Error;
                case AnalyticsEventKind.OrderViolation:
                    return BandSeverity.Warning;
                case AnalyticsEventKind.Signal:
                    return BandSeverity.Error;   // an authored failure
                default:
                    return BandSeverity.None;
            }
        }

        static float CeilingPenalty(AnalyticsMetric m, float rawValue, out BandSeverity worst)
        {
            worst = BandSeverity.None;
            float penalty = 0f;
            float bestThreshold = -1f;
            if (m.bands == null) return 0f;
            for (int i = 0; i < m.bands.Count; i++)
            {
                ScoringBand b = m.bands[i];
                if (b == null) continue;
                if (b.penaltyWeight <= 0f) continue;   // None / disabled band
                if (b.threshold <= 0f) continue;       // unset threshold = inactive (no footgun zeroing)
                if (rawValue >= b.threshold && b.threshold > bestThreshold)
                {
                    bestThreshold = b.threshold;
                    penalty = b.penaltyWeight;
                    worst = b.name;
                }
            }
            return Clamp01(penalty);
        }

        static float BandPenaltyFor(AnalyticsMetric m, BandSeverity severity)
        {
            if (m.bands == null) return 0f;
            for (int i = 0; i < m.bands.Count; i++)
            {
                ScoringBand b = m.bands[i];
                if (b != null && b.name == severity) return b.penaltyWeight;
            }
            return 0f;
        }

        static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        // Precomputed indices over the stream so each metric reduction is cheap and the math is obvious.
        sealed class ReduceContext
        {
            public readonly SessionEventStream Stream;
            readonly Dictionary<string, double> _firstEntered = new Dictionary<string, double>();
            readonly Dictionary<string, double> _firstCompletedAfter = new Dictionary<string, double>();
            readonly Dictionary<string, Analytic> _analyticsById = new Dictionary<string, Analytic>();
            readonly Dictionary<string, TrackedSubject> _subjectsById = new Dictionary<string, TrackedSubject>();

            public ReduceContext(LabConfig config, SessionEventStream stream)
            {
                Stream = stream;

                if (config.analytics != null)
                    for (int i = 0; i < config.analytics.Count; i++)
                    {
                        Analytic a = config.analytics[i];
                        if (a != null && !string.IsNullOrEmpty(a.id) && !_analyticsById.ContainsKey(a.id))
                            _analyticsById[a.id] = a;
                    }

                if (config.subjects != null)
                    for (int i = 0; i < config.subjects.Count; i++)
                    {
                        TrackedSubject s = config.subjects[i];
                        if (s != null && !string.IsNullOrEmpty(s.id) && !_subjectsById.ContainsKey(s.id))
                            _subjectsById[s.id] = s;
                    }

                // First entered per step, and the first completed at/after that entered (loop-safe-ish:
                // the launch interpretation measures the FIRST traversal; loops are a noted sec-11 nuance).
                var events = stream.events;
                for (int i = 0; i < events.Count; i++)
                {
                    AnalyticsEvent e = events[i];
                    if (string.IsNullOrEmpty(e.stepGuid)) continue;
                    if (e.kind == AnalyticsEventKind.StepEntered && !_firstEntered.ContainsKey(e.stepGuid))
                        _firstEntered[e.stepGuid] = e.tMs;
                    else if (e.kind == AnalyticsEventKind.StepCompleted && !_firstCompletedAfter.ContainsKey(e.stepGuid)
                             && _firstEntered.TryGetValue(e.stepGuid, out double ent) && e.tMs >= ent)
                        _firstCompletedAfter[e.stepGuid] = e.tMs;
                }
            }

            public Analytic FindAnalytic(string id)
            {
                if (string.IsNullOrEmpty(id)) return null;
                _analyticsById.TryGetValue(id, out Analytic a);
                return a;
            }

            public bool StepEntered(string guid) => _firstEntered.ContainsKey(guid);

            public bool TryStepDurationSeconds(string guid, out float seconds)
            {
                seconds = 0f;
                if (!_firstEntered.TryGetValue(guid, out double ent)) return false;
                if (!_firstCompletedAfter.TryGetValue(guid, out double comp)) return false;
                seconds = (float)((comp - ent) / 1000.0);
                if (seconds < 0f) seconds = 0f;
                return true;
            }

            public bool IsRelevant(string subjectId)
            {
                return _subjectsById.TryGetValue(subjectId, out TrackedSubject s) && s.scenarioRelevant;
            }

            public bool IsKnownDistractor(string subjectId)
            {
                return _subjectsById.TryGetValue(subjectId, out TrackedSubject s) && !s.scenarioRelevant;
            }
        }
    }
}
