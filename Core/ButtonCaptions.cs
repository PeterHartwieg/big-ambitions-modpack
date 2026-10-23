namespace HARTModPack.Core
{
    public static class ButtonCaptions
    {
        public const string ApplyKey = "hartmodpack_button_apply";

        public static string KeyFor(string label)
        {
            if (label == "hardmode_series_apply") return ApplyKey;
            if (label == LocaleKeys.CheckAgain) return LocaleKeys.CheckAgain;
            return null;
        }
    }
}
