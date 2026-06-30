#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pitech.XR.Core.Editor
{
    /// <summary>
    /// Scene-agnostic helpers for finding/creating and wiring DevKit scene objects.
    /// Uses reflection to avoid runtime asmdef references.
    /// </summary>
    internal sealed class GuidedSetupService
    {
        const string ManagersRootName = SceneRootNames.ManagersRoot;   // canonical name for CREATE
        static readonly Dictionary<string, Type> TypeCache = new Dictionary<string, Type>();

        readonly Dictionary<string, Component> firstSceneComponentByType = new Dictionary<string, Component>();
        readonly List<Component> sceneComponents = new List<Component>();
        bool sceneComponentIndexBuilt;
        int sceneComponentIndexSceneHandle;

        public Scene ActiveScene => SceneManager.GetActiveScene();

        public bool HasActiveSceneLoaded()
        {
            var s = ActiveScene;
            return s.IsValid() && s.isLoaded;
        }

        /// <summary>
        /// Read-only lookup of the managers root. Returns the existing root's transform, or null
        /// if the active scene has none (or no scene is loaded). Never creates a GameObject,
        /// registers Undo, or marks the scene dirty - safe to call from UI render. Use
        /// <see cref="EnsureManagersRoot"/> from an explicit user action when creation is intended.
        /// </summary>
        public Transform FindManagersRoot()
        {
            var s = ActiveScene;
            if (!s.IsValid() || !s.isLoaded) return null;

            var root = s.GetRootGameObjects().FirstOrDefault(g => SceneRootNames.IsManagersRoot(g.name));
            return root ? root.transform : null;
        }

        public Transform EnsureManagersRoot()
        {
            var s = ActiveScene;
            if (!s.IsValid() || !s.isLoaded) return null;

            var existing = FindManagersRoot();
            if (existing) return existing;

            var root = new GameObject(ManagersRootName);
            Undo.RegisterCreatedObjectUndo(root, "Create Managers Root");
            EditorSceneManager.MarkSceneDirty(s);
            return root.transform;
        }

        public static Type FindType(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return null;
            if (TypeCache.TryGetValue(fullName, out var cached)) return cached;

            var found = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch { return Array.Empty<Type>(); }
                })
                .FirstOrDefault(t => t.FullName == fullName);

            TypeCache[fullName] = found;
            return found;
        }

        public Component FindFirstInScene(string fullTypeName)
        {
            if (string.IsNullOrEmpty(fullTypeName)) return null;

            EnsureSceneComponentIndex();

            // Fast path: an exact concrete-type match was indexed during the one scan (also returns a
            // previously memoized assignable hit).
            if (firstSceneComponentByType.TryGetValue(fullTypeName, out var exact) && exact)
                return exact;

            // Assignable fallback: a SUBCLASS of the requested type counts as present, matching the
            // pre-cache Resources.FindObjectsOfTypeAll(t) semantics the perf rework dropped. Resolve the
            // requested type and return the first scene component it is assignable from, memoizing the
            // hit so repeated render-time queries stay O(1).
            var requestedType = FindType(fullTypeName);
            if (requestedType == null) return null;

            for (int i = 0; i < sceneComponents.Count; i++)
            {
                var component = sceneComponents[i];
                if (component && requestedType.IsInstanceOfType(component))
                {
                    firstSceneComponentByType[fullTypeName] = component;
                    return component;
                }
            }
            return null;
        }

        public Component CreateUnderManagersRoot(string fullTypeName, string goName, string undoName)
        {
            var t = FindType(fullTypeName);
            if (t == null)
            {
                EditorUtility.DisplayDialog("DevKit", $"Type not found: {fullTypeName}", "OK");
                return null;
            }

            var parent = EnsureManagersRoot();
            if (!parent)
            {
                EditorUtility.DisplayDialog("DevKit", "Open a scene first (e.g. Assets/Scenes/Testing).", "OK");
                return null;
            }

            var go = new GameObject(goName);
            Undo.RegisterCreatedObjectUndo(go, undoName);
            go.transform.SetParent(parent, false);
            var comp = go.AddComponent(t) as Component;
            RememberSceneComponent(comp);
            if (comp && sceneComponentIndexBuilt) sceneComponents.Add(comp);
            EditorSceneManager.MarkSceneDirty(go.scene);
            Selection.activeObject = go;
            return comp;
        }

        public void AssignObjectProperty(Component targetComponent, string propertyName, UnityEngine.Object value, string undoName)
        {
            if (!targetComponent) return;

            var so = new SerializedObject(targetComponent);
            var prop = so.FindProperty(propertyName);
            if (prop == null)
            {
                EditorUtility.DisplayDialog("DevKit", $"Property not found: {targetComponent.GetType().Name}.{propertyName}", "OK");
                return;
            }

            Undo.RecordObject(targetComponent, undoName);
            so.Update();
            prop.objectReferenceValue = value;
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(targetComponent);
            EditorSceneManager.MarkSceneDirty(targetComponent.gameObject.scene);
        }

        void EnsureSceneComponentIndex()
        {
            var s = ActiveScene;
            int sceneHandle = s.IsValid() ? s.handle : 0;
            if (sceneComponentIndexBuilt && sceneComponentIndexSceneHandle == sceneHandle)
                return;

            sceneComponentIndexBuilt = true;
            sceneComponentIndexSceneHandle = sceneHandle;
            firstSceneComponentByType.Clear();
            sceneComponents.Clear();

            if (!s.IsValid() || !s.isLoaded) return;

            var components = Resources.FindObjectsOfTypeAll<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (!component || !component.gameObject || component.gameObject.scene != s)
                    continue;

                sceneComponents.Add(component);
                RememberSceneComponent(component);
            }
        }

        void RememberSceneComponent(Component component)
        {
            if (!component) return;

            var fullName = component.GetType().FullName;
            if (string.IsNullOrEmpty(fullName) || firstSceneComponentByType.ContainsKey(fullName))
                return;

            firstSceneComponentByType.Add(fullName, component);
        }
    }
}
#endif


