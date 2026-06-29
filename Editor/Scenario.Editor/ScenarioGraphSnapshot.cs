#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Pitech.XR.Scenario;

namespace Pitech.XR.Scenario.Editor
{
    /// <summary>
    /// Proof A's single source of truth. One generic <see cref="SerializedObject"/> walk over a
    /// <see cref="Scenario"/>'s <c>steps</c> graph, used by BOTH the test
    /// (<c>ScenarioGraphIntegrityTests</c>) and the <c>Export Lab as Test Fixture</c> tool - so the
    /// baseline captured at export and the snapshot re-extracted under new package code come from the
    /// SAME code path and are byte-identical by construction.
    ///
    /// The walk is intentionally TYPE-AGNOSTIC: it never lists step types. It derives routing fields by
    /// the <c>*NextGuid</c>/<c>specificStepGuid</c> convention and visits every serialized property under
    /// <c>steps</c>, so a future step type's fields are covered with no edit here. (WS A3 / Appendix I.0.)
    /// </summary>
    public static class ScenarioGraphSnapshot
    {
        // v2 (2026-06-10): StableId disambiguates same-named siblings (sibling index per path segment)
        // and same-type components (component index) - a silent rewire between such targets is now a diff.
        public const int SchemaVersion = 2;

        /// <summary>Find the Scenario component on a loaded prefab/scene-object root (inactive included).</summary>
        public static Scenario FindScenario(GameObject root)
            => root == null ? null : root.GetComponentInChildren<Scenario>(true);

