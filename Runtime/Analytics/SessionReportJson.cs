using System.Globalization;
using System.Text;

namespace Pitech.XR.Analytics
{
    // ---------- Session report -> JSON (write-only; the G2 wire payload) ----------
    // WS B2.1. A small, dependency-free serializer (no Newtonsoft - the DevKit must compile on AR with
    // no extra packages). WRITE-ONLY: the DevKit emits; the cloud (B2.3) parses. The polymorphic config
    // (Analytic / AnalyticsMetric subclasses) is keyed by a "type" discriminator = the CLR SHORT TYPE
    // NAME (e.g. "StepAnalytic", "StepDurationMetric") - the ratified convention (WS B1.6 S2, commit
    // d49bb64; the freeze doc sec 3.8), the same one ScenarioJsonExporter uses for steps. Explicit
    // per-type writing (no reflection) so it is IL2CPP-safe and the "no silent default" rule holds.
    //
    // PENDING G2: the field names / shape are the proposed wire contract for Stergios' 2026-06-29 review.

    /// <summary>Serializes a <see cref="SessionReport"/> to the G2 JSON wire format (write-only).</summary>
    public static class SessionReportJson
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string Serialize(SessionReport r)
        {
            var sb = new StringBuilder(1024);
            if (r == null) { sb.Append("null"); return sb.ToString(); }

            sb.Append('{');
            Num(sb, "schemaVersion", r.schemaVersion); sb.Append(',');
            Str(sb, "tenantId", r.tenantId); sb.Append(',');
            Str(sb, "sessionId", r.sessionId); sb.Append(',');
            Str(sb, "labId", r.labId); sb.Append(',');
            Str(sb, "labVersion", r.labVersion); sb.Append(',');
            Bool(sb, "isComplete", r.isComplete); sb.Append(',');

            Key(sb, "consent"); WriteConsent(sb, r.consent); sb.Append(',');

            Key(sb, "users"); sb.Append('[');
            if (r.users != null)
                for (int i = 0; i < r.users.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    SessionReportUser u = r.users[i];
                    sb.Append('{');
                    Str(sb, "userId", u != null ? u.userId : string.Empty); sb.Append(',');
                    Str(sb, "role", (u != null ? u.role : SessionRole.Participant).ToString());
                    sb.Append('}');
                }
            sb.Append("],");

            Key(sb, "events"); sb.Append('[');
            if (r.events != null)
                for (int i = 0; i < r.events.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    WriteEvent(sb, r.events[i]);
                }
            sb.Append("],");

            Key(sb, "config");
            WriteConfig(sb, r.config);

            sb.Append('}');
            return sb.ToString();
        }

        static void WriteEvent(StringBuilder sb, AnalyticsEvent e)
        {
            if (e == null) { sb.Append("null"); return; }
            sb.Append('{');
            Str(sb, "kind", e.kind.ToString()); sb.Append(',');
            Num(sb, "tMs", e.tMs); sb.Append(',');
            Str(sb, "stepGuid", e.stepGuid); sb.Append(',');
            Str(sb, "subjectId", e.subjectId); sb.Append(',');
            Str(sb, "signalId", e.signalId); sb.Append(',');
            Str(sb, "userId", e.userId);
            sb.Append('}');
        }

        static void WriteConsent(StringBuilder sb, Pitech.XR.Core.ConsentReceipt c)
        {
            if (c == null) { sb.Append("null"); return; }
            sb.Append('{');
            Str(sb, "consentId", c.consentId); sb.Append(',');
            Str(sb, "policyVersion", c.policyVersion); sb.Append(',');
            Str(sb, "grantedAtUtc", c.grantedAtUtc);
            sb.Append('}');
        }

        static void WriteConfig(StringBuilder sb, LabConfig rb)
        {
            if (rb == null) { sb.Append("null"); return; }
            sb.Append('{');
            Num(sb, "schemaVersion", rb.schemaVersion); sb.Append(',');

            Key(sb, "analytics"); sb.Append('[');
            if (rb.analytics != null)
                for (int i = 0; i < rb.analytics.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    WriteAnalytic(sb, rb.analytics[i]);
                }
            sb.Append("],");

            Key(sb, "subjects"); sb.Append('[');
            if (rb.subjects != null)
                for (int i = 0; i < rb.subjects.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    TrackedSubject s = rb.subjects[i];
                    sb.Append('{');
                    Str(sb, "id", s != null ? s.id : string.Empty); sb.Append(',');
                    Str(sb, "label", s != null ? s.label : string.Empty); sb.Append(',');
                    Bool(sb, "scenarioRelevant", s != null && s.scenarioRelevant); sb.Append(',');
                    Str(sb, "ownerStepGuid", s != null ? s.ownerStepGuid : string.Empty);
                    sb.Append('}');
                }
            sb.Append("],");

            Key(sb, "penalties"); sb.Append('[');
            if (rb.penalties != null)
                for (int i = 0; i < rb.penalties.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    WritePenalty(sb, rb.penalties[i]);
                }
            sb.Append("],");

            Key(sb, "goals"); sb.Append('[');
            if (rb.goals != null)
                for (int i = 0; i < rb.goals.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    WriteGoal(sb, rb.goals[i]);
                }
            sb.Append("],");

            Key(sb, "roleCapacities");
            WriteCapacities(sb, rb.roleCapacities);

            sb.Append('}');
        }

