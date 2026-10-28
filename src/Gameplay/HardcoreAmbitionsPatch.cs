using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HardModePatch.Core;
using HarmonyLib;

namespace HardModePatch
{
    // Hardcore Ambitions (3809287926) sets up any new game as its own: 23 difficulty values, a $75,454
    // student loan, three investment funds and $551 cash, and higher vehicle and boat prices. This keeps
    // the indebted start and the vehicle prices, and lets the save keep its own difficulty:
    //
    // - EnforceHardcoreGameVariables writes the 23 values and re-prices every vanilla loan at its 2.56
    //   interest, on every load. Skipped. The student loan keeps the mod's fixed $16/$158 a day terms,
    //   which its bootstrap re-applies itself.
    // - EnsureStartingReserve buys the three funds and sets cash to $551.37. Replaced by setting the cash
    //   and the mod's own "done" flag, so it never tries again.
    // - ApplyCatalogPriceTargets edits the VehicleType assets, which last until the game quits. The
    //   prices are recorded before its writes and put back when a save it didn't mark loads. Its BoatType
    //   pass never takes effect on build 3682: boats are plain objects in a static list, not Unity assets.
    // - LoanHelper.RecalculateLoanPayments re-prices every loan when the player changes the interest
    //   setting mid-game. Hardcore Ambitions' own recalculation, which spared the student loan, is the one
    //   skipped above, so the student loan's fixed terms are put back after the game's.
    public sealed class HardcoreAmbitionsPatch : ITargetPatch
    {
        const string ModType = "EconomyPresets.HardcoreAmbitions.HardcoreAmbitionsPrototypeMod";
        const int EnforceHook = 0, ReserveHook = 1, CatalogueHook = 2, GameLoadedHook = 3, RecalculateHook = 4;
        // Reviewed bodies (build 3682, mod hash 62ba60cb66dc226c). EnforceHardcoreGameVariables is skipped,
        // EnsureStartingReserve replaced, and TryBootstrapFromRunner's flag order is what makes both safe.
        static readonly Contract EnforceBody = new Contract("EnforceHardcoreGameVariables", "6a1f72eb0f944748");
        static readonly Contract ReserveBody = new Contract("EnsureStartingReserve", "445cf66a78b31d76");
        static readonly Contract BootstrapBody = new Contract("TryBootstrapFromRunner", "c8983a876aa90151");
        // How it finds the student loan and the terms it gives it, which RecalculatePostfix repeats.
        static readonly Contract FindLoanBody = new Contract("FindStudentLoan", "2f1fb5f179acbf3e");
        static readonly Contract LoanTermsBody = new Contract("ApplyStudentLoanTerms", "8e05800d4a1e7e4b");

        public TargetInfo Target { get { return Targets.HardcoreAmbitions; } }

        public IList<MethodBase> ResolveHooks(Assembly target)
        {
            var mod = target.GetType(ModType, false);
            var hooks = new MethodBase[5];
            if (mod != null)
            {
                hooks[EnforceHook] = Method(mod, "EnforceHardcoreGameVariables", typeof(GameInstance));
                // The second parameter is the mod's private BankBundle class; only its position is relied on.
                hooks[ReserveHook] = AccessTools.GetDeclaredMethods(mod).FirstOrDefault(m =>
                    m.Name == "EnsureStartingReserve" && m.ReturnType == typeof(bool)
                    && m.GetParameters().Length == 2 && m.GetParameters()[0].ParameterType == typeof(GameInstance));
                hooks[CatalogueHook] = Method(mod, "ApplyCatalogPriceTargets", typeof(string), typeof(string), typeof(Dictionary<double, double>));
            }
            hooks[GameLoadedHook] = AccessTools.Method(typeof(GlobalEvents), "InvokeOnGameLoaded", Type.EmptyTypes);
            bool loanFields = AccessTools.Field(typeof(Loan), "totalAmount") != null && AccessTools.Field(typeof(Loan), "dailyInterest") != null && AccessTools.Field(typeof(Loan), "dailyPayment") != null;
            if (loanFields) hooks[RecalculateHook] = AccessTools.Method(typeof(Helpers.LoanHelper), "RecalculateLoanPayments", new[] { typeof(GameInstance) });
            return hooks;
        }