        // -------------------------------------------------------------------------------------
        // Invariants (the hard gate). Returns one human-readable line per violation; empty = clean.
        // Each line names the step (number + type), the field, and the concrete action to take.
        //
        // A note on UnityEvent listeners: real labs ship with inert authoring detritus - listener rows
        // with no target (an empty row, OR a method like SetActive whose target slot was never filled).
        // Those never fire at runtime (UnityEvent skips a persistent call with a null target) and are
        // NOT violations. The ONLY listener violation is a DANGLING target: a reference that WAS
        // assigned and whose object (or its script) is now gone (objectReferenceInstanceIDValue != 0 but
        // resolves null). The snapshot records every listener leaf regardless, so a refactor that changes
        // any listener target/method is still caught as drift - this check is only for genuine breakage.
        // -------------------------------------------------------------------------------------
        public static List<string> CheckInvariants(Scenario scenario)
        {
            var violations = new List<string>();
            if (scenario == null) { violations.Add("This object has no Scenario component."); return violations; }

            var so = new SerializedObject(scenario);
            var stepsRoot = so.FindProperty("steps");
            if (stepsRoot == null) { violations.Add("The Scenario has no step list to check."); return violations; }

            // Pass 1: collect node guids (Step.guid - NOT the reference guids under childRequirements).
            var nodeGuids = new HashSet<string>();
            var duplicateGuids = new List<string>();
            var routingRefs = new List<(string path, string value)>();   // must be "" or in nodeGuids
            var nullEntries = new List<string>();
            var missingRefs = new List<string>();                        // dangling refs OUTSIDE listeners
            var calls = new Dictionary<string, CallInfo>();              // one entry per persistent-call row

            Walk(stepsRoot, p =>
            {
                string path = p.propertyPath;

                // Null [SerializeReference] element directly in a steps array.
                if (p.propertyType == SerializedPropertyType.ManagedReference
                    && IsStepsArrayElement(path)
                    && string.IsNullOrEmpty(p.managedReferenceFullTypename))
                {
                    nullEntries.Add(path);
                    return;
                }

                if (p.propertyType == SerializedPropertyType.String)
                {
                    if (p.name == "guid" && !IsUnderRequirementList(path))
                    {
                        // A Step's own identity.
                        string g = p.stringValue;
                        if (!string.IsNullOrEmpty(g))
                        {
                            if (!nodeGuids.Add(g)) duplicateGuids.Add(g);
                        }
                    }
                }
            });

            // Pass 2: now that every node guid is known, validate routing + refs + listeners.
            Walk(stepsRoot, p =>
            {
                string path = p.propertyPath;

                if (p.propertyType == SerializedPropertyType.String && IsRoutingField(p.name, path))
                {
                    string v = p.stringValue;
                    if (!string.IsNullOrEmpty(v) && !nodeGuids.Contains(v))
                        routingRefs.Add((path, v));
                }
                else if (p.propertyType == SerializedPropertyType.ObjectReference)
                {
                    bool underCall = IsUnderPersistentCall(path);
                    if (!underCall)
                    {
                        // Dangling pointer in a step field: serialized fileID/GUID no longer resolves.
                        // A clean null (instanceID 0) is allowed by design.
                        if (p.objectReferenceValue == null && p.objectReferenceInstanceIDValue != 0)
                            missingRefs.Add(path);
                    }
                    else if (p.name == "m_Target")
                    {
                        // Only a dangling target matters (assigned-then-broken). A clean null is benign.
                        GetCall(calls, path).targetDangling =
                            p.objectReferenceValue == null && p.objectReferenceInstanceIDValue != 0;
                    }
                }
            });

            foreach (var n in nullEntries)
                violations.Add($"{StepLocator(so, n)} is empty - there is a blank entry in the step list. "
                    + "Use the Scenario inspector's \"Clear Nulls\" to remove the empty slot, or re-add the intended step.");
            foreach (var d in duplicateGuids)
                violations.Add($"Two or more steps share the same internal id ('{Shorten(d)}') - navigation between "
                    + "them will misbehave. Re-create one of the duplicated steps so it gets a fresh id.");
            foreach (var r in routingRefs)
                violations.Add($"{StepLocator(so, r.path)}: a connection ({FieldLeaf(r.path)}) points to a step "
                    + $"that no longer exists (id '{Shorten(r.value)}'). Re-link it in the Scenario graph, or clear the connection.");
            foreach (var m in missingRefs)
                violations.Add($"{StepLocator(so, m)}: the '{FieldLeaf(m)}' reference is missing - the object was "
                    + "deleted, or its script was removed. Re-assign it in the lab, or clear the field.");

            // DANGLING listener targets only. A clean-null target (never assigned - with or without a
            // method) is inert authoring detritus real labs ship, so it is left alone; only a reference
            // that was assigned and is now broken (instanceID != 0, resolves null) is a violation.
            foreach (var kv in calls)
            {
                if (kv.Value.targetDangling)
                    violations.Add($"{StepLocator(so, kv.Key)}: the '{EventField(kv.Key)}' event has a listener "
                        + $"(slot {Slot(kv.Key)}) whose target object is missing - it was deleted, or its script was "
                        + "removed. Re-assign the target, or delete the listener row.");
            }

            return violations;
        }

        // ---- WS B1.6 Step 1: dangling-ROUTE auto-repair (the optional half; the LINT lives in
        // CheckInvariants above). Mirrors that detection EXACTLY (same Walk + node-guid set), restricted
        // to the nextGuid family - clearing one to "" means "fall through to the next step in list" (the
        // neutral default). Deliberately does NOT touch specificStepGuid / childRequirement.guid
        // (structural group refs - fixed by hand) nor any [SerializeReference]/object data. -----------

        // Route guids that mean "go to this step next" - safe to clear to "" (fall-through). Excludes
        // specificStepGuid + childRequirement.guid (those are structural, not fall-through-safe).
        static bool IsRouteGuidField(string name)
            => name == "nextGuid" || (name != null && name.EndsWith("NextGuid", System.StringComparison.Ordinal));

        static List<string> FindDanglingRouteGuidPaths(SerializedObject so)
        {
            var paths = new List<string>();
            var stepsRoot = so?.FindProperty("steps");
            if (stepsRoot == null) return paths;

            // Pass 1: every Step.guid (NOT the reference guids under childRequirements) - same as CheckInvariants.
            var nodeGuids = new HashSet<string>();
            Walk(stepsRoot, p =>
            {
                if (p.propertyType == SerializedPropertyType.String && p.name == "guid" && !IsUnderRequirementList(p.propertyPath))
                {
                    string g = p.stringValue;
                    if (!string.IsNullOrEmpty(g)) nodeGuids.Add(g);
                }
            });

            // Pass 2: route guids whose value is non-empty and resolves to no step = dangling.
            Walk(stepsRoot, p =>
            {
                if (p.propertyType == SerializedPropertyType.String && IsRouteGuidField(p.name))
                {
                    string v = p.stringValue;
                    if (!string.IsNullOrEmpty(v) && !nodeGuids.Contains(v))
                        paths.Add(p.propertyPath);
                }
            });
            return paths;
        }

