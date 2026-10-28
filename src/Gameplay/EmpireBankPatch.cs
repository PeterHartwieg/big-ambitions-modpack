using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HardModePatch.Core;
using HarmonyLib;
using UI.Notification;

namespace HardModePatch
{
    // Empire Bank (3781110629), a bank the player owns and runs from the phone.
    //
    // - Interest withdrawals reach the player as plain cash, which the game never taxes. The gateway's
    //   TryChangePlayerMoney is the one place cash moves; a withdrawal is taxed there at the save's rate.
    // - Emergency central-bank funding ($10M from the Risk Center card, at least $500K from the dashboard's
    //   liquidity banner) all goes through EmpireBankRuntime.RequestEmergencyFunding, which is skipped.
    // - Applicant credit scores are drawn, then raised to the product's minimum (Math.Max), so a minimum of
    //   850 makes every applicant perfect. The Math.Max after BellCredit is replaced so the drawn score stays.
    // - Loan products allow 50% APR, a 10% fee and a one-month term. Edits beyond the limits are refused,
    //   rate nudges are limited before the mod saves them, products loaded from a save are brought within
    //   the limits, approvals are clamped, and corporate negotiation offers are clamped before they're
    //   assessed. Contracts already signed keep their terms.
    // - It fetches /v1/stats and supporters.json from the author's servers without asking. The stats
    //   coroutine (reached from Settings and after a heartbeat) and the supporters refresh are skipped.
    //   The opt-in heartbeat is left to the player.
    public sealed class EmpireBankPatch : ITargetPatch
    {
        const int Gateway = 0, Emergency = 1, LoanApplicants = 2, CardApplicants = 3, UpdateLoan = 4, UpdateProduct = 5, AdjustRate = 6,
            Stats = 7, Supporters = 8, EmergencyCard = 9, SaveData = 10, ApproveLoan = 11, ApproveCard = 12, Negotiate = 13, Finalize = 14;
        const string WithdrawalReason = "withdraw";
        static PropertyInfo productsProperty, productIdProperty, productTypeProperty, rateProperty, feeProperty, termProperty;
        static PropertyInfo casesProperty, caseIdProperty, caseTypeProperty, offeredRateProperty, offeredFeeProperty, offeredTermProperty;
        static PropertyInfo applicationsProperty, suggestedAprProperty, requestedTermProperty, applicationStatusProperty;
        static MethodInfo markMutated;
        static PropertyInfo initializedProperty;
        // The bank runtime of the loaded save, seen when it binds its data, so a rule switched on mid-game
        // can bring its products within the limits.
        static WeakReference activeRuntime;

        // Reviewed bodies (mod hash 98be6844bcfd5793). Each is skipped, has its arguments changed, or is
        // relied on for what its values mean (rates as fractions, which list holds what), so any change
        // leaves Empire Bank unpatched until it is reviewed again.
        static readonly Contract[] Bodies =
        {
            new Contract("BigAmbitionsPlayerEconomyGateway.TryChangePlayerMoney", "26fad19818864980"),
            new Contract("EmpireBankRuntime.RequestEmergencyFunding", "60347e82b0345bf8"),
            new Contract("EmpireBankEmergencyCard.OnRequest", "30e9409284aed635"),
            new Contract("EmpireBankRuntime.UpdateLoanProduct", "5639a0f5646999a7"),
            new Contract("EmpireBankRuntime.UpdateProduct", "f55960e19e8395d4"),
            new Contract("EmpireBankRuntime.AdjustProductRate", "a3702dec711141e9"),
            new Contract("EmpireBankRuntime.ApplySaveData", "08b01919405ac20e"),
            new Contract("EmpireBankRuntime.ApproveApplication", "37f4bc1840295652"),
            new Contract("EmpireBankRuntime.ApproveCreditCardApplication", "e5633d6e4f023223"),
            new Contract("EmpireBankRuntime.SubmitNegotiationOffer", "a1fd52e597dfd1d8"),
            new Contract("EmpireBankRuntime.FinalizeAcceptedNegotiation", "9c3f1589adc0347c"),
            new Contract("EmpireTelemetryClient.FetchStats", "a7f75bfb07728f61"),
            new Contract("EmpireTelemetryClient.FetchStats iterator", "bb353dfbb5307335"),
            new Contract("EmpireBankSupportersView.StartRemoteRefresh", "0465452d6e792ccb")
        };