        public IList<ContractCheck> Contracts(Assembly target, IList<MethodBase> hooks)
        {
            var mod = target.GetType(ModType, false);
            var bootstrap = mod == null ? null : AccessTools.DeclaredMethod(mod, "TryBootstrapFromRunner", new[] { typeof(int) });
            var findLoan = mod == null ? null : AccessTools.DeclaredMethod(mod, "FindStudentLoan", new[] { typeof(GameInstance) });
            var loanTerms = mod == null ? null : AccessTools.DeclaredMethod(mod, "ApplyStudentLoanTerms", new[] { typeof(Loan) });
            return new[]
            {
                new ContractCheck(hooks[EnforceHook], EnforceBody),
                new ContractCheck(hooks[ReserveHook], ReserveBody),
                new ContractCheck(bootstrap, BootstrapBody),
                new ContractCheck(findLoan, FindLoanBody),
                new ContractCheck(loanTerms, LoanTermsBody)
            };
        }

        public void Revert() { }

        static MethodBase Method(Type type, string name, params Type[] parameters)
        {
            var method = AccessTools.Method(type, name, parameters);
            return method != null && method.DeclaringType == type ? method : null;
        }

        public void Apply(Harmony harmony, Assembly target, IList<MethodBase> hooks)
        {
            var self = typeof(HardcoreAmbitionsPatch);
            harmony.Patch(hooks[EnforceHook], prefix: new HarmonyMethod(self, "EnforcePrefix"));
            harmony.Patch(hooks[ReserveHook], prefix: new HarmonyMethod(self, "ReservePrefix"));
            harmony.Patch(hooks[CatalogueHook], prefix: new HarmonyMethod(self, "CataloguePrefix"));
            harmony.Patch(hooks[GameLoadedHook], postfix: new HarmonyMethod(self, "GameLoadedPostfix"));
            harmony.Patch(hooks[RecalculateHook], postfix: new HarmonyMethod(self, "RecalculatePostfix"));
        }

        // Hardcore Ambitions' student loan, found as it finds it: by its total.
        const float StudentLoanTotal = 75454.35f;
        const int StudentLoanInterest = 16, StudentLoanPayment = 158;

        static void RecalculatePostfix(GameInstance gameInstance)
        {
            try
            {
                if (!Rules.HardcoreKeepDifficulty.Enabled || !IsHardcore(gameInstance) || gameInstance.Loans == null) return;
                foreach (var loan in gameInstance.Loans)
                {
                    if (loan == null || Math.Abs(loan.totalAmount - StudentLoanTotal) > 0.05f) continue;
                    loan.dailyInterest = StudentLoanInterest;
                    loan.dailyPayment = StudentLoanPayment;
                }
            }
            catch (Exception ex) { Log.Error("Hardcore Ambitions: student loan terms not restored after a recalculation: " + ex); }
        }

        static bool IsHardcore(GameInstance game)
        {
            string flag;
            return game != null && game.modData != null && game.modData.TryGetValue(HardcoreStart.SaveEnabledFlag, out flag) && flag == "1";
        }

        static bool loggedKeep;

        static bool EnforcePrefix()
        {
            if (!Rules.HardcoreKeepDifficulty.Enabled) return true;
            if (!loggedKeep) Log.Info("Hardcore Ambitions: the save keeps its own difficulty settings; the student loan keeps the mod's $16 interest and $158 payment a day.");
            loggedKeep = true;
            return false;
        }

        static bool ReservePrefix(object[] __args, ref bool __result)
        {
            if (!Rules.HardcoreNoFunds.Enabled) return true;
            var game = __args[0] as GameInstance;
            if (game == null) return true;
            if (game.modData == null) game.modData = new Dictionary<string, string>();
            game.Money = HardcoreStart.StartingCash;
            game.modData[HardcoreStart.ReserveInitializedFlag] = "1";
            game.modData[HardcoreStart.NoFundsFlag] = "1";
            __result = true;
            Log.Info("Hardcore Ambitions: start set up without the investment funds; cash " + HardcoreStart.StartingCash.ToString("0.00") + ".");
            return false;
        }

        // Prices as they were before Hardcore Ambitions' first write, per catalogue asset.
        sealed class PriceRecord
        {
            public UnityEngine.Object Asset;
            public double Price;
        }
        static readonly Dictionary<string, List<PriceRecord>> snapshot = new Dictionary<string, List<PriceRecord>>();
        static readonly Dictionary<string, Type> catalogueTypes = new Dictionary<string, Type>();
        static readonly HashSet<string> notAssets = new HashSet<string>();
        static int gamesLoaded;