        /// <summary>Editor read-only: how many dangling ROUTE guids (nextGuid family) the repair would clear.</summary>
        public static int CountDanglingRoutes(Scenario scenario)
            => scenario == null ? 0 : FindDanglingRouteGuidPaths(new SerializedObject(scenario)).Count;

        /// <summary>
        /// Editor repair (undo-able): clears dangling ROUTE guids (nextGuid family) to "" so they fall
        /// through to the next step in list. Clears EXACTLY the dangling routes <see cref="CheckInvariants"/>
        /// flags - never the step list, group requirements, or object refs. Returns the count cleared.
        /// </summary>
        public static int RepairDanglingRoutes(Scenario scenario)
        {
            if (scenario == null) return 0;
            var so = new SerializedObject(scenario);
            var paths = FindDanglingRouteGuidPaths(so);
            if (paths.Count == 0) return 0;

            foreach (var path in paths)
            {
                var pr = so.FindProperty(path);
                if (pr != null) pr.stringValue = "";
            }
            so.ApplyModifiedProperties();   // registers Undo + marks the Scenario dirty
            return paths.Count;
        }

        // ---- WS B1.3 Step 6 (map sec-10): the two multiplayer validators. INERT BY CONSTRUCTION at
        // launch (no launch lab declares a Networked param; declared-param counts are ~16, well under the
        // WARN/FAIL thresholds). Parameters live on LabConsole, NOT on the Scenario, so these take the
        // prefab ROOT (the object Proof A already loads) and resolve the LabConsole from it. A prefab with
        // no LabConsole (the synthetic corpus) yields zero findings. Internal: not on the public API
        // baseline (Proof B). Severity carried so the test maps FAIL -> Assert.Fail, WARN -> a logged note.
        //
        // NOTE on enum reads: the Pitech.XR.Scenario.Editor asmdef references Pitech.XR.Scenario (so
        // ConditionsStep / ConditionValueSource are nameable) but NOT the Pitech.XR.Core RUNTIME assembly
        // (only Pitech.XR.Core.Editor) - assembly refs are not transitive. So the Core enum ParamScope is
        // read by its locked serialized index (ParamScope {Local=0, Networked=1}, ParamTypes.cs), the same
        // technique StatsConfigToParametersUpgrader.cs uses, to avoid widening the asmdef references.

        // ParamScope serialized index (Pitech.XR.Core.ParamScope, ParamTypes.cs): {Local=0, Networked=1}.
        // Read by index because the Core RUNTIME assembly is not referenced here (see note above).
        const int ParamScopeNetworked = 1;

        internal enum LabValidationSeverity { Warn, Fail }

        internal struct LabValidationFinding
        {
            public LabValidationSeverity severity;
            public string message;
        }

        // WARN/FAIL budget thresholds (map sec-10). Measured baseline ~16; cap 64.
        internal const int StateBudgetWarnAt = 48;
        internal const int StateBudgetFailAt = 64;