        public TargetInfo Target { get { return Targets.EmpireBank; } }

        public IList<MethodBase> ResolveHooks(Assembly target)
        {
            var hooks = new MethodBase[15];
            var gateway = target.GetType("EmpireBank.Integration.BigAmbitionsPlayerEconomyGateway", false);
            var card = target.GetType("EmpireBank.UI.EmpireBankEmergencyCard", false);
            var loans = target.GetType("EmpireBank.Core.LoanApplicationService", false);
            var cards = target.GetType("EmpireBank.Core.CreditCardApplicationService", false);
            var runtime = target.GetType("EmpireBank.Core.EmpireBankRuntime", false);
            var product = target.GetType("EmpireBank.Models.BankProduct", false);
            var application = target.GetType("EmpireBank.Models.LoanApplication", false);
            var telemetry = target.GetType("EmpireTelemetry.EmpireTelemetryClient", false);
            var supporters = target.GetType("EmpireBank.UI.EmpireBankSupportersView", false);
            var negotiation = target.GetType("EmpireBank.Models.NegotiationCase", false);
            var outString = typeof(string).MakeByRefType();

            if (gateway != null) hooks[Gateway] = Exact(gateway, "TryChangePlayerMoney", typeof(bool), P("amount", typeof(decimal)), P("reason", typeof(string)));
            if (card != null) hooks[EmergencyCard] = Exact(card, "OnRequest", typeof(void));
            if (loans != null) hooks[LoanApplicants] = WithScoreClamp(loans, "Create");
            if (cards != null) hooks[CardApplicants] = WithScoreClamp(cards, "CreateApplication");
            if (runtime != null) hooks[Emergency] = Exact(runtime, "RequestEmergencyFunding", typeof(void), P("amount", typeof(decimal)));
            if (runtime != null && product != null && application != null && ReadProductMembers(runtime, product, application))
            {
                hooks[UpdateLoan] = Exact(runtime, "UpdateLoanProduct", typeof(bool), P("productId", typeof(string)), P("apr", typeof(decimal)), P("termMonths", typeof(int)), P("feeRate", typeof(decimal)), P("message", outString));
                hooks[UpdateProduct] = Exact(runtime, "UpdateProduct", typeof(bool), P("productId", typeof(string)), P("rate", typeof(decimal)), P("termMonths", typeof(int)), P("feeValue", typeof(decimal)), P("message", outString));
                hooks[AdjustRate] = Exact(runtime, "AdjustProductRate", typeof(void), P("productId", typeof(string)), P("delta", typeof(decimal)));
                hooks[SaveData] = Exact(runtime, "ApplySaveData", typeof(void));
                hooks[ApproveLoan] = Exact(runtime, "ApproveApplication", null, P("id", typeof(string)), P("apr", typeof(decimal)), P("term", typeof(int)));
                hooks[ApproveCard] = Exact(runtime, "ApproveCreditCardApplication", null, P("id", typeof(string)), P("apr", typeof(decimal)));
                if (negotiation != null && ReadNegotiationMembers(runtime, negotiation))
                {
                    hooks[Negotiate] = Exact(runtime, "SubmitNegotiationOffer", typeof(bool), P("caseId", typeof(string)), P("rate", typeof(decimal)), P("termMonths", typeof(int)), P("feeRate", typeof(decimal)));
                    hooks[Finalize] = Exact(runtime, "FinalizeAcceptedNegotiation", typeof(bool), P("caseId", typeof(string)), P("message", outString));
                }
            }
            // The stats coroutine itself: Settings reaches it through FetchStatsIfStale, a successful heartbeat directly.
            if (telemetry != null) hooks[Stats] = Exact(telemetry, "FetchStats", typeof(IEnumerator));
            if (supporters != null) hooks[Supporters] = Exact(supporters, "StartRemoteRefresh", typeof(void));
            return hooks;
        }

