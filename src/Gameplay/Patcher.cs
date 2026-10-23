using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
using System.Threading;
using BAModAPI.Services;
using HardModePatch.Core;
using HarmonyLib;

namespace HardModePatch
{
    // One patch per Workshop mod. The patch never references the mod's DLL at compile time: it finds the
    // loaded assembly by name, so a player without that mod only loses that patch.
    public interface ITargetPatch
    {
        TargetInfo Target { get; }
        // Every method the patch hooks, resolved in the target assembly (or the game). A null entry means
        // the method is missing or its signature or the members it relies on changed.
        IList<MethodBase> ResolveHooks(Assembly target);
        // Methods whose bodies the patch skips, replaces or relies on, with the reviewed fingerprints.
        // A new version whose body differs is left unpatched.
        IList<ContractCheck> Contracts(Assembly target, IList<MethodBase> hooks);
        void Apply(Harmony harmony, Assembly target, IList<MethodBase> hooks);
        // Undo anything Apply changed outside Harmony, for an unload while the game runs.
        void Revert();
    }

    // A target patched only in versions whose code was reviewed. A refused version is left unpatched without the
    // hook and contract checks; the gate itself logs why, once.
    public interface IReviewedOnly
    {
        bool Accepts(Assembly target);
    }

    public sealed class ContractCheck
    {
        public readonly MethodBase Method;
        public readonly Contract Contract;
        public ContractCheck(MethodBase method, Contract contract) { Method = method; Contract = contract; }
    }

    public sealed class Patcher
    {
        readonly Harmony harmony;
        readonly ITargetPatch[] patches;
        readonly Dictionary<string, TargetState> states = new Dictionary<string, TargetState>();
        readonly Queue<Assembly> pending = new Queue<Assembly>();
        readonly object gate = new object();
        int mainThread;
        bool listening;

        public Patcher(Harmony harmony, ITargetPatch[] patches)
        {
            this.harmony = harmony;
            this.patches = patches;
            foreach (var patch in patches) states[patch.Target.Key] = TargetState.Missing;
        }

        public TargetState StateOf(TargetInfo target)
        {
            lock (gate) { TargetState state; return states.TryGetValue(target.Key, out state) ? state : TargetState.Missing; }
        }

        // Mods load in an order the player doesn't control, so the targets already loaded are patched now
        // and the rest when their assembly arrives. The game loads mod DLLs on the main thread; an assembly
        // arriving on another thread is queued for the next frame, since patching and the options page
        // touch Unity.
        public void Start()
        {
            mainThread = Thread.CurrentThread.ManagedThreadId;
            lock (gate) listening = true;
            AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
            UnityLifecycleProvider.OnUpdate += DrainPending;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) Consider(assembly);
        }

        public void Stop()
        {
            lock (gate)
            {
                listening = false;
                pending.Clear();
            }
            AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
            UnityLifecycleProvider.OnUpdate -= DrainPending;
            harmony.UnpatchAll(harmony.Id);
            foreach (var patch in patches)
            {
                try { patch.Revert(); }
                catch (Exception ex) { Log.Error(patch.Target.Title + ": could not undo its changes: " + ex); }
            }
        }

        void OnAssemblyLoad(object sender, AssemblyLoadEventArgs args)
        {
            try
            {
                if (Thread.CurrentThread.ManagedThreadId == mainThread) Consider(args.LoadedAssembly);
                else lock (gate) if (listening) pending.Enqueue(args.LoadedAssembly);
            }
            catch (Exception ex) { Log.Error("Could not inspect " + args.LoadedAssembly.GetName().Name + ": " + ex); }
        }

        void DrainPending()
        {
            while (true)
            {
                Assembly next;
                lock (gate)
                {
                    if (pending.Count == 0) return;
                    next = pending.Dequeue();
                }
                try { Consider(next); }
                catch (Exception ex) { Log.Error("Could not inspect " + next.GetName().Name + ": " + ex); }
            }
        }

        void Consider(Assembly assembly)
        {
            string name = assembly.GetName().Name;
            foreach (var patch in patches)
            {
                if (!string.Equals(patch.Target.AssemblyName, name, StringComparison.Ordinal)) continue;
                lock (gate)
                {
                    if (!listening || states[patch.Target.Key] != TargetState.Missing) return;
                    Install(patch, assembly);
                }
                Options.Refresh();
            }
        }

        void Install(ITargetPatch patch, Assembly assembly)
        {
            var target = patch.Target;
            var reviewed = patch as IReviewedOnly;
            if (reviewed != null && !reviewed.Accepts(assembly))
            {
                states[target.Key] = TargetState.Refused;
                return;
            }
            IList<MethodBase> hooks;
            try { hooks = patch.ResolveHooks(assembly); }
            catch (Exception ex) { Log.Error(target.Title + ": hook lookup failed, left unpatched: " + ex); states[target.Key] = TargetState.Incompatible; return; }
            int found = 0;
            var missing = new List<int>();
            for (int index = 0; index < hooks.Count; index++)
            {
                if (hooks[index] != null) found++;
                else missing.Add(index);
            }
            string hash = HashOf(assembly);
            var state = TargetGate.Classify(true, hash, target.VerifiedHashes, hooks.Count, found);
            string version = target.Title + " (" + (hash ?? "hash unreadable") + ")";
            if (state != TargetState.Incompatible && !ContractsHold(patch, assembly, hooks, state, version)) state = TargetState.Incompatible;
            states[target.Key] = state;
            if (state == TargetState.Incompatible)
            {
                if (missing.Count > 0)
                    Log.Warn(version + ": " + missing.Count + " of " + hooks.Count + " hooked methods are missing or changed (hook " + string.Join(", ", missing.Select(i => i.ToString()).ToArray()) + "). Left unpatched; it runs as its author published it.");
                return;
            }
            try
            {
                patch.Apply(harmony, assembly, hooks);
            }
            catch (Exception ex)
            {
                // A half-applied target is worse than an unpatched one.
                foreach (var hook in hooks) if (hook != null) harmony.Unpatch(hook, HarmonyPatchType.All, harmony.Id);
                try { patch.Revert(); } catch (Exception) { }
                states[target.Key] = TargetState.Incompatible;
                Log.Error(version + ": patching failed, left unpatched: " + ex);
                return;
            }
            if (state == TargetState.Verified) Log.Info(version + ": verified version, patched.");
            else Log.Warn(version + ": a version this patch hasn't reviewed. Its hooked methods and the method bodies it replaces are unchanged, so it is patched; report problems with this line from the log.");
        }

