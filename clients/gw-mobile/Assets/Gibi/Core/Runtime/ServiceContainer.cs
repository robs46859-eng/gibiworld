// GW-ARCH-001 section 4.1 — Bootstrap owns the dependency container.
// Deliberately minimal: constructor-free registration, no reflection, no scanning.
// A heavyweight IoC container would add startup cost against the section 7 budget of
// p95 <= 5 s to interactive home, and hide the dependency graph that section 4 exists
// to keep explicit.
using System;
using System.Collections.Generic;

namespace Gibi.Core
{
    public sealed class ServiceContainer
    {
        private readonly Dictionary<Type, object> _instances = new();
        private readonly Dictionary<Type, Func<object>> _factories = new();
        private bool _sealed;

        public void Register<T>(T instance) where T : class
        {
            Guard();
            _instances[typeof(T)] = instance ?? throw new ArgumentNullException(nameof(instance));
        }

        public void RegisterLazy<T>(Func<T> factory) where T : class
        {
            Guard();
            _factories[typeof(T)] = () => factory();
        }

        /// <summary>
        /// Called once bootstrap completes. Registration after this point indicates a
        /// service being constructed mid-frame, which is a startup-budget bug.
        /// </summary>
        public void Seal() => _sealed = true;

        public T Resolve<T>() where T : class
        {
            var t = typeof(T);
            if (_instances.TryGetValue(t, out var inst)) return (T)inst;
            if (_factories.TryGetValue(t, out var f))
            {
                var created = f();
                _instances[t] = created;
                return (T)created;
            }
            throw new InvalidOperationException(
                $"Service {t.Name} was never registered. Register it in GibiBootstrap.");
        }

        public bool TryResolve<T>(out T service) where T : class
        {
            try { service = Resolve<T>(); return true; }
            catch (InvalidOperationException) { service = null; return false; }
        }

        private void Guard()
        {
            if (_sealed)
                throw new InvalidOperationException(
                    "ServiceContainer is sealed. Register during bootstrap only.");
        }
    }
}
