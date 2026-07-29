using System;
using System.Collections.Generic;

namespace Century.Core
{
    /// <summary>
    /// Minimal registry for long-lived services, used so that scene-local objects can reach
    /// campaign state without singletons scattered through the codebase or hard assembly
    /// references between the campaign and battle layers.
    /// </summary>
    /// <remarks>
    /// This is deliberately small. If the project grows past a dozen services, replace it with a
    /// proper container (VContainer is the usual choice) — call sites already ask for an interface,
    /// so the swap is mechanical.
    /// </remarks>
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> Services = new Dictionary<Type, object>();

        public static void Register<T>(T service) where T : class
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            Services[typeof(T)] = service;
        }

        public static void Unregister<T>() where T : class => Services.Remove(typeof(T));

        public static T Get<T>() where T : class
        {
            if (Services.TryGetValue(typeof(T), out object service)) return (T)service;
            throw new InvalidOperationException(
                $"No service of type {typeof(T).Name} is registered. " +
                "Enter play mode from the Boot scene so GameDirector can register it.");
        }

        public static bool TryGet<T>(out T service) where T : class
        {
            if (Services.TryGetValue(typeof(T), out object found))
            {
                service = (T)found;
                return true;
            }

            service = null;
            return false;
        }

        public static void Clear() => Services.Clear();
    }
}
