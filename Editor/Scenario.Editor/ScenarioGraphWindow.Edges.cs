#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Pitech.XR.Scenario;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UIElements;
using Pitech.XR.Interactables;

// Avoid Button clash (UGUI vs UIElements)
using UGUIButton = UnityEngine.UI.Button;
using UIEButton = UnityEngine.UIElements.Button;

public partial class ScenarioGraphWindow
{
    // ======== metadata kept on ports ========
    sealed class PortMeta
    {
        public Step owner;
        public int choiceIndex; // -1 => "Next"
        public static PortMeta From(Port p) => p?.userData as PortMeta;
        public static void Set(Port p, Step owner, int choiceIndex) => p.userData = new PortMeta { owner = owner, choiceIndex = choiceIndex };
    }

    // ---- Edge with moving "flow" marker ----
    // ---- Edge with moving glowing "light" segment ----
    // ---- Edge with moving glowing "light" segment ----
    class FlowEdge : Edge
    {
        float glowT;
        bool playing;

        const float SegmentLength = 0.18f;   // length of glow along the curve
        const float GlowWidth = 6f;      // thickness of glow

        // we cache whatever drawing the base Edge already had
        readonly Action<MeshGenerationContext> _baseGenerate;

        public FlowEdge()
        {
            // Cache existing generator then replace with our own wrapper
            _baseGenerate = this.generateVisualContent;
            this.generateVisualContent = OnGenerate;
        }

        public void PlayFlow()
        {
            glowT = 0f;
            playing = true;

            // simple ~0.5s animation (~60fps)
            this.schedule.Execute(_ =>
            {
                if (!playing)
                    return;

                glowT += 0.03f;
                if (glowT >= 1f)
                {
                    glowT = 1f;
                    playing = false;
                }

                MarkDirtyRepaint();

            }).Every(16).Until(() => !playing);
        }

        void OnGenerate(MeshGenerationContext ctx)
        {
            // draw normal edge first
            _baseGenerate?.Invoke(ctx);

            if (!playing || edgeControl == null)
                return;

            var cps = edgeControl.controlPoints;   // IList<Vector2> in GraphView
            if (cps == null || cps.Length < 4)
                return;

            DrawGlow(ctx, cps, glowT);
        }

        void DrawGlow(MeshGenerationContext ctx, System.Collections.Generic.IList<Vector2> cps, float tCenter)
        {
            if (cps == null || cps.Count < 4)
                return;

            float t0 = Mathf.Clamp01(tCenter - SegmentLength * 0.5f);
            float t1 = Mathf.Clamp01(tCenter + SegmentLength * 0.5f);
            if (t1 <= t0)
                return;

            const int segments = 8;
            int vertCount = segments * 4;
            int idxCount = segments * 6;

            var mesh = ctx.Allocate(vertCount, idxCount);
            var color = new Color(0.3f, 0.8f, 1f, 0.85f);   // cyan-ish glow

            for (int i = 0; i < segments; i++)
            {
                float tt0 = Mathf.Lerp(t0, t1, (float)i / segments);
                float tt1 = Mathf.Lerp(t0, t1, (float)(i + 1) / segments);

                Vector2 p0 = CubicBezier(cps[0], cps[1], cps[2], cps[3], tt0);
                Vector2 p1 = CubicBezier(cps[0], cps[1], cps[2], cps[3], tt1);

                Vector2 dir = (p1 - p0);
                if (dir.sqrMagnitude < 1e-6f)
                    continue;
                dir.Normalize();

                Vector2 n = new Vector2(-dir.y, dir.x);
                float halfW = GlowWidth * 0.5f;

                float fade = 1f - (float)i / segments;
                Color c = color;
                c.a *= fade;

                // 4 verts per segment
                mesh.SetNextVertex(new Vertex { position = p0 + n * halfW, tint = c });
                mesh.SetNextVertex(new Vertex { position = p0 - n * halfW, tint = c });
                mesh.SetNextVertex(new Vertex { position = p1 - n * halfW, tint = c });
                mesh.SetNextVertex(new Vertex { position = p1 + n * halfW, tint = c });

                int baseIndex = i * 4;
                mesh.SetNextIndex((ushort)(baseIndex + 0));
                mesh.SetNextIndex((ushort)(baseIndex + 1));
                mesh.SetNextIndex((ushort)(baseIndex + 2));
                mesh.SetNextIndex((ushort)(baseIndex + 0));
                mesh.SetNextIndex((ushort)(baseIndex + 2));
                mesh.SetNextIndex((ushort)(baseIndex + 3));
            }
        }

        static Vector2 CubicBezier(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float u = 1f - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;

            Vector2 p = uuu * p0;
            p += 3f * uu * t * p1;
            p += 3f * u * tt * p2;
            p += ttt * p3;
            return p;
        }
    }
}
#endif