        public IList<ContractCheck> Contracts(Assembly target, IList<MethodBase> hooks)
        {
            var telemetry = target.GetType("EmpireTelemetry.EmpireTelemetryClient", false);
            MethodBase iterator = null;
            if (telemetry != null)
            {
                var state = telemetry.GetNestedTypes(BindingFlags.NonPublic).FirstOrDefault(t => t.Name.StartsWith("<FetchStats>", StringComparison.Ordinal));
                if (state != null) iterator = AccessTools.DeclaredMethod(state, "MoveNext", Type.EmptyTypes);
            }
            var bodies = new[] { hooks[Gateway], hooks[Emergency], hooks[EmergencyCard], hooks[UpdateLoan], hooks[UpdateProduct], hooks[AdjustRate],
                hooks[SaveData], hooks[ApproveLoan], hooks[ApproveCard], hooks[Negotiate], hooks[Finalize], hooks[Stats], iterator, hooks[Supporters] };
            return bodies.Select((method, index) => new ContractCheck(method, Bodies[index])).ToArray();
        }

        public void Revert()
        {
            Rules.BankProductLimits.Changed -= OnLimitsChanged;
            activeRuntime = null;
        }

        static KeyValuePair<string, Type> P(string name, Type type) { return new KeyValuePair<string, Type>(name, type); }

        // The one declared method of that name, provided every parameter the patch binds to is there with
        // exactly that type, and the return type matches (null: any).
        static MethodBase Exact(Type type, string name, Type returns, params KeyValuePair<string, Type>[] parameters)
        {
            var matches = AccessTools.GetDeclaredMethods(type).Where(m => m.Name == name).ToList();
            if (matches.Count != 1) return null;
            var method = matches[0];
            if (returns != null && method.ReturnType != returns) return null;
            var actual = method.GetParameters();
            foreach (var wanted in parameters)
                if (!actual.Any(p => p.Name == wanted.Key && p.ParameterType == wanted.Value)) return null;
            return method;
        }

        static bool HasEnumValues(Type type, params string[] names)
        {
            return type.IsEnum && names.All(n => Enum.GetNames(type).Contains(n));
        }

        static bool Readable(PropertyInfo property, Type type)
        {
            return property != null && property.PropertyType == type && property.CanRead && property.GetIndexParameters().Length == 0
                && !property.GetGetMethod(true).IsStatic;
        }

        static bool Writable(PropertyInfo property, Type type)
        {
            return Readable(property, type) && property.CanWrite;
        }

        // A list of the stored objects themselves: a class element type, so changes made while
        // enumerating land on the bank's own objects.
        static bool ListOf(PropertyInfo property, Type element)
        {
            return property != null && !element.IsValueType && property.CanRead && property.GetIndexParameters().Length == 0
                && typeof(IEnumerable<>).MakeGenericType(element).IsAssignableFrom(property.PropertyType);
        }

        // Only a method whose IL still holds the BellCredit-then-Math.Max pair can be rewritten.
        static MethodBase WithScoreClamp(Type service, string name)
        {
            var method = AccessTools.GetDeclaredMethods(service).FirstOrDefault(m => m.Name == name);
            if (method == null) return null;
            try { return CountScoreClamps(PatchProcessor.GetOriginalInstructions(method)) == 1 ? method : null; }
            catch (Exception) { return null; }
        }

        static readonly MethodInfo MathMax = AccessTools.Method(typeof(Math), "Max", new[] { typeof(int), typeof(int) });

        static int CountScoreClamps(IList<CodeInstruction> code)
        {
            int count = 0;
            for (int index = 1; index < code.Count; index++)
                if (IsBellCredit(code[index - 1]) && code[index].Calls(MathMax)) count++;
            return count;
        }

