using System.Collections.Generic;

namespace Pitech.XR.Analytics
{
    // ---------- The grade engine: the canonical reducer (v3 model, 2026-07-02) ----------
    // Pure function: (LabConfig + captured SessionEventStream) -> GradeResult. NO Unity scene refs, NO
    // Stopwatch (the stream is already portable ms), runs ONCE at session stop. DevKit-CANONICAL; the Web
    // Portal (B2.3) is a MIRROR that must compute an identical GradeResult from the same raw (config + events).
    // The equivalence golden fixture keeps them in lockstep. All rules that the recorder ALSO evaluates live
    // (severity, gates, ceiling) live in AnalyticsSeverity so there is one source of truth.
    //
    // FORMULA (grade points are 0-100; scores are fractions 0-1):
    //   step   X_s   = equal mean of applicable scored (non-gate, non-notifyOnly) metrics; 0 if a gate failed;
    //                  1 if all gates passed and there are no scored metrics.
    //   base   B     = sum(w_s . X_s) / sum(w_s)  over ENTERED step analytics; 1.0 if the config has NO step
    //                  analytics (a deliberate pure-penalty lab); inapplicable if steps exist but none ran.
    //   penalty P    = sum of per-rule deductions (count kinds: per-occurrence points by severity, capped;
    //                  duration kind: the highest crossed tier).  In grade points.
    //   bonus  U     = sum(goal.bonusPoints for passed, earnable goals) - VOIDED entirely if any step failed.
    //   GRADE        = clamp01( B - P/100 + U/100 )   [single final clamp]
    //   FAILED (a critical scenario gate fired, or a scenario.failed fact is present) -> grade 0, terminal.
    //
    // isComplete: FAILED is always complete (the fail IS the outcome, even on an unclosed bracket). Otherwise a
    // grade needs an applicable base AND a closed bracket; else "Incomplete".

    /// <summary>The canonical analytics reducer (v3). Stateless; call <see cref="Compute"/>.</summary>
    public static class AnalyticsGradeEngine
    {
        public static GradeResult Compute(LabConfig config, SessionEventStream stream, SessionRole role)
        {
            var result = new GradeResult { role = role, isComplete = false, grade = 0f };
            if (config == null || stream == null) return result;

            var ctx = new ReduceContext(config, stream);

            bool scenarioFail = false;
            string failId = string.Empty, failLabel = string.Empty;

            // ---- 1. STEPS -> base ----
            float stepNum = 0f, stepDen = 0f;
            int stepsTotal = 0, stepsGraded = 0;
            bool anyStepFailed = false;

            if (config.analytics != null)
            {
                for (int i = 0; i < config.analytics.Count; i++)
                {
                    if (!(config.analytics[i] is StepAnalytic sa)) continue;   // v3: only step analytics
                    stepsTotal++;
                    AnalyticScoreResult sr = ComputeStep(sa, ctx);
                    result.steps.Add(sr);
                    if (!sr.applicable) continue;
                    stepsGraded++;
                    stepNum += sa.weight * sr.score;
                    stepDen += sa.weight;
                    if (sr.stepFailed)
                    {
                        anyStepFailed = true;
                        if (sa.failsScenario && !scenarioFail)
                        {
                            scenarioFail = true;
                            failId = FirstFailedGateId(sr);
                            failLabel = FirstFailedGateLabel(sr, sa);
                        }
                    }
                }
            }

            bool hasStepAnalytics = stepsTotal > 0;
            bool baseApplicable;
            float baseScore;
            if (!hasStepAnalytics) { baseScore = 1f; baseApplicable = true; }          // pure-penalty lab
            else if (stepDen > 0f) { baseScore = Clamp01(stepNum / stepDen); baseApplicable = true; }
            else { baseScore = 0f; baseApplicable = false; }                            // authored, none ran

            result.baseScore = baseScore;

            // ---- 2. PENALTIES ----
            int penaltyTotal = 0;
            if (config.penalties != null)
            {
                for (int i = 0; i < config.penalties.Count; i++)
                {
                    PenaltyRule p = config.penalties[i];
                    if (p == null) continue;
                    PenaltyScoreResult pr = ComputePenalty(p, ctx, out bool tripped);
                    result.penalties.Add(pr);
                    penaltyTotal += pr.pointsDeducted;
                    if (tripped && p.failScenario && !scenarioFail)
                    {
                        scenarioFail = true;
                        failId = p.id ?? string.Empty;
                        failLabel = string.IsNullOrEmpty(p.label) ? "Critical penalty" : p.label;
                    }
                }
            }
            result.penaltyPointsTotal = penaltyTotal;

            // ---- 3. GOALS (bonus) ----
            int bonusTotal = 0;
            if (config.goals != null)
            {
                for (int i = 0; i < config.goals.Count; i++)
                {
                    Goal g = config.goals[i];
                    if (g == null) continue;
                    GoalScoreResult gr = ComputeGoal(g, ctx);
                    result.goals.Add(gr);
                    if (gr.earnable && gr.passed && !anyStepFailed) bonusTotal += g.bonusPoints;
                }
            }
            result.bonusPointsTotal = bonusTotal;
            result.bonusesVoided = anyStepFailed;

            // ---- scenario.failed fact (authoritative for the restart / incomplete-step case) ----
            if (TryFindScenarioFailFact(stream, out string factCause) && !scenarioFail)
            {
                scenarioFail = true;
                failId = factCause ?? string.Empty;
                failLabel = ResolveCauseLabel(config, factCause);
            }

            // ---- stats ----
            result.stats = ComputeStats(ctx, stream, stepsGraded, stepsTotal);

            // ---- finalize ----
            if (scenarioFail)
            {
                result.failed = true;
                result.failCauseMetricId = failId;
                result.failCauseLabel = string.IsNullOrEmpty(failLabel) ? "Critical failure" : failLabel;
                result.grade = 0f;
                result.isComplete = true;   // a fail is a complete outcome, even on an unclosed bracket
                return result;
            }

            if (baseApplicable && stream.IsComplete)
            {
                result.isComplete = true;
                float g100 = baseScore * 100f - penaltyTotal + bonusTotal;
                result.grade = Clamp01(g100 / 100f);
            }
            // else: Incomplete (no base, or the bracket never closed) - grade stays 0, isComplete false.
            return result;
        }