        // Runs before every catalogue pass. Assets not yet recorded are recorded now, before the pass
        // writes them; assets loaded after the first pass are picked up on a later one.
        static void CataloguePrefix(string typeName)
        {
            try
            {
                if (typeName == null || notAssets.Contains(typeName)) return;
                Type type;
                if (!catalogueTypes.TryGetValue(typeName, out type))
                {
                    var named = AccessTools.AllTypes().Where(t => t.Name == typeName).ToList();
                    if (named.Count == 0) return;   // not loaded yet; Hardcore Ambitions retries and so do we
                    type = named.FirstOrDefault(t => typeof(UnityEngine.Object).IsAssignableFrom(t));
                    if (type == null) { notAssets.Add(typeName); return; }   // BoatType: nothing for either mod to change
                    catalogueTypes[typeName] = type;
                }
                List<PriceRecord> records;
                if (!snapshot.TryGetValue(typeName, out records)) records = new List<PriceRecord>();
                var known = new HashSet<int>(records.Select(r => r.Asset == null ? 0 : r.Asset.GetInstanceID()));
                int added = 0;
                foreach (var asset in UnityEngine.Resources.FindObjectsOfTypeAll(type))
                {
                    double price;
                    if (asset == null || known.Contains(asset.GetInstanceID()) || !PriceMember.TryRead(asset, out price)) continue;
                    records.Add(new PriceRecord { Asset = asset, Price = price });
                    added++;
                }
                if (records.Count > 0) snapshot[typeName] = records;
                if (added > 0) Log.Info("Hardcore Ambitions: recorded " + added + " " + typeName + " prices before the Hardcore changes.");
            }
            catch (Exception ex) { Log.Error("Hardcore Ambitions: could not record " + typeName + " prices: " + ex); }
        }

        static void GameLoadedPostfix()
        {
            try
            {
                var game = SaveGameManager.Current;
                if (game == null) return;
                gamesLoaded++;
                bool hardcore = IsHardcore(game);
                // Hardcore Ambitions registers its setup callback once per launch, and the game clears it after
                // the first load. Reloading the same save is harmless; a different Hardcore save gets neither its
                // vehicle prices nor its checks.
                if (hardcore && gamesLoaded > 1)
                    Log.Warn("Hardcore Ambitions only sets up the first save loaded in a session. If this isn't that save, restart the game before playing it.");
                if (!Rules.HardcoreVehiclePricesPerSave.Enabled) return;
                if (!HardcoreStart.ShouldRestoreVanillaPrices(snapshot.Count > 0, hardcore)) return;
                int restored = 0;
                foreach (var records in snapshot.Values)
                    foreach (var record in records)
                        if (record.Asset != null && PriceMember.TryWrite(record.Asset, record.Price)) restored++;
                Log.Info("Hardcore Ambitions: this save isn't a Hardcore save; restored " + restored + " vehicle and boat prices.");
            }
            catch (Exception ex) { Log.Error("Hardcore Ambitions: could not restore vehicle prices: " + ex); }
        }
    }

    // Reads and writes a catalogue price the way Hardcore Ambitions does: "Price", then "price", as a field
    // or a property.
    static class PriceMember
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static readonly string[] Names = { "Price", "price" };

        public static bool TryRead(object owner, out double value)
        {
            value = 0;
            foreach (var name in Names)
            {
                object raw;
                var field = owner.GetType().GetField(name, Flags);
                if (field != null) raw = field.GetValue(owner);
                else
                {
                    var property = owner.GetType().GetProperty(name, Flags);
                    if (property == null || !property.CanRead || property.GetIndexParameters().Length > 0) continue;
                    raw = property.GetValue(owner, null);
                }
                if (!(raw is float || raw is double || raw is int || raw is long || raw is decimal)) continue;
                value = Convert.ToDouble(raw);
                return true;
            }
            return false;
        }

        public static bool TryWrite(object owner, double value)
        {
            foreach (var name in Names)
            {
                try
                {
                    var field = owner.GetType().GetField(name, Flags);
                    if (field != null) { field.SetValue(owner, Convert.ChangeType(value, field.FieldType)); return true; }
                    var property = owner.GetType().GetProperty(name, Flags);
                    if (property != null && property.CanWrite) { property.SetValue(owner, Convert.ChangeType(value, property.PropertyType), null); return true; }
                }
                catch (Exception) { }
            }
            return false;
        }
    }
}
