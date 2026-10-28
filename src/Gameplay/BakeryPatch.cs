using System;
using System.Collections.Generic;
using System.Reflection;
using BigAmbitions.Items;
using HardModePatch.Core;
using HarmonyLib;
using UnityEngine;

namespace HardModePatch
{
    // Bakery (3810927743) registers five products whose margins are five to ten times vanilla food. Their
    // wholesale and default market prices are rewritten as the items register, and once more for items
    // already registered when this patch arrived. Rival bakeries price from the same items. A business
    // that already set its own selling prices keeps them.
    public sealed class BakeryPatch : ITargetPatch
    {
        static FieldInfo marketPriceField;
        static FieldInfo itemsField;
        // Prices as the mod shipped them, per item asset this patch changed, for an unload mid-game.
        static readonly Dictionary<Item, KeyValuePair<float, float>> originals = new Dictionary<Item, KeyValuePair<float, float>>();

        public TargetInfo Target { get { return Targets.Bakery; } }

        public IList<MethodBase> ResolveHooks(Assembly target)
        {
            var bakery = target.GetType("Bakery", false);
            marketPriceField = AccessTools.Field(typeof(Item), "defaultMarketPrice");
            itemsField = AccessTools.Field(typeof(ItemsGetter), "ItemsDictionary");
            MethodBase register = marketPriceField != null && marketPriceField.FieldType == typeof(float) && itemsField != null
                ? AccessTools.Method(typeof(ItemsGetter), "RegisterModItem", new[] { typeof(Item) })
                : null;
            MethodBase bakeryLoad = bakery == null ? null : AccessTools.Method(bakery, "OnLoadAsync");
            return new MethodBase[] { register, bakeryLoad };
        }

        // OnLoadAsync names the items the price table is keyed by.
        static readonly Contract LoadBody = new Contract("Bakery.OnLoadAsync", "d0fd788dfc9bc258");

        public IList<ContractCheck> Contracts(Assembly target, IList<MethodBase> hooks)
        {
            return new[] { new ContractCheck(hooks[1], LoadBody) };
        }

        public void Revert()
        {
            foreach (var pair in originals)
            {
                if (pair.Key == null) continue;
                pair.Key.wholesalePrice = pair.Value.Key;
                marketPriceField.SetValue(pair.Key, pair.Value.Value);
            }
            if (originals.Count > 0) Log.Info("Bakery: restored the published prices of " + originals.Count + " items.");
            originals.Clear();
        }

        public void Apply(Harmony harmony, Assembly target, IList<MethodBase> hooks)
        {
            harmony.Patch(hooks[0], prefix: new HarmonyMethod(typeof(BakeryPatch), "RegisterPrefix"));
            if (!Rules.BakeryVanillaMargins.Enabled) return;
            var items = itemsField.GetValue(null) as Dictionary<string, Item>;
            if (items == null) return;
            foreach (var price in BakeryPrices.All)
            {
                Item item;
                if (items.TryGetValue(price.ItemName, out item) && item != null) Reprice(item, price);
            }
        }

        static void RegisterPrefix(Item item)
        {
            try
            {
                if (!Rules.BakeryVanillaMargins.Enabled || item == null) return;
                var price = BakeryPrices.For(item.itemName);
                if (price != null) Reprice(item, price);
            }
            catch (Exception ex) { Log.Error("Bakery: price left as published: " + ex); }
        }

        static void Reprice(Item item, BakeryPrice price)
        {
            if (!originals.ContainsKey(item)) originals[item] = new KeyValuePair<float, float>(item.wholesalePrice, (float)marketPriceField.GetValue(item));
            item.wholesalePrice = price.Wholesale;
            marketPriceField.SetValue(item, price.Market);
            Log.Info("Bakery: " + price.ItemName + " now " + price.Wholesale.ToString("0.00") + " wholesale, " + price.Market.ToString("0.00") + " market.");
        }
    }
}