        // ---- steps ----

        static AnalyticScoreResult ComputeStep(StepAnalytic sa, ReduceContext ctx)
        {
            var sr = new AnalyticScoreResult
            {
                id = sa.id,
                label = sa.label,
                kind = sa.Kind,
                weight = sa.weight,
                failsScenario = sa.failsScenario,
                applicable = false,
                score = 0f,
                stepFailed = false
            };

            // A step is applicable iff it was ENTERED (base normalizes over the steps the learner reached).
            if (string.IsNullOrEmpty(sa.stepGuid) || !ctx.StepEntered(sa.stepGuid)) return sr;
            sr.applicable = true;

            float num = 0f; int den = 0;
            if (sa.metrics != null)
            {
                for (int i = 0; i < sa.metrics.Count; i++)
                {
                    AnalyticsMetric m = sa.metrics[i];
                    if (m == null) continue;
                    MetricScoreResult mr = ComputeMetric(m, sa.stepGuid, ctx);
                    sr.metrics.Add(mr);

                    if (m.critical)
                    {
                        if (mr.gateFailed) sr.stepFailed = true;
                        continue;   // a gate never contributes to the score
                    }
                    if (m.notifyOnly) continue;   // notify-only: no score contribution
                    if (!mr.applicable) continue;
                    num += mr.score; den++;        // EQUAL split (v3: no per-metric weight)
                }
            }

            if (sr.stepFailed) { sr.score = 0f; return sr; }
            sr.score = den > 0 ? Clamp01(num / den) : 1f;   // all gates passed, no scored metrics -> perfect
            return sr;
        }