        static void WriteAnalytic(StringBuilder sb, Analytic a)
        {
            if (a == null) { sb.Append("null"); return; }
            sb.Append('{');
            Str(sb, "type", a.GetType().Name); sb.Append(',');   // CLR short-name discriminator (only StepAnalytic in v3)
            Str(sb, "id", a.id); sb.Append(',');
            Str(sb, "label", a.label); sb.Append(',');
            if (a is StepAnalytic sa)
            {
                Str(sb, "stepGuid", sa.stepGuid); sb.Append(',');
                Num(sb, "weight", sa.weight); sb.Append(',');
                Bool(sb, "failsScenario", sa.failsScenario); sb.Append(',');
            }

            Key(sb, "metrics"); sb.Append('[');
            if (a.metrics != null)
                for (int i = 0; i < a.metrics.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    WriteMetric(sb, a.metrics[i]);
                }
            sb.Append(']');
            sb.Append('}');
        }

        static void WriteMetric(StringBuilder sb, AnalyticsMetric m)
        {
            if (m == null) { sb.Append("null"); return; }
            sb.Append('{');
            Str(sb, "type", m.GetType().Name); sb.Append(',');   // CLR short-name discriminator
            Str(sb, "id", m.id); sb.Append(',');
            Str(sb, "label", m.label); sb.Append(',');
            Bool(sb, "critical", m.critical); sb.Append(',');
            Bool(sb, "notifyOnly", m.notifyOnly); sb.Append(',');

            Key(sb, "bands"); sb.Append('[');
            if (m.bands != null)
                for (int i = 0; i < m.bands.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    ScoringBand b = m.bands[i];
                    sb.Append('{');
                    Str(sb, "name", (b != null ? b.name : BandSeverity.None).ToString()); sb.Append(',');
                    Num(sb, "threshold", b != null ? b.threshold : 0f); sb.Append(',');
                    Num(sb, "penaltyWeight", b != null ? b.penaltyWeight : 0f); sb.Append(',');
                    Bool(sb, "notifyInScene", b != null && b.notifyInScene);
                    sb.Append('}');
                }
            sb.Append(']');
            sb.Append('}');
        }

        static void WritePenalty(StringBuilder sb, PenaltyRule p)
        {
            if (p == null) { sb.Append("null"); return; }
            sb.Append('{');
            Str(sb, "id", p.id); sb.Append(',');
            Str(sb, "label", p.label); sb.Append(',');
            Str(sb, "kind", p.kind.ToString()); sb.Append(',');
            Str(sb, "signalId", p.signalId); sb.Append(',');
            Num(sb, "pointsPerWarning", p.pointsPerWarning); sb.Append(',');
            Num(sb, "pointsPerError", p.pointsPerError); sb.Append(',');
            Num(sb, "maxDeduction", p.maxDeduction); sb.Append(',');
            Bool(sb, "failScenario", p.failScenario); sb.Append(',');
            Bool(sb, "notifyInScene", p.notifyInScene); sb.Append(',');
            Key(sb, "tiers"); sb.Append('[');
            if (p.tiers != null)
                for (int i = 0; i < p.tiers.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    PenaltyTier t = p.tiers[i];
                    sb.Append('{');
                    Num(sb, "overSeconds", t != null ? t.overSeconds : 0f); sb.Append(',');
                    Num(sb, "points", t != null ? t.points : 0);
                    sb.Append('}');
                }
            sb.Append(']');
            sb.Append('}');
        }

        static void WriteGoal(StringBuilder sb, Goal g)
        {
            if (g == null) { sb.Append("null"); return; }
            sb.Append('{');
            Str(sb, "id", g.id); sb.Append(',');
            Str(sb, "label", g.label); sb.Append(',');
            Num(sb, "bonusPoints", g.bonusPoints); sb.Append(',');
            Str(sb, "kind", g.kind.ToString()); sb.Append(',');
            Num(sb, "threshold", g.threshold); sb.Append(',');
            Str(sb, "countKind", g.countKind.ToString()); sb.Append(',');
            Str(sb, "signalId", g.signalId); sb.Append(',');
            Key(sb, "stepAnalyticIds"); sb.Append('[');
            if (g.stepAnalyticIds != null)
                for (int i = 0; i < g.stepAnalyticIds.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    WriteEscaped(sb, g.stepAnalyticIds[i]);
                }
            sb.Append(']');
            sb.Append('}');
        }

        static void WriteCapacities(StringBuilder sb, SessionRoleCapacities c)
        {
            if (c == null) { sb.Append("null"); return; }
            sb.Append('{');
            Num(sb, "minProfessors", c.minProfessors); sb.Append(',');
            Num(sb, "maxProfessors", c.maxProfessors); sb.Append(',');
            Num(sb, "minParticipants", c.minParticipants); sb.Append(',');
            Num(sb, "maxParticipants", c.maxParticipants); sb.Append(',');
            Num(sb, "minSpectators", c.minSpectators); sb.Append(',');
            Num(sb, "maxSpectators", c.maxSpectators);
            sb.Append('}');
        }

        // ---- primitives ----
        static void Key(StringBuilder sb, string key) { sb.Append('"').Append(key).Append("\":"); }
        static void Str(StringBuilder sb, string key, string val) { Key(sb, key); WriteEscaped(sb, val); }
        static void Num(StringBuilder sb, string key, int val) { Key(sb, key); sb.Append(val.ToString(Inv)); }
        static void Num(StringBuilder sb, string key, float val) { Key(sb, key); sb.Append(val.ToString("R", Inv)); }
        static void Num(StringBuilder sb, string key, double val) { Key(sb, key); sb.Append(val.ToString("R", Inv)); }
        static void Bool(StringBuilder sb, string key, bool val) { Key(sb, key); sb.Append(val ? "true" : "false"); }

        static void WriteEscaped(StringBuilder sb, string s)
        {
            if (s == null) { sb.Append("null"); return; }
            sb.Append('"');
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4", Inv));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }
}