        /// <summary>WS B1.3 Step 6: run the two launch validators against a loaded lab prefab ROOT.
        /// READ-GATE-FORBIDDEN: a ConditionsStep that gates on a Networked-scoped declared parameter is a
        /// FAIL (an AR/single-player client with no peer would hang). STATE-BUDGET: count declared
        /// parameters; WARN at 48, FAIL at 64. Returns one finding per violation; empty = clean. Reads the
        /// LabConsole's private 'parameters' list via SerializedObject (the field is [SerializeField] but
        /// not public). No LabConsole on the root -> empty (synthetic fixtures).</summary>
        internal static List<LabValidationFinding> RunLabValidators(GameObject prefabRoot)
        {
            var findings = new List<LabValidationFinding>();
            if (prefabRoot == null) return findings;

            var console = prefabRoot.GetComponentInChildren<LabConsole>(true);
            if (console == null) return findings;   // no params on this lab -> nothing to validate

            // Build the declared-parameter table from the private serialized list.
            var declaredScopeById = ReadDeclaredParamScopes(console, out int declaredCount);

            // --- STATE-BUDGET ---
            if (declaredCount >= StateBudgetFailAt)
                findings.Add(new LabValidationFinding
                {
                    severity = LabValidationSeverity.Fail,
                    message = $"State budget exceeded: this lab declares {declaredCount} parameters "
                        + $"(hard cap {StateBudgetFailAt}). Reduce the number of declared parameters."
                });
            else if (declaredCount >= StateBudgetWarnAt)
                findings.Add(new LabValidationFinding
                {
                    severity = LabValidationSeverity.Warn,
                    message = $"State budget high: this lab declares {declaredCount} parameters "
                        + $"(warning at {StateBudgetWarnAt}, hard cap {StateBudgetFailAt}). Consider reducing them."
                });

            // --- READ-GATE-FORBIDDEN ---
            var scenario = FindScenario(prefabRoot);
            if (scenario != null && declaredScopeById.Count > 0)
            {
                var so = new SerializedObject(scenario);
                var stepsRoot = so.FindProperty("steps");
                if (stepsRoot != null)
                {
                    Walk(stepsRoot, p =>
                    {
                        // A ConditionsStep gating on a Stat-sourced value: valueSource == Stat (enum
                        // index 0), read statKey, resolve to a declared param, FAIL if Networked-scoped.
                        if (p.propertyType != SerializedPropertyType.ManagedReference) return;
                        if (!IsConditionsStep(p)) return;

                        var vs = p.FindPropertyRelative("valueSource");
                        if (vs == null || vs.propertyType != SerializedPropertyType.Enum) return;
                        if (vs.enumValueIndex != (int)Pitech.XR.Scenario.ConditionValueSource.Stat) return;

                        // Resolve the key exactly like the runtime does (statKey, else memberName) and
                        // normalize with the same StatsConfig.NormalizeKey (Trim) the store/read use, so the
                        // validator matches what the lab will actually look up at runtime (Stats asmdef is referenced).
                        string key = p.FindPropertyRelative("statKey")?.stringValue;
                        if (string.IsNullOrEmpty(key)) key = p.FindPropertyRelative("memberName")?.stringValue;
                        key = Pitech.XR.Stats.StatsConfig.NormalizeKey(key);
                        if (string.IsNullOrEmpty(key)) return;
                        if (declaredScopeById.TryGetValue(key, out int scopeIndex)
                            && scopeIndex == ParamScopeNetworked)
                        {
                            findings.Add(new LabValidationFinding
                            {
                                severity = LabValidationSeverity.Fail,
                                message = $"{StepLocator(so, p.propertyPath)}: a step advance gates on the "
                                    + $"Networked parameter '{key}'. A client with no peer (AR / single-player) "
                                    + "would never receive a value and the lab would hang. Change the parameter "
                                    + "scope to Local, or gate on a Local value."
                            });
                        }
                    });
                }
            }

            return findings;
        }

        // Read the LabConsole's private [SerializeField] List<ConsoleParameter> 'parameters' via
        // SerializedObject (it is serialized but not public - LabConsole.cs). Returns {id -> scope index}
        // for declared params with a non-empty id; out 'count' is the raw declared count (state budget).
        // Scope is the serialized enum INDEX (ParamScope {Local=0, Networked=1}) - the Core runtime enum
        // type is not referenced by this asmdef (see note above), so it is carried as an int.
        static Dictionary<string, int> ReadDeclaredParamScopes(LabConsole console, out int count)
        {
            var byId = new Dictionary<string, int>(System.StringComparer.Ordinal);
            count = 0;
            var so = new SerializedObject(console);
            var list = so.FindProperty("parameters");
            if (list == null || !list.isArray) return byId;
            count = list.arraySize;
            for (int i = 0; i < list.arraySize; i++)
            {
                var el = list.GetArrayElementAtIndex(i);
                string id = el.FindPropertyRelative("id")?.stringValue;
                var scopeProp = el.FindPropertyRelative("scope");
                int scopeIndex = scopeProp != null && scopeProp.propertyType == SerializedPropertyType.Enum
                    ? scopeProp.enumValueIndex
                    : 0;   // default Local
                if (!string.IsNullOrEmpty(id)) byId[id] = scopeIndex;   // last wins on dup id (matches LocalParamStore.Declare)
            }
            return byId;
        }

