using System.Reflection;
using UnityEngine;

namespace Pitech.XR.ContentDelivery
{
    /// <summary>
    /// Reflection helpers that drive a scene runner referenced as a bare <see cref="MonoBehaviour"/>
    /// (so ContentDelivery needs no compile-time dependency on the Scenario assembly): toggling its
    /// <c>autoStart</c> field/property and invoking its parameterless <c>Restart()</c> method. Shared by
    /// <see cref="AddressablesBootstrapper"/> and <see cref="ContentDeliverySpawner"/>.
    /// </summary>
    internal static class SceneRunnerReflection
    {
        internal static void TrySetAutoStart(MonoBehaviour target, bool value)
        {
            if (target == null)
            {
                return;
            }

            var type = target.GetType();
            FieldInfo field = type.GetField("autoStart", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null && field.FieldType == typeof(bool))
            {
                field.SetValue(target, value);
                return;
            }

            PropertyInfo prop = type.GetProperty("autoStart", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop != null && prop.PropertyType == typeof(bool) && prop.CanWrite)
            {
                prop.SetValue(target, value);
            }
        }

        internal static void TryRestart(MonoBehaviour target)
        {
            if (target == null)
            {
                return;
            }

            MethodInfo restart = target.GetType().GetMethod(
                "Restart",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            restart?.Invoke(target, null);
        }
    }
}
