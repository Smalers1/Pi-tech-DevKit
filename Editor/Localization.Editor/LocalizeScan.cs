using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using Pitech.XR.Localization;

namespace Pitech.XR.Localization.Editor
{
    // ---------- [Localize] data-asset scan (WS B2.5) ----------
    // The keying-coverage extension the map's "E work" calls for: the VR scene scanner only finds scene
    // TMP_Text; it misses DATA-ASSET text (QuizAsset prompts/answers/explanations) and code literals.
    // This tool reflects over [Localize]-marked string fields on ScriptableObject assets (recursing into
    // [Serializable] nested classes + lists - e.g. QuizAsset.questions[].answers[].text) and collects
    // (key, sourceText). Key = the explicit [Localize("key")] if set, else auto-derived from the
    // declaring type + member (matching the attribute's intent).
    //
    // Output: logs a summary and writes a JSON list (key, source, where) you can feed to the translate
    // round-trip. RUNNING this on the real labs + baking the StringTables is the post-B2 pipeline step;
    // the tool itself is delivered now (DevKit-side, no dependency on the VR pipeline).

    public static class LocalizeScan
    {
        const int MaxDepth = 6;

        [Serializable]
        public sealed class Entry
        {
            public string key;
            public string source;
            public string where;
        }

        [MenuItem("Pi tech/Localization/Scan [Localize] fields in assets")]
        public static void ScanMenu()
        {
            List<Entry> entries = ScanAllScriptableObjects();
            string json = ToJson(entries);
            string path = EditorUtility.SaveFilePanel("Save [Localize] scan", Application.dataPath, "localize-scan", "json");
            if (!string.IsNullOrEmpty(path))
            {
                System.IO.File.WriteAllText(path, json);
                Debug.Log($"[Localization] Scanned {entries.Count} [Localize] field(s) -> {path}");
            }
            else
            {
                Debug.Log($"[Localization] Scanned {entries.Count} [Localize] field(s) (not saved):\n{json}");
            }
        }

        /// <summary>Scan every ScriptableObject asset in the project for [Localize]-marked strings.</summary>
        public static List<Entry> ScanAllScriptableObjects()
        {
            var entries = new List<Entry>();
            string[] guids = AssetDatabase.FindAssets("t:ScriptableObject");
            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                var so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
                if (so == null) continue;
                Collect(so, so.GetType(), assetPath, entries, 0, new HashSet<object>());
            }
            return entries;
        }

        static void Collect(object obj, Type type, string where, List<Entry> entries, int depth, HashSet<object> seen)
        {
            if (obj == null || depth > MaxDepth) return;
            if (!type.IsValueType)
            {
                if (seen.Contains(obj)) return;
                seen.Add(obj);
            }

            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo f = fields[i];
                if (!IsSerializedLike(f)) continue;
                object value = f.GetValue(obj);

                if (f.FieldType == typeof(string))
                {
                    var attr = f.GetCustomAttribute<LocalizeAttribute>();
                    if (attr != null)
                    {
                        string key = !string.IsNullOrEmpty(attr.Key) ? attr.Key : DeriveKey(type, f.Name);
                        entries.Add(new Entry { key = key, source = value as string ?? string.Empty, where = where });
                    }
                    continue;
                }

                if (value is IList list && !(value is string))
                {
                    Type elem = ElementType(f.FieldType);
                    if (elem != null && IsWalkable(elem))
                        foreach (object item in list)
                            Collect(item, item != null ? item.GetType() : elem, where, entries, depth + 1, seen);
                    continue;
                }

                if (IsWalkable(f.FieldType) && value != null)
                    Collect(value, value.GetType(), where, entries, depth + 1, seen);
            }
        }

        static bool IsSerializedLike(FieldInfo f)
        {
            if (f.IsStatic) return false;
            if (f.IsPublic) return !f.IsNotSerialized;
            return f.GetCustomAttribute<SerializeField>() != null;
        }

        // Walk only plain [Serializable] managed types - never UnityEngine.Object refs (would crawl the
        // whole project/scene).
        static bool IsWalkable(Type t)
        {
            if (t == null) return false;
            if (typeof(UnityEngine.Object).IsAssignableFrom(t)) return false;
            if (t.IsPrimitive || t.IsEnum || t == typeof(string)) return false;
            if (t.IsClass) return t.GetCustomAttribute<SerializableAttribute>() != null;
            if (t.IsValueType) return t.GetCustomAttribute<SerializableAttribute>() != null && !t.IsPrimitive;
            return false;
        }

        static Type ElementType(Type listType)
        {
            if (listType.IsArray) return listType.GetElementType();
            if (listType.IsGenericType && listType.GetGenericTypeDefinition() == typeof(List<>))
                return listType.GetGenericArguments()[0];
            return null;
        }

        static string DeriveKey(Type declaring, string member)
        {
            string t = declaring != null ? declaring.Name : "type";
            return (t + "." + member).ToLowerInvariant();
        }

        static string ToJson(List<Entry> entries)
        {
            var sb = new StringBuilder();
            sb.Append("[\n");
            for (int i = 0; i < entries.Count; i++)
            {
                Entry e = entries[i];
                sb.Append("  {\"key\":").Append(Quote(e.key))
                  .Append(",\"source\":").Append(Quote(e.source))
                  .Append(",\"where\":").Append(Quote(e.where)).Append('}');
                if (i < entries.Count - 1) sb.Append(',');
                sb.Append('\n');
            }
            sb.Append(']');
            return sb.ToString();
        }

        static string Quote(string s)
        {
            if (s == null) return "null";
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
