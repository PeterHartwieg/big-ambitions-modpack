using System;

namespace HardModePatch.Core
{
    public static class BankRules
    {
        // Loan and card product limits. The mod allows up to 50% APR, a 10% origination fee and a one-month term.
        public const decimal MaxLoanApr = 0.15m;
        public const decimal MaxLoanFee = 0.03m;
        public const int MinLoanTermMonths = 6;
        public const decimal MaxCardApr = 0.30m;

        // Withdrawals reach the player as plain cash, which the game never taxes: it taxes business income
        // from the registrations, not transactions. The save's tax rate is taken at the moment of withdrawal.
        public static decimal AfterTax(decimal amount, int taxPercentage)
        {
            if (amount <= 0m) return amount;
            int rate = Math.Max(0, Math.Min(100, taxPercentage));
            return decimal.Round(amount * (100 - rate) / 100m, 2, MidpointRounding.AwayFromZero);
        }

        // Null when the loan product is allowed, otherwise the reason shown to the player.
        public static string CheckLoan(decimal apr, decimal feeRate, int termMonths)
        {
            if (apr > MaxLoanApr) return "HART. Mod Pack caps loan APR at " + Percent(MaxLoanApr) + ".";
            if (feeRate > MaxLoanFee) return "HART. Mod Pack caps the origination fee at " + Percent(MaxLoanFee) + ".";
            if (termMonths < MinLoanTermMonths) return "HART. Mod Pack requires a loan term of at least " + MinLoanTermMonths + " months.";
            return null;
        }

        public static string CheckCard(decimal apr)
        {
            return apr > MaxCardApr ? "HART. Mod Pack caps card APR at " + Percent(MaxCardApr) + "." : null;
        }

        public static decimal ClampRate(bool isLoan, bool isCard, decimal rate)
        {
            if (isLoan) return Math.Min(rate, MaxLoanApr);
            if (isCard) return Math.Min(rate, MaxCardApr);
            return rate;
        }

        // The rate change to pass on so that current + change stays within the cap. The mod saves the new
        // rate as soon as it changes it, so the change itself has to be limited, not the result.
        public static decimal ClampDelta(bool isLoan, bool isCard, decimal current, decimal delta)
        {
            decimal capped = ClampRate(isLoan, isCard, current + delta);
            return capped < current + delta ? Math.Max(0m, capped - current) : delta;
        }

        public static decimal ClampFee(decimal feeRate) { return Math.Min(feeRate, MaxLoanFee); }
        public static int ClampTerm(int termMonths) { return Math.Max(termMonths, MinLoanTermMonths); }

        // The mod draws an applicant's score, then raises it to the product's minimum, so a minimum of 850
        // turns every applicant into a perfect borrower. The drawn score is kept instead.
        public static int ApplicantScore(int productMinimum, int drawn)
        {
            return drawn;
        }

        static string Percent(decimal rate) { return (rate * 100m).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "%"; }
    }
}
