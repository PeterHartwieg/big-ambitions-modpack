using System;
using System.Reflection;

namespace HARTModPack.Core
{
    // Subscribe before scanning: also catches a legacy DLL that arrives after this pack.
    // AssemblyLoad completes before the loader can invoke that DLL's mod entry point.
    // A loaded assembly cannot be unloaded by switching its mod off, so refusal lasts until restart.
    public sealed class LegacyPatchGuard : IDisposable
    {
        readonly Action start, stop, conflict;
        readonly object gate = new object();
        bool listening, ownsFeatures;
        public bool Blocked { get; private set; }
        public bool Active { get { lock (gate) return ownsFeatures && !Blocked; } }

        public LegacyPatchGuard(Action start, Action stop, Action conflict)
        {
            this.start = start;
            this.stop = stop;
            this.conflict = conflict;
        }

        public static bool IsLegacy(string name)
        {
            return string.Equals(name, "HardModePatch", StringComparison.OrdinalIgnoreCase);
        }

        public void Begin()
        {
            lock (gate)
            {
                if (listening) return;
                listening = true;
                AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) Observe(assembly.GetName().Name);
                if (Blocked) return;
                ownsFeatures = true;
                try
                {
                    start();
                    // Handles a reentrant load during start, as well as ordinary later loads.
                    if (Blocked) { ownsFeatures = true; StopOwned(); }
                }
                catch
                {
                    StopOwned();
                    throw;
                }
            }
        }

        void OnAssemblyLoad(object sender, AssemblyLoadEventArgs args) { Observe(args.LoadedAssembly.GetName().Name); }

        public void Observe(string assemblyName)
        {
            lock (gate)
            {
                if (!IsLegacy(assemblyName) || Blocked) return;
                Blocked = true;
                StopOwned();
                conflict();
            }
        }

        void StopOwned()
        {
            if (!ownsFeatures) return;
            ownsFeatures = false;
            stop();
        }

        public void Dispose()
        {
            lock (gate)
            {
                AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
                listening = false;
                StopOwned();
            }
        }
    }
}
