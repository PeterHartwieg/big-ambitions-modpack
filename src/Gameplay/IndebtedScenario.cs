using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HardModePatch.Core;
using HarmonyLib;
using Helpers;
using SpecialServices.Bank;
using UI.Notification;

namespace HardModePatch
{
    // Standalone scenario. No RLO or Hardcore compile-time reference, no loan-payment hook and no
    // second ledger. The actual new-game call arms it; before game-loaded subscribers run, the city's
    // bank supplies a normal Loan contract. Existing saves without our pending marker are never adopted.
    // Keep every patched method in ResolveHooks, also exercised by the offline HookCheck.
    public static class IndebtedScenario
    {
        static readonly Harmony harmony = new Harmony(GameplayFeatures.HarmonyId + ".scenario");
        public static bool Available { get; private set; }

        public static IList<MethodBase> ResolveHooks()
        {
            var create = AccessTools.DeclaredMethod(typeof(SaveGameManager), "New", new[] { typeof(GameVariables) });
            var loaded = AccessTools.DeclaredMethod(typeof(GlobalEvents), "InvokeOnGameLoaded", Type.EmptyTypes);
            return new MethodBase[]
            {
                create != null && create.IsStatic && create.ReturnType == typeof(void) ? create : null,
                loaded != null && loaded.IsStatic && loaded.ReturnType == typeof(void) ? loaded : null
            };
        }

        public static void ApplyHooks(Harmony owner, IList<MethodBase> hooks)
        {
            owner.Patch(hooks[0], postfix: new HarmonyMethod(typeof(IndebtedScenario), "NewGamePostfix"));
            owner.Patch(hooks[1], prefix: new HarmonyMethod(typeof(IndebtedScenario), "GameLoadedPrefix"));
        }

        public static void Start()
        {
            try
            {
                var hooks = ResolveHooks();
                if (hooks.Any(h => h == null)) throw new MissingMethodException("New-game scenario hooks changed.");
                ApplyHooks(harmony, hooks);
                Available = true;
            }
            catch (Exception ex)
            {
                harmony.UnpatchAll(harmony.Id);
                Available = false;
                Log.Error("Indebted start unavailable; existing target patches are unaffected: " + ex);
            }
        }

        public static void Stop()
        {
            harmony.UnpatchAll(harmony.Id);
            Available = false;
        }

        static bool HardcoreLoaded()
        {
            return AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == Targets.HardcoreAmbitions.AssemblyName);
        }

        static void NewGamePostfix()
        {
            try
            {
                bool conflict = HardcoreLoaded();
                if (Rules.IndebtedScenario.Enabled && conflict)
                    Log.Warn("Indebted start skipped: disable Hardcore Ambitions and restart before creating this scenario.");
                ArmNewGame(SaveGameManager.Current, Rules.IndebtedScenario.Enabled, conflict);
            }
            catch (Exception ex) { Log.Error("Indebted start could not be selected for the new game: " + ex); }
        }

        // Only called from the New postfix in production; exposed internally for the offline behavior checks.
        internal static bool ArmNewGame(GameInstance game, bool selected, bool hardcoreLoaded)
        {
            if (game == null) return false;
            bool marked = game.modData != null && game.modData.ContainsKey(IndebtedStart.StateKey);
            if (!IndebtedStart.CanArm(selected, hardcoreLoaded, marked)) return false;
            if (game.modData == null) game.modData = new Dictionary<string, string>();
            game.modData[IndebtedStart.StateKey] = IndebtedStart.Pending;
            return true;
        }

