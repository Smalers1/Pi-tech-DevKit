using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Pitech.XR.Analytics;
using Pitech.XR.Scenario;
using Pitech.XR.Interactables;

namespace Pitech.XR.Analytics.Editor
{
    // 'Scenario' as a bare name binds to the Pitech.XR.Scenario NAMESPACE here (the enclosing Pitech.XR
    // sees the child namespace before the using-import resolves the type). Alias it to the type; this MUST
    // live inside the namespace block to win over that enclosing-namespace match.
    using Scenario = Pitech.XR.Scenario.Scenario;

    // ---------- LabAnalytics inspector: auto-detect + auto-wire (map sec-11.2, WS B2.2 S2/S3) ----------
    // Two convenience buttons over the default inspector:
    //   * Auto-detect subjects: scan the lab's Scenario for InsertStep items AND SelectionStep correct
    //     targets, and pre-fill the subjects registry (id + label + target + ownerStepGuid = the step).
    //     An InsertStep contributes its single item; a SelectionStep contributes the correct colliders of
    //     the list it tests (resolved by listIndex, else listKey). Distractors / free grabbables are
    //     added by hand (the map's stated split).
    //   * Auto-wire: add an AnalyticsSubject to each subject's target object and stamp its subjectId, so
    //     the author only has to hook the grab/drop/use UnityEvents (or use the below-Y drop check).
    // Edits go through Undo so they are reversible and mark the scene/prefab dirty correctly.

    [CustomEditor(typeof(LabAnalytics))]
    public sealed partial class LabAnalyticsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            // Draw the wiring fields (role selector, sink, output events) but NOT the raw 'config' - the config has
            // its own purpose-built builder below (DrawConfigBuilder), so drawing it raw too is duplicate clutter.
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script", "config");
            serializedObject.ApplyModifiedProperties();