        static bool IsBellCredit(CodeInstruction instruction)
        {
            var method = instruction.operand as MethodInfo;
            return (instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) && method != null && method.Name == "BellCredit" && method.ReturnType == typeof(int);
        }

        static bool ReadProductMembers(Type runtime, Type product, Type application)
        {
            productsProperty = AccessTools.Property(runtime, "Products");
            productIdProperty = AccessTools.Property(product, "ProductId");
            productTypeProperty = AccessTools.Property(product, "ProductType");
            rateProperty = AccessTools.Property(product, "InterestRate");
            feeProperty = AccessTools.Property(product, "OriginationFeeRate");
            termProperty = AccessTools.Property(product, "TermMonths");
            applicationsProperty = AccessTools.Property(runtime, "LoanApplications");
            suggestedAprProperty = AccessTools.Property(application, "SuggestedApr");
            requestedTermProperty = AccessTools.Property(application, "RequestedTermMonths");
            applicationStatusProperty = AccessTools.Property(application, "Status");
            markMutated = AccessTools.DeclaredMethod(runtime, "MarkStateMutated", new[] { typeof(string) });
            initializedProperty = AccessTools.Property(runtime, "IsInitialized");
            return ListOf(productsProperty, product) && Readable(productIdProperty, typeof(string))
                && productTypeProperty != null && Readable(productTypeProperty, productTypeProperty.PropertyType) && HasEnumValues(productTypeProperty.PropertyType, "Loan", "CreditCard")
                && Writable(rateProperty, typeof(decimal)) && Writable(feeProperty, typeof(decimal)) && Writable(termProperty, typeof(int))
                && ListOf(applicationsProperty, application) && Writable(suggestedAprProperty, typeof(decimal)) && Writable(requestedTermProperty, typeof(int))
                && applicationStatusProperty != null && Readable(applicationStatusProperty, applicationStatusProperty.PropertyType) && HasEnumValues(applicationStatusProperty.PropertyType, "Pending")
                && markMutated != null && markMutated.ReturnType == typeof(void) && !markMutated.IsStatic
                && Readable(initializedProperty, typeof(bool));
        }

        static bool ReadNegotiationMembers(Type runtime, Type negotiation)
        {
            casesProperty = AccessTools.Property(runtime, "NegotiationCases");
            caseIdProperty = AccessTools.Property(negotiation, "CaseId");
            caseTypeProperty = AccessTools.Property(negotiation, "CaseType");
            offeredRateProperty = AccessTools.Property(negotiation, "OfferedInterestRate");
            offeredFeeProperty = AccessTools.Property(negotiation, "OfferedOriginationFeeRate");
            offeredTermProperty = AccessTools.Property(negotiation, "OfferedTermMonths");
            return ListOf(casesProperty, negotiation) && Readable(caseIdProperty, typeof(string))
                && caseTypeProperty != null && Readable(caseTypeProperty, caseTypeProperty.PropertyType) && HasEnumValues(caseTypeProperty.PropertyType, "CorporateCreditFacility", "PreferredDeposit")
                && Writable(offeredRateProperty, typeof(decimal)) && Writable(offeredFeeProperty, typeof(decimal)) && Writable(offeredTermProperty, typeof(int));
        }