        // A prefix so RLO's load/first-timer credit reading sees the starting debt, not a later new loan.
        // No timer may inject a debt after play has begun. A failed setup is marked failed and never
        // silently retried on reload: that could reset cash after the player has already begun playing.
        static void GameLoadedPrefix()
        {
            try
            {
                var game = SaveGameManager.Current;
                if (StateOf(game) != IndebtedStart.Pending) return;
                if (HardcoreLoaded())
                {
                    FailPending(game, "Hardcore Ambitions is loaded; disable it and create a new game.");
                    return;
                }
                Address address;
                BankSettings bank;
                if (!FindBank(game, out address, out bank))
                {
                    // A new game raises game-loaded twice: when character creation opens, before the city
                    // exists (no building registrations yet), and again when the city has loaded. Play can't
                    // begin before the second, so waiting for it keeps the "no debt after play" rule.
                    if (IndebtedStart.ShouldWaitForCity(CityRegistrations(game)))
                    {
                        Log.Info("Indebted start waiting for the city to load (game-loaded before the city exists).");
                        return;
                    }
                    FailPending(game, "No native bank is ready when the city has loaded.");
                    return;
                }
                // These are the game's own formulas for this bank/calendar/difficulty. An existing
                // contract is not repriced by our code: RLO tracks it and can apply its own holds/relief.
                int interest = LoanHelper.CalculateDailyInterestPayment(IndebtedStart.Debt, bank,
                    game.gameVariables.daysPerYear, game.gameVariables.bankInterestMultiplier);
                int payment = LoanHelper.CalculateDailyPayment(IndebtedStart.Debt, bank, game.gameVariables.daysPerYear);
                if (!CompletePending(game, address, interest, payment))
                {
                    FailPending(game, "Unexpected existing debt or invalid loan terms.");
                    return;
                }
                SaveGameManager.MarkChange();
                Log.Info("Indebted start created once: cash " + IndebtedStart.Cash.ToString("0.00")
                    + ", native debt " + IndebtedStart.Debt.ToString("0.00") + ", interest " + interest
                    + "/day, principal " + payment + "/day. Game/RLO own all later repayments and terms.");
            }
            catch (Exception ex)
            {
                if (StateOf(SaveGameManager.Current) == IndebtedStart.Pending) FailPending(SaveGameManager.Current, ex.ToString());
                else Log.Error("Indebted start completion/reporting error: " + ex);
            }
        }

        static void FailPending(GameInstance game, string reason)
        {
            if (StateOf(game) != IndebtedStart.Pending) return;
            game.modData[IndebtedStart.StateKey] = IndebtedStart.Failed;
            Log.Error("Indebted start not applied; no retry on this save: " + reason);
            try { Notifications.Show(NotificationType.Error, "hardmode_scenario_failed"); }
            catch (Exception ex) { Log.Error("Could not show the scenario failure notice: " + ex.Message); }
        }

        static int CityRegistrations(GameInstance game)
        {
            return game == null || game.BuildingRegistrations == null ? 0 : game.BuildingRegistrations.Count;
        }

        static string StateOf(GameInstance game)
        {
            string state;
            return game != null && game.modData != null && game.modData.TryGetValue(IndebtedStart.StateKey, out state) ? state : null;
        }

        internal static bool CompletePending(GameInstance game, Address bank, int interest, int payment)
        {
            if (game == null || game.Loans == null || game.modData == null) return false;
            if (!IndebtedStart.CanComplete(StateOf(game), game.Loans.Count != 0, !ReferenceEquals(bank, null), interest, payment)) return false;
            var loan = new Loan(IndebtedStart.Debt, interest, payment, bank);
            float cashBefore = game.Money;
            try
            {
                game.Loans.Add(loan);
                game.Money = IndebtedStart.Cash;
                game.modData[IndebtedStart.StateKey] = IndebtedStart.Complete;
                // This is pre-existing debt: no payout, origination fee, investment purchase or new-credit event.
                return true;
            }
            catch
            {
                game.Loans.Remove(loan);
                game.Money = cashBefore;
                game.modData[IndebtedStart.StateKey] = IndebtedStart.Pending;
                throw;
            }
        }

        static bool FindBank(GameInstance game, out Address address, out BankSettings settings)
        {
            address = null;
            settings = null;
            if (game.gameVariables == null || game.gameVariables.daysPerYear <= 0 || game.BuildingRegistrations == null) return false;
            float multiplier = game.gameVariables.bankInterestMultiplier;
            if (float.IsNaN(multiplier) || float.IsInfinity(multiplier) || multiplier < 0f) return false;
            // Same preferred real bank as the former start, with deterministic fallback to another native bank.
            foreach (var registration in game.BuildingRegistrations.Where(r => r != null && r.HasValidAddress)
                .OrderBy(r => r.Address.streetName == "ba:street_secondavenue" && r.Address.streetNumber == 6 ? 0 : 1)
                .ThenBy(r => r.Address.streetName, StringComparer.Ordinal).ThenBy(r => r.Address.streetNumber))
            {
                var candidate = LoanHelper.GetBankSettings(registration.Address);
                if (ReferenceEquals(candidate, null) || candidate.yearsToPayLoan <= 0 || candidate.annualInterestRate < 0) continue;
                address = registration.Address;
                settings = candidate;
                return true;
            }
            return false;
        }
    }
}
