using System.Collections.Generic;

namespace HardModePatch.Core
{
    public sealed class BakeryPrice
    {
        public readonly string ItemName;
        public readonly float Wholesale;
        public readonly float Market;

        public BakeryPrice(string itemName, float wholesale, float market)
        {
            ItemName = itemName;
            Wholesale = wholesale;
            Market = market;
        }

        public float Margin { get { return Market - Wholesale; } }
    }

    // Margins near the vanilla bakery and coffee items (croissant $4.29, cupcake $3.10, donut $2.28,
    // cup of coffee $3.00). The mod ships bread at $12/$50, bun $8/$28, cookie $5/$20, latte $2/$8.20 and
    // americano $3.50/$9.
    public static class BakeryPrices
    {
        public static readonly IList<BakeryPrice> All = new[]
        {
            new BakeryPrice("bakeryxx:itemname_bread", 3.00f, 7.50f),
            new BakeryPrice("bakeryxx:itemname_bun", 2.00f, 5.00f),
            new BakeryPrice("bakeryxx:itemname_chocolatecookie", 1.50f, 3.80f),
            new BakeryPrice("bakeryxx:itemname_latte", 1.10f, 4.50f),
            new BakeryPrice("bakeryxx:itemname_americano", 0.90f, 3.90f)
        };

        public static BakeryPrice For(string itemName)
        {
            foreach (var price in All) if (price.ItemName == itemName) return price;
            return null;
        }
    }
}