        // True when a [SerializeReference] element's managed type is ConditionsStep (matched on the
        // type name in managedReferenceFullTypename, same technique ShortTypeName uses).
        static bool IsConditionsStep(SerializedProperty p)
            => ShortTypeName(p.managedReferenceFullTypename) == nameof(Pitech.XR.Scenario.ConditionsStep);

        // ---- per-listener accumulation + friendly-message helpers ---------------------------

        sealed class CallInfo { public bool targetDangling; }

        static CallInfo GetCall(Dictionary<string, CallInfo> calls, string propPathUnderCall)
        {
            string key = CallPath(propPathUnderCall);
            if (!calls.TryGetValue(key, out var ci)) { ci = new CallInfo(); calls[key] = ci; }
            return ci;
        }

        // The path of the persistent-call ROW that contains a given m_Target/m_MethodName leaf.
        static string CallPath(string path)
        {
            var m = Regex.Match(path, @"^(.*?m_PersistentCalls\.m_Calls\.Array\.data\[\d+\])");
            return m.Success ? m.Groups[1].Value : path;
        }

        // "Step 06 (EventStep)" or, for a step nested in a group, "Step 02 > child 01 (EventStep)".
        // Resolves each step's type from its [SerializeReference] type name - no hard type list.
        static string StepLocator(SerializedObject so, string path)
        {
            var segs = Regex.Matches(path, @"steps\.Array\.data\[(\d+)\]");
            if (segs.Count == 0) return "The Scenario";
            var sb = new StringBuilder();
            for (int i = 0; i < segs.Count; i++)
            {
                int idx = int.Parse(segs[i].Groups[1].Value, CultureInfo.InvariantCulture);
                string stepPath = path.Substring(0, segs[i].Index + segs[i].Length);
                string type = ShortTypeName(so.FindProperty(stepPath)?.managedReferenceFullTypename);
                sb.Append(i == 0
                    ? $"Step {idx.ToString("00", CultureInfo.InvariantCulture)}"
                    : $" > child {idx.ToString("00", CultureInfo.InvariantCulture)}");
                if (!string.IsNullOrEmpty(type)) sb.Append($" ({type})");
            }
            return sb.ToString();
        }

        // "Pitech.XR.Scenario Pitech.XR.Scenario.EventStep" -> "EventStep".
        static string ShortTypeName(string managedReferenceFullTypename)
        {
            if (string.IsNullOrEmpty(managedReferenceFullTypename)) return null;
            int sp = managedReferenceFullTypename.LastIndexOf(' ');
            string t = sp >= 0 ? managedReferenceFullTypename.Substring(sp + 1) : managedReferenceFullTypename;
            int dot = t.LastIndexOf('.');
            return dot >= 0 ? t.Substring(dot + 1) : t;
        }

        // The UnityEvent field name a listener path belongs to: "...data[6].onEnter.m_PersistentCalls..." -> "onEnter".
        static string EventField(string path)
        {
            int pc = path.IndexOf(".m_PersistentCalls", System.StringComparison.Ordinal);
            if (pc < 0) return "event";
            string before = path.Substring(0, pc);
            int dot = before.LastIndexOf('.');
            return dot >= 0 ? before.Substring(dot + 1) : before;
        }

        // The zero-padded listener-row index, matching what the inspector shows ("05").
        static string Slot(string callPath)
        {
            var m = Regex.Match(callPath, @"m_Calls\.Array\.data\[(\d+)\]");
            return m.Success
                ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture).ToString("00", CultureInfo.InvariantCulture)
                : "?";
        }

