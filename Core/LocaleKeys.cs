namespace HARTModPack.Core
{
    // Keys shipped in config/Locales/en.json. A test checks every one of them is there.
    public static class LocaleKeys
    {
        public const string LegacyPatch = "hartmodpack_legacy_patch";
        public const string Title = "hartmodpack_title";
        public const string Checking = "hartmodpack_checking";
        public const string CheckAgain = "hartmodpack_check_again";
        public const string MembersHeader = "hartmodpack_members_header";
        public const string Notice = "hartmodpack_notice";
        public const string Unavailable = "hartmodpack_unavailable";

        public static readonly string[] All = { Title, LegacyPatch, Checking, CheckAgain, MembersHeader, Notice, Unavailable, ButtonCaptions.ApplyKey };
    }
}