        public void Apply(Harmony harmony, Assembly target, IList<MethodBase> hooks)
        {
            var self = typeof(EmpireBankPatch);
            harmony.Patch(hooks[Gateway], prefix: new HarmonyMethod(self, "MoneyPrefix"));
            harmony.Patch(hooks[Emergency], prefix: new HarmonyMethod(self, "EmergencyPrefix"));
            harmony.Patch(hooks[EmergencyCard], prefix: new HarmonyMethod(self, "EmergencyPrefix"));
            harmony.Patch(hooks[LoanApplicants], transpiler: new HarmonyMethod(self, "ScoreTranspiler"));
            harmony.Patch(hooks[CardApplicants], transpiler: new HarmonyMethod(self, "ScoreTranspiler"));
            harmony.Patch(hooks[UpdateLoan], prefix: new HarmonyMethod(self, "UpdateLoanPrefix"));
            harmony.Patch(hooks[UpdateProduct], prefix: new HarmonyMethod(self, "UpdateProductPrefix"));
            harmony.Patch(hooks[AdjustRate], prefix: new HarmonyMethod(self, "AdjustRatePrefix"));
            harmony.Patch(hooks[SaveData], postfix: new HarmonyMethod(self, "SaveDataPostfix"));
            harmony.Patch(hooks[ApproveLoan], prefix: new HarmonyMethod(self, "ApproveLoanPrefix"));
            harmony.Patch(hooks[ApproveCard], prefix: new HarmonyMethod(self, "ApproveCardPrefix"));
            harmony.Patch(hooks[Negotiate], prefix: new HarmonyMethod(self, "NegotiatePrefix"));
            harmony.Patch(hooks[Finalize], prefix: new HarmonyMethod(self, "FinalizePrefix"));
            harmony.Patch(hooks[Stats], prefix: new HarmonyMethod(self, "StatsPrefix"));
            harmony.Patch(hooks[Supporters], prefix: new HarmonyMethod(self, "RemotePrefix"));
            Rules.BankProductLimits.Changed += OnLimitsChanged;
        }

        // Switched on mid-game: bring the loaded bank's products and pending applications within the limits
        // now, and record that as a bank change so the next save keeps it.
        static void OnLimitsChanged(bool enabled)
        {
            if (!enabled) return;
            try
            {
                var runtime = activeRuntime == null ? null : activeRuntime.Target;
                // Back at the main menu the runtime still holds the last save's products but no bank.
                if (runtime == null || !(bool)initializedProperty.GetValue(runtime, null)) return;
                if (Normalize(runtime) > 0) markMutated.Invoke(runtime, new object[] { "HardModePatchLimits" });
            }
            catch (Exception ex) { Log.Error("Empire Bank: products not brought within the limits: " + ex); }
        }

        static void MoneyPrefix(ref decimal amount, string reason)
        {
            try
            {
                if (!Rules.BankTaxWithdrawals.Enabled || amount <= 0m || reason == null) return;
                if (reason.IndexOf(WithdrawalReason, StringComparison.OrdinalIgnoreCase) < 0) return;
                var game = SaveGameManager.Current;
                if (game == null || game.gameVariables == null) return;
                decimal gross = amount;
                amount = BankRules.AfterTax(gross, game.gameVariables.taxPercentage);
                Log.Info("Empire Bank: withdrawal of " + gross.ToString("0.00") + " taxed at " + game.gameVariables.taxPercentage + "%, " + amount.ToString("0.00") + " paid out.");
            }
            catch (Exception ex) { Log.Error("Empire Bank: withdrawal left untaxed: " + ex); }
        }

        // Both entry points: the Risk Center card (its click handler) and the runtime method every path uses.
        // The card's handler is skipped too so it doesn't refresh the UI for funding that never came.
        static bool EmergencyPrefix()
        {
            if (!Rules.BankNoEmergencyBorrowing.Enabled) return true;
            try { Notifications.ShowError("hardmode_bank_emergency_blocked", "hardmodeEmergencyBlocked"); }
            catch (Exception) { }
            Log.Info("Empire Bank: emergency borrowing refused.");
            return false;
        }

        // Called in place of Math.Max(product.MinimumCreditScore, BellCredit(rng)), same arguments.
        public static int ApplicantScore(int productMinimum, int drawn)
        {
            return Rules.BankHonestCreditScores.Enabled ? BankRules.ApplicantScore(productMinimum, drawn) : Math.Max(productMinimum, drawn);
        }

        static IEnumerable<CodeInstruction> ScoreTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            var replacement = AccessTools.Method(typeof(EmpireBankPatch), "ApplicantScore");
            int replaced = 0;
            for (int index = 1; index < code.Count; index++)
            {
                if (!IsBellCredit(code[index - 1]) || !code[index].Calls(MathMax)) continue;
                code[index].opcode = OpCodes.Call;
                code[index].operand = replacement;
                replaced++;
            }
            if (replaced != 1) throw new InvalidOperationException("expected one credit score clamp, found " + replaced);
            return code;
        }