            var la = (LabAnalytics)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Authoring helpers (WS B2.2)", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Auto-detect Tracked Objects",
                    "Pre-fill the Tracked Objects from the lab's Scenario (InsertStep items + SelectionStep correct targets).")))
                {
                    AutoDetect(la);
                }

                if (GUILayout.Button(new GUIContent("Auto-wire components",
                    "Add an AnalyticsSubject to each subject's target object and set its subjectId.")))
                {
                    AutoWire(la);
                }
            }

            EditorGUILayout.HelpBox(
                "Auto-detect adds InsertStep items and SelectionStep correct targets as relevant Tracked Objects (owner = that step). Add distractors / free grabbables by hand, then Auto-wire.",
                MessageType.Info);

            DrawConfigBuilder();
        }

        static Scenario ResolveScenario(LabAnalytics la)
        {
            if (la == null) return null;
            // LabAnalytics now lives on a dedicated "Analytics" object that is a SIBLING of the LabConsole
            // (next to it, NOT a child), so self/parent lookups miss it. After the cheap co-located / ancestor
            // cases, resolve the console anywhere under the lab ROOT - the common ancestor of the two siblings.
            var console = la.GetComponent<LabConsole>();
            if (console == null) console = la.GetComponentInParent<LabConsole>(true);
            if (console == null) console = la.transform.root.gameObject.GetComponentInChildren<LabConsole>(true);
            return console != null ? console.scenario : null;
        }

        static void AutoDetect(LabAnalytics la)
        {
            Scenario scenario = ResolveScenario(la);
            if (scenario == null || scenario.steps == null)
            {
                EditorUtility.DisplayDialog("Auto-detect subjects",
                    "No Scenario found for this lab (searched the LabConsole on this object, its parents, and the lab root). " +
                    "Assign a Scenario on the lab's LabConsole first.", "OK");
                return;
            }
            if (la.config == null) la.config = new LabConfig();

            // Index existing subjects by target + by id so re-runs are idempotent.
            var byTarget = new HashSet<GameObject>();
            var ids = new HashSet<string>();
            for (int i = 0; i < la.config.subjects.Count; i++)
            {
                TrackedSubject s = la.config.subjects[i];
                if (s == null) continue;
                if (s.target != null) byTarget.Add(s.target);
                if (!string.IsNullOrEmpty(s.id)) ids.Add(s.id);
            }

            int added = 0;       // from InsertSteps
            int addedSel = 0;    // from SelectionStep correct targets
            Undo.RegisterCompleteObjectUndo(la, "Auto-detect analytics subjects");

            // InsertStep -> its single inserted item (owner = that step).
            for (int i = 0; i < scenario.steps.Count; i++)
            {
                if (!(scenario.steps[i] is InsertStep ins) || ins.item == null) continue;
                if (TryAddSubject(la, ins.item.gameObject, ins.guid, byTarget, ids)) added++;
            }

            // SelectionStep -> the CORRECT colliders of the list it tests (owner = that step). The list is
            // resolved the same way the runtime does: listIndex when set (>= 0), else by listKey (name).
            // Distractors (the wrong selectables) are intentionally NOT auto-added - the recorder treats an
            // unregistered/non-relevant selection as the distractor case; the author adds any they want to
            // track by hand, matching the InsertStep "distractors by hand" split.
            for (int i = 0; i < scenario.steps.Count; i++)
            {
                if (!(scenario.steps[i] is SelectionStep sel) || sel.lists == null) continue;
                SelectionList list = ResolveList(sel.lists, sel.listIndex, sel.listKey);
                if (list == null || list.correct == null) continue;
                for (int c = 0; c < list.correct.Count; c++)
                {
                    Collider col = list.correct[c];
                    if (col == null) continue;
                    if (TryAddSubject(la, col.gameObject, sel.guid, byTarget, ids)) addedSel++;
                }
            }

            EditorUtility.SetDirty(la);
            Debug.Log($"[Analytics] Auto-detect added {added} subject(s) from InsertSteps and {addedSel} from SelectionStep correct targets.", la);
        }

        // Add a relevant subject for a target GameObject, deduped by target + by id. Returns false if the
        // target is null or already registered. Caller owns the Undo bracket + SetDirty.
        static bool TryAddSubject(LabAnalytics la, GameObject go, string ownerStepGuid,
            HashSet<GameObject> byTarget, HashSet<string> ids)
        {
            if (go == null || byTarget.Contains(go)) return false;
            string id = UniqueId(go.name, ids);
            la.config.subjects.Add(new TrackedSubject
            {
                id = id,
                label = go.name,
                target = go,
                scenarioRelevant = true,
                ownerStepGuid = ownerStepGuid
            });
            ids.Add(id);
            byTarget.Add(go);
            return true;
        }

        // Resolve the SelectionList a SelectionStep tests: by index when set (matches SelectionLists'
        // index-or-name contract), else by name (listKey). Null if neither resolves.
        static SelectionList ResolveList(SelectionLists lists, int index, string key)
        {
            if (lists == null || lists.lists == null) return null;
            if (index >= 0 && index < lists.lists.Count) return lists.lists[index];
            if (!string.IsNullOrEmpty(key))
                for (int i = 0; i < lists.lists.Count; i++)
                    if (lists.lists[i] != null && lists.lists[i].name == key) return lists.lists[i];
            return null;
        }

        static void AutoWire(LabAnalytics la)
        {
            if (la.config == null || la.config.subjects == null) return;

            int wired = 0;
            for (int i = 0; i < la.config.subjects.Count; i++)
            {
                TrackedSubject s = la.config.subjects[i];
                if (s == null || s.target == null || string.IsNullOrEmpty(s.id)) continue;

                AnalyticsSubject agent = s.target.GetComponent<AnalyticsSubject>();
                if (agent == null) agent = Undo.AddComponent<AnalyticsSubject>(s.target);

                if (agent.subjectId != s.id)
                {
                    Undo.RecordObject(agent, "Wire AnalyticsSubject id");
                    agent.subjectId = s.id;
                    EditorUtility.SetDirty(agent);
                }
                wired++;
            }

            Debug.Log($"[Analytics] Auto-wire ensured AnalyticsSubject on {wired} subject target(s).", la);
        }

        static string UniqueId(string baseName, HashSet<string> taken)
        {
            string id = Sanitize(baseName);
            if (string.IsNullOrEmpty(id)) id = "subject";
            if (!taken.Contains(id)) return id;
            for (int n = 2; ; n++)
            {
                string candidate = id + "_" + n;
                if (!taken.Contains(candidate)) return candidate;
            }
        }

        static string Sanitize(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            var sb = new System.Text.StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
                else if (c == ' ' || c == '_' || c == '-') sb.Append('_');
            }
            return sb.ToString();
        }
    }
}