        // For a reviewed DLL the fingerprints are only compared to report a difference between how .NET
        // and the game's Mono read the same IL. For a new version, a changed body leaves the target unpatched.
        static bool ContractsHold(ITargetPatch patch, Assembly assembly, IList<MethodBase> hooks, TargetState state, string version)
        {
            IList<ContractCheck> contracts;
            try { contracts = patch.Contracts(assembly, hooks); }
            catch (Exception ex) { Log.Error(version + ": could not read the methods it relies on: " + ex); return state == TargetState.Verified; }
            bool hold = true;
            foreach (var check in contracts)
            {
                string actual = null;
                try { actual = check.Method == null ? null : IlText.Fingerprint(check.Method); }
                catch (Exception) { }
                if (actual == check.Contract.Expected) continue;
                if (state == TargetState.Verified)
                    Log.Info(version + ": " + check.Contract.Name + " reads as " + (actual ?? "unreadable") + " in the game, " + check.Contract.Expected + " offline. Diagnostic only; the DLL is the reviewed one.");
                else
                {
                    Log.Warn(version + ": " + check.Contract.Name + " changed (" + (actual ?? "unreadable") + ", reviewed " + check.Contract.Expected + "). Left unpatched; it runs as its author published it.");
                    hold = false;
                }
            }
            return hold;
        }

        internal static string HashOf(Assembly assembly)
        {
            try
            {
                string path = assembly.Location;
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
                using (var sha = SHA256.Create())
                {
                    byte[] digest = sha.ComputeHash(File.ReadAllBytes(path));
                    return TargetGate.HashPrefix(BitConverter.ToString(digest).Replace("-", string.Empty));
                }
            }
            catch (Exception) { return null; }
        }
    }

    // A method body as runtime-neutral text: opcode names, operands by name, branch targets by
    // instruction index. Types are written by namespace and name, without assembly names, so .NET and
    // Mono read the same text from the same IL. Compiler-generated ordinals are written as #, so only a
    // change to this body changes its fingerprint (Fingerprint.WithoutGeneratedOrdinals).
    public static class IlText
    {
        public static string Fingerprint(MethodBase method)
        {
            return Core.Fingerprint.Of(Lines(method));
        }

        public static IList<string> Lines(MethodBase method)
        {
            var code = PatchProcessor.GetOriginalInstructions(method);
            var labels = new Dictionary<Label, int>();
            for (int index = 0; index < code.Count; index++)
                foreach (var label in code[index].labels) labels[label] = index;
            var lines = new List<string>(code.Count);
            foreach (var instruction in code)
                lines.Add(Core.Fingerprint.WithoutGeneratedOrdinals(instruction.opcode.Name + " " + Operand(instruction.operand, labels)));
            return lines;
        }

        static string Operand(object operand, Dictionary<Label, int> labels)
        {
            if (operand == null) return string.Empty;
            if (operand is Label) { int at; return labels.TryGetValue((Label)operand, out at) ? "@" + at : "@?"; }
            var many = operand as Label[];
            if (many != null) return string.Join(",", many.Select(l => { int at; return labels.TryGetValue(l, out at) ? "@" + at : "@?"; }).ToArray());
            var local = operand as LocalBuilder;
            if (local != null) return "local " + local.LocalIndex + " " + TypeName(local.LocalType);
            var method = operand as MethodBase;
            if (method != null) return TypeName(method.DeclaringType) + "::" + method.Name + "(" + string.Join(",", method.GetParameters().Select(p => TypeName(p.ParameterType)).ToArray()) + ")";
            var field = operand as FieldInfo;
            if (field != null) return TypeName(field.DeclaringType) + "::" + field.Name;
            var type = operand as Type;
            if (type != null) return TypeName(type);
            var text = operand as string;
            if (text != null) return "\"" + text + "\"";
            var formattable = operand as IFormattable;
            if (formattable != null) return formattable.ToString(null, CultureInfo.InvariantCulture);
            return operand.GetType().Name;
        }

        public static string TypeName(Type type)
        {
            if (type == null) return "?";
            if (type.IsArray) return TypeName(type.GetElementType()) + "[]";
            if (type.IsByRef) return TypeName(type.GetElementType()) + "&";
            if (type.IsPointer) return TypeName(type.GetElementType()) + "*";
            if (type.IsGenericParameter) return type.Name;
            string name = type.IsNested ? TypeName(type.DeclaringType) + "+" + type.Name : (string.IsNullOrEmpty(type.Namespace) ? type.Name : type.Namespace + "." + type.Name);
            if (type.IsGenericType && !type.IsGenericTypeDefinition)
                name += "[" + string.Join(",", type.GetGenericArguments().Select(TypeName).ToArray()) + "]";
            return name;
        }
    }
}