        static bool UpdateLoanPrefix(decimal apr, int termMonths, decimal feeRate, ref string message, ref bool __result)
        {
            if (!Rules.BankProductLimits.Enabled) return true;
            string refusal = BankRules.CheckLoan(apr, feeRate, termMonths);
            if (refusal == null) return true;
            message = refusal;
            __result = false;
            return false;
        }

        static bool UpdateProductPrefix(object __instance, string productId, decimal rate, int termMonths, decimal feeValue, ref string message, ref bool __result)
        {
            if (!Rules.BankProductLimits.Enabled) return true;
            try
            {
                string kind = KindOf(FindProduct(__instance, productId));
                string refusal = kind == "Loan" ? BankRules.CheckLoan(rate, feeValue, termMonths) : kind == "CreditCard" ? BankRules.CheckCard(rate) : null;
                if (refusal == null) return true;
                message = refusal;
                __result = false;
                return false;
            }
            catch (Exception ex) { Log.Error("Empire Bank: product edit not checked: " + ex); return true; }
        }

        // The mod saves the new rate as soon as it applies the change, so the change is limited first.
        static void AdjustRatePrefix(object __instance, string productId, ref decimal delta)
        {
            if (!Rules.BankProductLimits.Enabled || delta <= 0m) return;
            try
            {
                var product = FindProduct(__instance, productId);
                if (product == null) return;
                string kind = KindOf(product);
                delta = BankRules.ClampDelta(kind == "Loan", kind == "CreditCard", (decimal)rateProperty.GetValue(product, null), delta);
            }
            catch (Exception ex) { Log.Error("Empire Bank: rate change not checked: " + ex); }
        }

        // Products in a save may predate the limits (or the rule being switched on). New applications copy a
        // product's rate and term and approvals read its fee, so loaded products are brought within the limits.
        // Not marked as a bank change here: the bank binds its save afterwards, and loading always normalises
        // again, so a save written before any other bank change still loads within the limits.
        static void SaveDataPostfix(object __instance)
        {
            activeRuntime = new WeakReference(__instance);
            if (!Rules.BankProductLimits.Enabled) return;
            try { Normalize(__instance); }
            catch (Exception ex) { Log.Error("Empire Bank: saved products not checked: " + ex); }
        }

        static void ApproveLoanPrefix(object __instance, ref decimal apr, ref int term)
        {
            if (!Rules.BankProductLimits.Enabled) return;
            apr = BankRules.ClampRate(true, false, apr);
            term = BankRules.ClampTerm(term);
            try { Normalize(__instance); }   // the approval reads the product's fee
            catch (Exception ex) { Log.Error("Empire Bank: products not checked before approval: " + ex); }
        }

        static void ApproveCardPrefix(ref decimal apr)
        {
            if (Rules.BankProductLimits.Enabled) apr = BankRules.ClampRate(false, true, apr);
        }

        // Corporate facilities are negotiated, not made from a product. Offers are clamped before the mod
        // assesses them; preferred deposits are the bank's own cost and left alone.
        static void NegotiatePrefix(object __instance, string caseId, ref decimal rate, ref int termMonths, ref decimal feeRate)
        {
            if (!Rules.BankProductLimits.Enabled) return;
            try
            {
                string type = CaseTypeOf(FindCase(__instance, caseId));
                if (type == null || type == "PreferredDeposit") return;
                rate = BankRules.ClampRate(true, false, rate);
                feeRate = BankRules.ClampFee(feeRate);
                if (type == "CorporateCreditFacility") termMonths = BankRules.ClampTerm(termMonths);
            }
            catch (Exception ex) { Log.Error("Empire Bank: negotiation offer not checked: " + ex); }
        }

