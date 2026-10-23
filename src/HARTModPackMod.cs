using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using BAModAPI;
using HARTModPack.Core;

[assembly: AssemblyVersion("1.1.0.0")]
[assembly: AssemblyInformationalVersion("1.1.0")]
[assembly: RegisterModClass(typeof(HARTModPack.HARTModPackMod))]
namespace HARTModPack
{
    // Initialization scope: loaded once at startup and kept for the whole session, like the members it checks.
    // Pack management uses public APIs; integrated gameplay uses guarded Harmony hooks.
    [ModEntryOnInitializationLoad]
    public sealed class HARTModPackMod : IModBigAmbitions
    {
        // With a Workshop and a ModsLocal copy both on, the game creates this class twice from one DLL.
        // The first copy does the work and the other waits; if the working copy is switched off, the next one takes over.
        static readonly List<KeyValuePair<HARTModPackMod, string>> copies = new List<KeyValuePair<HARTModPackMod, string>>();
        static HARTModPackMod active;

        public string[] RelativeAssetBundlePaths { get { return new string[0]; } }

        public Task OnLoadAsync(ModContext context)
        {
            copies.Add(new KeyValuePair<HARTModPackMod, string>(this, context.ModId));
            if (active != null)
            {
                Log.Warn("A second copy (" + context.ModId + ") is on; it waits. Turn one copy off.");
                return Task.FromResult(0);
            }
            try
            {
                Activate(this, context.ModId);
            }
            catch (Exception ex)
            {
                // Let the game record the failure, with nothing of this copy left behind.
                copies.RemoveAll(c => c.Key == this);
                Log.Error("Not active: " + ex);
                throw;
            }
            return Task.FromResult(0);
        }

        public Task OnUnloadAsync()
        {
            copies.RemoveAll(c => c.Key == this);
            if (active != this) return Task.FromResult(0);
            Deactivate();
            if (copies.Count > 0)
            {
                try { Activate(copies[0].Key, copies[0].Value); Log.Info("The other copy (" + copies[0].Value + ") took over."); }
                catch (Exception ex) { Deactivate(); Log.Error("The other copy couldn't take over: " + ex); }
            }
            return Task.FromResult(0);
        }

        static void Activate(HARTModPackMod copy, string modId)
        {
            try
            {
                OptionButtonCaptions.Start();
                StatusPage.Register(modId);
                HardModePatch.GameplayFeatures.Begin(modId);
                PackRunner.Begin();
                CompanionSave.Start();
                active = copy;
                Log.Info("Version " + Pack.Version + " active. Checking the pack once the other mods have loaded.");
            }
            catch (Exception)
            {
                Deactivate();
                throw;
            }
        }

        static void Deactivate()
        {
            active = null;
            try { CompanionSave.Stop(); } catch (Exception ex) { Log.Error("Stopping companion: " + ex); }
            try { HardModePatch.GameplayFeatures.End(); } catch (Exception ex) { Log.Error("Stopping gameplay: " + ex); }
            try { PackRunner.End(); } catch (Exception ex) { Log.Error("Stopping: " + ex); }
            try { StatusPage.Unregister(); } catch (Exception ex) { Log.Error("Removing the Mods page: " + ex); }
            try { OptionButtonCaptions.Stop(); } catch (Exception ex) { Log.Error("Removing button captions: " + ex); }
        }
    }

    static class Log
    {
        const string Prefix = "[HART. Mod Pack] ";
        public static void Info(string message) { UnityEngine.Debug.Log(Prefix + message); }
        public static void Warn(string message) { UnityEngine.Debug.LogWarning(Prefix + message); }
        public static void Error(string message) { UnityEngine.Debug.LogError(Prefix + message); }
    }
}
