using System;
using System.Collections.Generic;

namespace Project.Core.Runtime.Framework
{
    public static class Services
    {
        private static readonly Dictionary<Type, object> Registry = new();

        public static void Register<T>(T service) where T : class
        {
            if (service == null)
            {
                return;
            }

            Registry[typeof(T)] = service;
            Registry[service.GetType()] = service;
        }

        public static void Register(Type serviceType, object service)
        {
            if (serviceType == null || service == null)
            {
                return;
            }

            Registry[serviceType] = service;
            Registry[service.GetType()] = service;
        }

        public static bool TryGet<T>(out T service) where T : class
        {
            if (Registry.TryGetValue(typeof(T), out var instance) && instance is T typed)
            {
                service = typed;
                return true;
            }

            foreach (var value in Registry.Values)
            {
                if (value is T assignable)
                {
                    service = assignable;
                    return true;
                }
            }

            service = null;
            return false;
        }

        public static T Get<T>() where T : class
        {
            if (TryGet<T>(out var service))
            {
                return service;
            }

            throw new InvalidOperationException($"Service not registered: {typeof(T).Name}");
        }

        public static void Unregister<T>() where T : class
        {
            Registry.Remove(typeof(T));
        }

        public static void Unregister(Type serviceType)
        {
            if (serviceType == null)
            {
                return;
            }

            Registry.Remove(serviceType);
        }

        public static void UnregisterInstance(object service)
        {
            if (service == null)
            {
                return;
            }

            var keysToRemove = new List<Type>();
            foreach (var pair in Registry)
            {
                if (ReferenceEquals(pair.Value, service))
                {
                    keysToRemove.Add(pair.Key);
                }
            }

            foreach (var key in keysToRemove)
            {
                Registry.Remove(key);
            }
        }

        public static void Clear()
        {
            Registry.Clear();
        }
    }
}
