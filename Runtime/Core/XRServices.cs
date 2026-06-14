namespace Pitech.XR.Core
{
    /// <summary>
    /// A host-provided capability the DevKit consumes through <see cref="XRServices"/>. Implementers own a
    /// lifecycle: <see cref="Initialize"/> on registration/boot, <see cref="Shutdown"/> on teardown.
    /// </summary>
    public interface IXRService { void Initialize(); void Shutdown(); }

    /// <summary>
    /// Minimal service locator: the host registers capability implementations by type and the DevKit
    /// resolves them by type. Keeps runtime package code free of <c>FindObjectsOfType</c>/singletons.
    /// </summary>
    public static class XRServices
    {
        static readonly System.Collections.Generic.Dictionary<System.Type, IXRService> map = new();

        /// <summary>Register (or replace) the implementation for service type <typeparamref name="T"/>.</summary>
        public static void Register<T>(T impl) where T : class, IXRService => map[typeof(T)] = impl;

        /// <summary>Resolve the implementation for <typeparamref name="T"/>, or <c>null</c> if none is registered.</summary>
        public static T Get<T>() where T : class, IXRService => map.TryGetValue(typeof(T), out var s) ? (T)s : null;

        /// <summary>Try to resolve the implementation for <typeparamref name="T"/>; returns false (and <c>null</c>) when absent.</summary>
        public static bool TryGet<T>(out T svc) where T: class, IXRService { svc = Get<T>(); return svc != null; }

        /// <summary>Call <see cref="IXRService.Initialize"/> on every registered service.</summary>
        public static void InitializeAll(){ foreach (var s in map.Values) s.Initialize(); }

        /// <summary>Call <see cref="IXRService.Shutdown"/> on every registered service, then clear the registry.</summary>
        public static void ShutdownAll(){ foreach (var s in map.Values) s.Shutdown(); map.Clear(); }
    }
}