        static MetricScoreResult ComputeMetric(AnalyticsMetric m, string stepGuid, ReduceContext ctx)
        {
            var mr = new MetricScoreResult
            {
                id = m.id, label = m.label, kind = m.Kind,
                applicable = false, rawValue = 0f, score = 0f,
                worstSeverity = BandSeverity.None, isGate = m.critical, gateFailed = false
            };

            if (m.Kind == StepDurationMetric.KindId)
            {
                if (!ctx.TryStepDurationSeconds(stepGuid, out float seconds)) return mr;   // step never completed
                mr.applicable = true;
                mr.rawValue = seconds;
                float penalty = AnalyticsSeverity.CeilingPenalty(m, seconds, out BandSeverity worst);
                mr.worstSeverity = worst;
                mr.score = Clamp01(1f - penalty);
                if (m.critical) mr.gateFailed = AnalyticsSeverity.DurationGateTrips(m, seconds);
                return mr;
            }

            // count / signal kinds
            if (!ctx.StepEntered(stepGuid)) return mr;
            mr.applicable = true;

            AnalyticsEventKind kind; bool signalById = false;
            switch (m.Kind)
            {
                case DropMetric.KindId: kind = AnalyticsEventKind.Drop; break;
                case WrongInteractionMetric.KindId: kind = AnalyticsEventKind.WrongInteraction; break;
                case OrderMetric.KindId: kind = AnalyticsEventKind.OrderViolation; break;
                case SignalMetric.KindId: kind = AnalyticsEventKind.Signal; signalById = true; break;
                default: mr.applicable = false; return mr;   // unknown kind - masked
            }

            int count = 0; float penaltySum = 0f; bool anyError = false; BandSeverity worstC = BandSeverity.None;
            var events = ctx.Stream.events;
            for (int i = 0; i < events.Count; i++)
            {
                AnalyticsEvent e = events[i];
                bool match = signalById ? (e.kind == AnalyticsEventKind.Signal && e.signalId == m.id) : (e.kind == kind);
                if (!match) continue;
                if (e.stepGuid != stepGuid) continue;   // step-scoped: only this step's occurrences
                BandSeverity sev = AnalyticsSeverity.Derive(e.kind, ctx.IsRelevant(e.subjectId), ctx.IsKnownDistractor(e.subjectId));
                penaltySum += AnalyticsSeverity.BandPenaltyFor(m, sev);
                if (sev > worstC) worstC = sev;
                if (sev == BandSeverity.Error) anyError = true;
                count++;
            }
            mr.rawValue = count;
            mr.worstSeverity = worstC;
            mr.score = Clamp01(1f - Clamp01(penaltySum));
            if (m.critical) mr.gateFailed = AnalyticsSeverity.CountGateTrips(anyError ? BandSeverity.Error : BandSeverity.None);
            return mr;
        }

        // ---- penalties ----

        static PenaltyScoreResult ComputePenalty(PenaltyRule p, ReduceContext ctx, out bool tripped)
        {
            var pr = new PenaltyScoreResult { id = p.id, label = p.label, kind = p.kind.ToString() };
            tripped = false;

            if (p.kind == PenaltyKind.TotalDuration)
            {
                float seconds = (float)(ctx.Stream.DurationMs / 1000.0);
                int best = 0; bool crossed = false;
                float bestOver = -1f;
                if (p.tiers != null)
                    for (int i = 0; i < p.tiers.Count; i++)
                    {
                        PenaltyTier t = p.tiers[i];
                        if (t == null || t.overSeconds <= 0f) continue;
                        if (seconds >= t.overSeconds && t.overSeconds > bestOver) { bestOver = t.overSeconds; best = t.points; crossed = true; }
                    }
                pr.pointsDeducted = ApplyCap(best, p.maxDeduction, out bool capped);
                pr.capped = capped;
                tripped = crossed;
                return pr;
            }

            if (!p.TryEventKind(out AnalyticsEventKind ek)) return pr;
            bool signalById = p.kind == PenaltyKind.Signal;
            int points = 0;
            var events = ctx.Stream.events;
            for (int i = 0; i < events.Count; i++)
            {
                AnalyticsEvent e = events[i];
                bool match = signalById ? (e.kind == AnalyticsEventKind.Signal && e.signalId == p.signalId) : (e.kind == ek);
                if (!match) continue;
                BandSeverity sev = AnalyticsSeverity.Derive(e.kind, ctx.IsRelevant(e.subjectId), ctx.IsKnownDistractor(e.subjectId));
                if (sev == BandSeverity.Error) { pr.errorCount++; points += p.pointsPerError; tripped = true; }
                else if (sev == BandSeverity.Warning) { pr.warningCount++; points += p.pointsPerWarning; }
                // None severity -> not counted
            }
            pr.pointsDeducted = ApplyCap(points, p.maxDeduction, out bool cap2);
            pr.capped = cap2;
            return pr;
        }