        // Last path segment ("steps.Array.data[6].nextGuid" -> "nextGuid"), for naming a field plainly.
        static string FieldLeaf(string path)
        {
            int dot = path.LastIndexOf('.');
            return dot >= 0 ? path.Substring(dot + 1) : path;
        }

        // A guid is noise in a sentence; show enough to identify without dumping 36 chars.
        static string Shorten(string guid)
            => string.IsNullOrEmpty(guid) ? "(empty)" : (guid.Length <= 12 ? guid : guid.Substring(0, 8) + "...");

        // -------------------------------------------------------------------------------------
        // Snapshot (deterministic JSON). Catches a DROPPED or SILENTLY REWIRED step/reference/listener
        // the invariants would pass. Three sorted maps: routing (guid topology), objectRefs (stable
        // identities), events (persistent-call fingerprint). Stable order, InvariantCulture, LF,
        // trailing newline - so a byte-diff is meaningful.
        // -------------------------------------------------------------------------------------
        public static string BuildSnapshotJson(Scenario scenario)
        {
            var routing = new SortedDictionary<string, string>(System.StringComparer.Ordinal);
            var objectRefs = new SortedDictionary<string, string>(System.StringComparer.Ordinal);
            var events = new SortedDictionary<string, string>(System.StringComparer.Ordinal);

            Transform fixtureRoot = scenario != null ? scenario.transform.root : null;

            if (scenario != null)
            {
                var so = new SerializedObject(scenario);
                var stepsRoot = so.FindProperty("steps");
                if (stepsRoot != null)
                {
                    Walk(stepsRoot, p =>
                    {
                        string path = p.propertyPath;

                        if (p.propertyType == SerializedPropertyType.String && IsRoutingField(p.name, path))
                        {
                            routing[path] = p.stringValue ?? "";
                        }
                        else if (p.propertyType == SerializedPropertyType.ObjectReference)
                        {
                            objectRefs[path] = StableId(p, fixtureRoot);
                        }
                        else if (IsUnderPersistentCall(path) && IsEventFingerprintLeaf(p))
                        {
                            events[path] = LeafValue(p);
                        }
                    });
                }
            }

            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"schemaVersion\": ").Append(SchemaVersion).Append(",\n");
            AppendMap(sb, "routing", routing, last: false);
            AppendMap(sb, "objectRefs", objectRefs, last: false);
            AppendMap(sb, "events", events, last: true);
            sb.Append("}\n");
            return sb.ToString();
        }

        // -------------------------------------------------------------------------------------
        // Generic subtree walk: visits every serialized property under 'root' (array elements, nested
        // managed references, nested lists/arrays) via Next(enterChildren:true) bounded by GetEndProperty.
        // -------------------------------------------------------------------------------------
        static void Walk(SerializedProperty root, System.Action<SerializedProperty> visit)
        {
            var p = root.Copy();
            var end = root.GetEndProperty();
            bool enterChildren = true;
            while (p.Next(enterChildren))
            {
                if (SerializedProperty.EqualContents(p, end)) break;
                enterChildren = true;
                visit(p);
            }
        }

        // ---- path/field classifiers ---------------------------------------------------------

        static bool IsRoutingField(string name, string path)
        {
            if (name == "specificStepGuid") return true;
            if (name == "nextGuid") return true;
            if (name != null && name.EndsWith("NextGuid", System.StringComparison.Ordinal)) return true;
            // A ChildRequirement.guid references a child step guid (must resolve like a route).
            if (name == "guid" && IsUnderRequirementList(path)) return true;
            return false;
        }

        static bool IsUnderRequirementList(string path)
            => path != null && path.Contains("childRequirements");

        static bool IsUnderPersistentCall(string path)
            => path != null && path.Contains("m_PersistentCalls");

        // True when the property is an element directly inside a list/array named "steps"
        // (root or nested GroupStep.steps), i.e. a candidate Step slot.
        static bool IsStepsArrayElement(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            // e.g. "steps.Array.data[2]" or "steps.Array.data[1].steps.Array.data[0]"
            int idx = path.LastIndexOf(".Array.data[", System.StringComparison.Ordinal);
            if (idx < 0) return false;
            if (!path.EndsWith("]", System.StringComparison.Ordinal)) return false;
            string before = path.Substring(0, idx);
            return before == "steps" || before.EndsWith(".steps", System.StringComparison.Ordinal);
        }

        // ---- snapshot value formatters ------------------------------------------------------

        static bool IsEventFingerprintLeaf(SerializedProperty p)
        {
            switch (p.propertyType)
            {
                case SerializedPropertyType.String:
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.Boolean:
                case SerializedPropertyType.Float:
                case SerializedPropertyType.Enum:
                    return true;
                default:
                    return false;   // ObjectReference targets are recorded in objectRefs
            }
        }

        static string LeafValue(SerializedProperty p)
        {
            switch (p.propertyType)
            {
                case SerializedPropertyType.String: return p.stringValue ?? "";
                case SerializedPropertyType.Integer: return p.longValue.ToString(CultureInfo.InvariantCulture);
                case SerializedPropertyType.Boolean: return p.boolValue ? "true" : "false";
                case SerializedPropertyType.Float: return p.doubleValue.ToString("R", CultureInfo.InvariantCulture);
                case SerializedPropertyType.Enum: return p.enumValueIndex.ToString(CultureInfo.InvariantCulture);
                default: return "";
            }
        }

        static string StableId(SerializedProperty objRefProp, Transform fixtureRoot)
        {
            // Dangling pointer.
            if (objRefProp.objectReferenceValue == null)
                return objRefProp.objectReferenceInstanceIDValue != 0 ? "MISSING" : "null";

            Object obj = objRefProp.objectReferenceValue;
            Transform t = null;
            if (obj is GameObject go) t = go.transform;
            else if (obj is Component comp) t = comp.transform;

            string typeName = obj.GetType().Name;
            if (t != null)
            {
                string id = HierarchyPath(t, fixtureRoot) + " :: " + typeName;
                // Same-type components on one GameObject: append the component's index so a rewire
                // from one to another changes the snapshot.
                if (obj is Component c)
                {
                    var sameType = c.gameObject.GetComponents(c.GetType());
                    if (sameType.Length > 1)
                        id += "#" + System.Array.IndexOf(sameType, c);
                }
                return id;
            }

            string assetPath = AssetDatabase.GetAssetPath(obj);
            if (!string.IsNullOrEmpty(assetPath))
                return "asset:" + assetPath + " :: " + typeName;

            return "obj:" + obj.name + " :: " + typeName;
        }

        // Name path with sibling indices: "Panel[0]/Btn_Yes[2]". Unity enforces neither unique sibling
        // names nor single same-type components, so bare names would make a rewire between same-named
        // siblings snapshot-invisible (a Proof A false green).
        static string HierarchyPath(Transform t, Transform root)
        {
            var stack = new List<string>();
            var cur = t;
            while (cur != null && cur != root)
            {
                stack.Add(cur.name + "[" + cur.GetSiblingIndex() + "]");
                cur = cur.parent;
            }
            stack.Reverse();
            return stack.Count == 0 ? "<root>" : string.Join("/", stack);
        }

        static void AppendMap(StringBuilder sb, string key, SortedDictionary<string, string> map, bool last)
        {
            sb.Append("  \"").Append(key).Append("\": {");
            if (map.Count == 0)
            {
                sb.Append("}");
            }
            else
            {
                sb.Append('\n');
                int i = 0;
                foreach (var kv in map)
                {
                    sb.Append("    ").Append(JsonStr(kv.Key)).Append(": ").Append(JsonStr(kv.Value));
                    sb.Append(++i < map.Count ? ",\n" : "\n");
                }
                sb.Append("  }");
            }
            sb.Append(last ? "\n" : ",\n");
        }

        static string JsonStr(string s)
        {
            var sb = new StringBuilder(s.Length + 2);
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default: sb.Append(c); break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }
    }
}
#endif
