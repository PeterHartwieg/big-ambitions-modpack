namespace HARTModPack.Core
{
    public static class ButtonCaptions
    {
        public const string ApplyLabelKey = "hardmode_series_apply";
        public const string ApplyKey = "hartmodpack_button_apply";

        public static string KeyFor(string label)
        {
            if (label == ApplyLabelKey) return ApplyKey;
            if (label == LocaleKeys.CheckAgain) return LocaleKeys.CheckAgain;
            return null;
        }
    }
}