        static int ApplyCap(int points, int maxDeduction, out bool capped)
        {
            capped = false;
            if (points < 0) points = 0;
            if (maxDeduction > 0 && points > maxDeduction) { capped = true; return maxDeduction; }
            return points;
        }

        // ---- goals ----

        static GoalScoreResult ComputeGoal(Goal g, ReduceContext ctx)
        {
            var gr = new GoalScoreResult
            {
                id = g.id, label = g.label, kind = g.kind,
                bonusPoints = g.bonusPoints, threshold = g.threshold,
                passed = false, earnable = false, rawValue = 0f
            };

            switch (g.kind)
            {
                case GoalKind.StepsScore:
                {
                    // Weighted mean of the referenced step analytics; earnable only if ALL referenced ran.
                    var ids = g.stepAnalyticIds;
                    float num = 0f, den = 0f; bool allRan = true; int considered = 0;
                    var steps = ctx.StepAnalytics;
                    for (int i = 0; i < steps.Count; i++)
                    {
                        StepAnalytic sa = steps[i];
                        if (ids != null && ids.Count > 0 && !ids.Contains(sa.id)) continue;
                        considered++;
                        if (!ctx.StepEntered(sa.stepGuid)) { allRan = false; break; }
                        AnalyticScoreResult sr = ComputeStep(sa, ctx);
                        num += sa.weight * sr.score; den += sa.weight;
                    }
                    if (!allRan || considered == 0 || den <= 0f) return gr;   // not earnable
                    gr.earnable = true;
                    gr.rawValue = Clamp01(num / den) * 100f;                  // percent
                    gr.passed = gr.rawValue >= g.threshold;
                    return gr;
                }
                case GoalKind.TotalTimeUnder:
                {
                    if (!ctx.Stream.IsComplete || g.threshold <= 0f) return gr;   // can't judge time / unset
                    gr.earnable = true;
                    gr.rawValue = (float)(ctx.Stream.DurationMs / 1000.0);        // seconds
                    gr.passed = gr.rawValue <= g.threshold;
                    return gr;
                }
                case GoalKind.MaxOccurrences:
                {
                    if (g.threshold < 0f) return gr;
                    gr.earnable = true;
                    gr.rawValue = ctx.CountOccurrences(g.countKind, g.signalId);   // raw count, severity-blind
                    gr.passed = gr.rawValue <= g.threshold;
                    return gr;
                }
                default:
                    return gr;
            }
        }

        // ---- stats + fact ----

        static SessionStats ComputeStats(ReduceContext ctx, SessionEventStream stream, int stepsGraded, int stepsTotal)
        {
            var st = new SessionStats
            {
                totalSeconds = (float)(stream.DurationMs / 1000.0),
                stepsGraded = stepsGraded,
                stepsTotal = stepsTotal
            };
            var events = stream.events;
            for (int i = 0; i < events.Count; i++)
            {
                switch (events[i].kind)
                {
                    case AnalyticsEventKind.Drop: st.drops++; break;
                    case AnalyticsEventKind.WrongInteraction: st.wrongInteractions++; break;
                    case AnalyticsEventKind.OrderViolation: st.orderViolations++; break;
                }
            }
            return st;
        }

        static bool TryFindScenarioFailFact(SessionEventStream stream, out string cause)
        {
            cause = null;
            var events = stream.events;
            for (int i = 0; i < events.Count; i++)
                if (events[i].kind == AnalyticsEventKind.ScenarioFailed)
                {
                    cause = events[i].signalId;
                    return true;
                }
            return false;
        }

        static string FirstFailedGateId(AnalyticScoreResult sr)
        {
            for (int i = 0; i < sr.metrics.Count; i++)
                if (sr.metrics[i].isGate && sr.metrics[i].gateFailed) return sr.metrics[i].id ?? string.Empty;
            return string.Empty;
        }

        static string FirstFailedGateLabel(AnalyticScoreResult sr, StepAnalytic sa)
        {
            for (int i = 0; i < sr.metrics.Count; i++)
                if (sr.metrics[i].isGate && sr.metrics[i].gateFailed)
                {
                    string ml = sr.metrics[i].label;
                    string sl = string.IsNullOrEmpty(sa.label) ? "step" : sa.label;
                    return (string.IsNullOrEmpty(ml) ? "Critical" : ml) + " (" + sl + ")";
                }
            return string.IsNullOrEmpty(sa.label) ? "Critical step" : sa.label;
        }

