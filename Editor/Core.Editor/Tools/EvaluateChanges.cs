#if PITECH_HAS_TESTFRAMEWORK
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pitech.XR.Core.Editor
{
    /// <summary>WS A3 Step 8 / Appendix I.11 - the one-click manual gate. Runs the EditMode net via the
    /// shared <see cref="DevKitChecks.RunEditModeGate"/> and shows a plain-language verdict:
    /// green "N checks passed - safe to push", or one sentence per failure. Same handler as the Hub
    /// Maintain button. Menu priority 22 keeps it in the Tools group (per the WS A2 priority scheme).
    /// Gated on PITECH_HAS_TESTFRAMEWORK like <see cref="DevKitChecks"/>.</summary>
    public sealed class EvaluateChanges : EditorWindow
    {
        static readonly Color Green = new Color(0.46f, 0.80f, 0.50f);
        static readonly Color Red = new Color(0.92f, 0.45f, 0.45f);

        Label _verdict;
        VisualElement _details;
        Button _runButton;
        bool _running;

        [MenuItem("Pi tech/Tools/Evaluate Changes", false, 22)]
        public static void Open()
        {
            var w = GetWindow<EvaluateChanges>();
            w.titleContent = new GUIContent("Evaluate Changes");
            w.minSize = new Vector2(580, 380);
            w.Show();
            // Defer one tick so CreateGUI has built the UI before the auto-run renders into it.
            EditorApplication.delayCall += () => { if (w != null) w.Run(); };
        }

        void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.backgroundColor = DevkitTheme.Bg;

            var section = DevkitTheme.Section("Evaluate Changes (the gate)");
            section.style.paddingLeft = 14; section.style.paddingRight = 14;
            section.style.paddingTop = 12; section.style.paddingBottom = 12;
            section.Add(DevkitTheme.Body(
                "Runs the DevKit EditMode safety net (Proof A graph integrity, Proof B public API + type "
                + "literals, Proof C serialized/GUID stability, pure-logic locks). Push only on green.",
                dim: true));
            section.Add(DevkitTheme.Body(
                "Coverage: " + string.Join(" + ", DevKitChecks.CoveredAssemblies) + ". The AgentSubstrate "
                + "suite is excluded until its known pre-existing failure is resolved by its owner - run it "
                + "via Window > General > Test Runner.",
                dim: true));
            section.Add(DevkitTheme.VSpace(10));

            _verdict = new Label("Ready.")
            {
                style = { unityFontStyleAndWeight = FontStyle.Bold, whiteSpace = WhiteSpace.Normal, fontSize = 14 }
            };
            _verdict.style.color = DevkitTheme.Text;
            section.Add(_verdict);
            section.Add(DevkitTheme.VSpace(8));

            _runButton = DevkitTheme.Primary("Run again", Run);
            section.Add(_runButton);
            section.Add(DevkitTheme.VSpace(10));

            var scroll = new ScrollView();
            scroll.style.flexGrow = 1;
            _details = scroll;
            section.Add(scroll);

            root.Add(section);
        }

        void Run()
        {
            if (_running) return;
            _running = true;
            if (_details != null) _details.Clear();
            if (_verdict != null) { _verdict.text = "Running the EditMode net..."; _verdict.style.color = DevkitTheme.Text; }
            if (_runButton != null) _runButton.SetEnabled(false);

            DevKitChecks.RunEditModeGate(OnComplete);
        }

        void OnComplete(DevKitGateResult r)
        {
            _running = false;
            if (_runButton != null) _runButton.SetEnabled(true);
            if (_verdict == null || _details == null) return;

            _verdict.text = DevKitChecks.Summarize(r);
            _verdict.style.color = r.Green ? Green : Red;   // zero-run reads red via Green=false

            _details.Clear();
            if (!r.Green && !r.RanNothing)
            {
                _details.Add(Header("Failures"));
                foreach (var f in r.failures)
                    _details.Add(Line("- " + f, Red));
            }
            if (r.inconclusive > 0)
            {
                _details.Add(DevkitTheme.VSpace(6));
                _details.Add(Header("Inconclusive (net not yet enforcing - usually means fixtures/baselines pending)"));
                foreach (var n in r.inconclusiveNames)
                    _details.Add(Line("- " + n, DevkitTheme.Text));
            }
            _details.Add(DevkitTheme.VSpace(8));
            _details.Add(Line($"Totals: {r.passed} passed, {r.failed} failed, {r.inconclusive} inconclusive, {r.skipped} skipped.", DevkitTheme.Text));
        }

        static Label Header(string text)
        {
            var l = new Label(text) { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 4, marginBottom = 2 } };
            l.style.color = DevkitTheme.Text;
            return l;
        }

        static Label Line(string text, Color color)
        {
            var l = new Label(text) { style = { whiteSpace = WhiteSpace.Normal, marginBottom = 1 } };
            l.style.color = color;
            return l;
        }
    }
}
#endif