        // An offer accepted before the limits applied (an older save, or the rule switched on later) is
        // finalised from its stored terms, so those are brought within the limits first.
        static void FinalizePrefix(object __instance, string caseId)
        {
            if (!Rules.BankProductLimits.Enabled) return;
            try
            {
                var item = FindCase(__instance, caseId);
                string type = CaseTypeOf(item);
                if (type == null || type == "PreferredDeposit") return;
                offeredRateProperty.SetValue(item, BankRules.ClampRate(true, false, (decimal)offeredRateProperty.GetValue(item, null)), null);
                offeredFeeProperty.SetValue(item, BankRules.ClampFee((decimal)offeredFeeProperty.GetValue(item, null)), null);
                if (type == "CorporateCreditFacility") offeredTermProperty.SetValue(item, BankRules.ClampTerm((int)offeredTermProperty.GetValue(item, null)), null);
            }
            catch (Exception ex) { Log.Error("Empire Bank: accepted offer not checked: " + ex); }
        }

        static object FindCase(object runtime, string caseId)
        {
            var cases = casesProperty.GetValue(runtime, null) as IEnumerable;
            if (cases == null) return null;
            foreach (var item in cases)
                if (item != null && (string)caseIdProperty.GetValue(item, null) == caseId) return item;
            return null;
        }

        static string CaseTypeOf(object item)
        {
            return item == null ? null : caseTypeProperty.GetValue(item, null).ToString();
        }

        // Loan and card products within the limits, and pending loan applications too: they copied their
        // product's rate and term when they were made, and the approval screen shows those. Returns how many
        // objects changed.
        static int Normalize(object runtime)
        {
            int changed = 0;
            var products = productsProperty.GetValue(runtime, null) as IEnumerable;
            if (products != null)
            {
                foreach (var product in products)
                {
                    if (product == null) continue;
                    string kind = KindOf(product);
                    if (kind != "Loan" && kind != "CreditCard") continue;
                    bool touched = Clamp(product, rateProperty, BankRules.ClampRate(kind == "Loan", kind == "CreditCard", (decimal)rateProperty.GetValue(product, null)));
                    if (kind == "Loan")
                    {
                        touched |= Clamp(product, feeProperty, BankRules.ClampFee((decimal)feeProperty.GetValue(product, null)));
                        touched |= Clamp(product, termProperty, BankRules.ClampTerm((int)termProperty.GetValue(product, null)));
                    }
                    if (touched) changed++;
                }
            }
            var applications = applicationsProperty.GetValue(runtime, null) as IEnumerable;
            if (applications != null)
            {
                foreach (var application in applications)
                {
                    if (application == null || applicationStatusProperty.GetValue(application, null).ToString() != "Pending") continue;
                    bool touched = Clamp(application, suggestedAprProperty, BankRules.ClampRate(true, false, (decimal)suggestedAprProperty.GetValue(application, null)));
                    touched |= Clamp(application, requestedTermProperty, BankRules.ClampTerm((int)requestedTermProperty.GetValue(application, null)));
                    if (touched) changed++;
                }
            }
            if (changed > 0) Log.Info("Empire Bank: brought " + changed + " products and pending applications within the limits.");
            return changed;
        }

        static bool Clamp(object owner, PropertyInfo property, object value)
        {
            if (Equals(property.GetValue(owner, null), value)) return false;
            property.SetValue(owner, value, null);
            return true;
        }

        static object FindProduct(object runtime, string productId)
        {
            var products = productsProperty.GetValue(runtime, null) as IEnumerable;
            if (products == null) return null;
            foreach (var product in products)
                if (product != null && (string)productIdProperty.GetValue(product, null) == productId) return product;
            return null;
        }

        static string KindOf(object product)
        {
            return product == null ? null : productTypeProperty.GetValue(product, null).ToString();
        }

        static bool RemotePrefix()
        {
            return !Rules.BankNoRemoteRequests.Enabled;
        }

        // An empty coroutine in place of the request, so the callers' StartCoroutine still gets one.
        static bool StatsPrefix(ref IEnumerator __result)
        {
            if (!Rules.BankNoRemoteRequests.Enabled) return true;
            __result = new object[0].GetEnumerator();
            return false;
        }
    }
}