        static string ResolveCauseLabel(LabConfig config, string causeId)
        {
            if (string.IsNullOrEmpty(causeId)) return "Critical failure";
            if (config.analytics != null)
                for (int i = 0; i < config.analytics.Count; i++)
                    if (config.analytics[i] is StepAnalytic sa && sa.metrics != null)
                        for (int j = 0; j < sa.metrics.Count; j++)
                            if (sa.metrics[j] != null && sa.metrics[j].id == causeId)
                                return (string.IsNullOrEmpty(sa.metrics[j].label) ? "Critical" : sa.metrics[j].label)
                                     + " (" + (string.IsNullOrEmpty(sa.label) ? "step" : sa.label) + ")";
            if (config.penalties != null)
                for (int i = 0; i < config.penalties.Count; i++)
                    if (config.penalties[i] != null && config.penalties[i].id == causeId)
                        return string.IsNullOrEmpty(config.penalties[i].label) ? "Critical penalty" : config.penalties[i].label;
            return "Critical failure";
        }

        static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        // ---- reduce context (indices over the stream + config so each reduction is cheap) ----
        sealed class ReduceContext
        {
            public readonly SessionEventStream Stream;
            public readonly List<StepAnalytic> StepAnalytics = new List<StepAnalytic>();
            readonly Dictionary<string, double> _firstEntered = new Dictionary<string, double>();
            readonly Dictionary<string, double> _firstCompletedAfter = new Dictionary<string, double>();
            readonly Dictionary<string, TrackedSubject> _subjectsById = new Dictionary<string, TrackedSubject>();

            public ReduceContext(LabConfig config, SessionEventStream stream)
            {
                Stream = stream;

                if (config.analytics != null)
                    for (int i = 0; i < config.analytics.Count; i++)
                        if (config.analytics[i] is StepAnalytic sa) StepAnalytics.Add(sa);

                if (config.subjects != null)
                    for (int i = 0; i < config.subjects.Count; i++)
                    {
                        TrackedSubject s = config.subjects[i];
                        if (s != null && !string.IsNullOrEmpty(s.id) && !_subjectsById.ContainsKey(s.id))
                            _subjectsById[s.id] = s;
                    }

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

            public bool StepEntered(string guid) => !string.IsNullOrEmpty(guid) && _firstEntered.ContainsKey(guid);

            public bool TryStepDurationSeconds(string guid, out float seconds)
            {
                seconds = 0f;
                if (string.IsNullOrEmpty(guid)) return false;
                if (!_firstEntered.TryGetValue(guid, out double ent)) return false;
                if (!_firstCompletedAfter.TryGetValue(guid, out double comp)) return false;
                seconds = (float)((comp - ent) / 1000.0);
                if (seconds < 0f) seconds = 0f;
                return true;
            }

            public bool IsRelevant(string subjectId)
                => _subjectsById.TryGetValue(subjectId, out TrackedSubject s) && s.scenarioRelevant;

            public bool IsKnownDistractor(string subjectId)
                => _subjectsById.TryGetValue(subjectId, out TrackedSubject s) && !s.scenarioRelevant;

            public int CountOccurrences(CountKind countKind, string signalId)
            {
                AnalyticsEventKind ek;
                bool signalById = false;
                switch (countKind)
                {
                    case CountKind.Drop: ek = AnalyticsEventKind.Drop; break;
                    case CountKind.WrongInteraction: ek = AnalyticsEventKind.WrongInteraction; break;
                    case CountKind.Order: ek = AnalyticsEventKind.OrderViolation; break;
                    case CountKind.Signal: ek = AnalyticsEventKind.Signal; signalById = true; break;
                    default: return 0;
                }
                int n = 0;
                var events = Stream.events;
                for (int i = 0; i < events.Count; i++)
                {
                    AnalyticsEvent e = events[i];
                    if (signalById) { if (e.kind == AnalyticsEventKind.Signal && e.signalId == signalId) n++; }
                    else if (e.kind == ek) n++;
                }
                return n;
            }
        }
    }
}
