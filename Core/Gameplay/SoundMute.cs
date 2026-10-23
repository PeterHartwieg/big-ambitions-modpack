using System;

namespace HardModePatch.Core
{
    // Own only the temporary mute bit, preserving the source's volume and its original mute state.
    // The adapter supplies exactly the reviewed cricket source, never a mixer or a global audio setting.
    public sealed class SoundMute<T> where T : class
    {
        readonly Func<T, bool> read;
        readonly Action<T, bool> write;
        T held;
        bool original;

        public SoundMute(Func<T, bool> read, Action<T, bool> write) { this.read = read; this.write = write; }

        public void Apply(T source, bool enabled)
        {
            if (!enabled || !ReferenceEquals(source, held)) Release();
            if (!enabled || source == null) return;
            if (held == null) { original = read(source); held = source; }
            write(source, true);
        }

        public void Release()
        {
            if (held == null) return;
            var source = held;
            held = null;
            write(source, original);
        }
    }
}
