using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using HARTModPack.Core;
using Xunit;

public class PackTests
{
    [Fact]
    public void Members_match_the_release_plan()
    {
        var required = Pack.Members.Where(m => m.Required).Select(m => m.AssemblyName).ToArray();
        Assert.Equal(new[] { "RealLifeOverhaul", "DynamicRivals", "HARTRivalBalance", "CityPerformance", "FoodTrucks", "DrivenRides",
            "Roadster", "AlcwareSeasons", "RetailCapacityUpgrades", "InternetCafe", "Bakery", "PolyModsHyperreal", "CampaignObjectives" }, required);
        var optional = Pack.Members.Where(m => !m.Required).Select(m => m.AssemblyName).ToArray();
        Assert.Equal(new[] { "CameraTools", "HARTFreeCamera", "BigCopilotLink" }, optional);
    }

    [Fact]
    public void Persistent_City_is_never_a_member()
    {
        Assert.Null(Pack.Find("PersistentCity"));
        Assert.True(Pack.IsExcluded("persistentcity"));
    }

    [Fact]
    public void Find_ignores_case_like_the_game()
    {
        Assert.Equal("RealLifeOverhaul", Pack.Find("reallifeoverhaul")!.AssemblyName);
        Assert.Null(Pack.Find(null!));
    }

    [Fact]
    public void Every_tested_version_entry_is_a_member()
    {
        foreach (var name in TestedVersions.Members) Assert.NotNull(Pack.Find(name));
    }
}

public class VersionTests
{
    [Theory]
    [InlineData("0.45.0+b12d3581860ce380397223a2790fcce8b72b3482", "0.45.0")]
    [InlineData("1.2.0", "1.2.0")]
    [InlineData(" 0.6.2-citywide-traffic-prototype ", "0.6.2-citywide-traffic-prototype")]
    public void Informational_version_wins_without_build_metadata(string info, string expected)
    {
        Assert.Equal(expected, TestedVersions.Exposed(info, new Version(9, 9, 9, 9)));
    }

    [Fact]
    public void Assembly_version_is_used_when_no_informational_version()
    {
        Assert.Equal("0.2.0", TestedVersions.Exposed(null, new Version(0, 2, 0, 0)));
        Assert.Equal("1.1.1", TestedVersions.Exposed("", new Version(1, 1, 1, 0)));
        Assert.Equal("1.2.3.4", TestedVersions.Exposed(null, new Version(1, 2, 3, 4)));
        Assert.Equal("1.2.0", TestedVersions.Exposed(null, new Version(1, 2)));
    }

    [Fact]
    public void Zero_version_counts_as_not_exposed()
    {
        Assert.Null(TestedVersions.Exposed(null, new Version(0, 0, 0, 0)));
        Assert.Null(TestedVersions.Exposed(null, null));
        Assert.Equal(VersionVerdict.NotExposed, TestedVersions.Check("Bakery", null));
    }

    [Fact]
    public void Tested_table_is_pinned_for_this_release()
    {
        Assert.Equal(new[] { "0.45.0", "0.49.0", "0.50.0" }, TestedVersions.For("RealLifeOverhaul"));
        Assert.Empty(TestedVersions.For("HardModePatch"));
        Assert.Equal(new[] { "0.5.0" }, TestedVersions.For("CityPerformance"));
        Assert.Equal(new[] { "0.2.0" }, TestedVersions.For("HARTRivalBalance"));
        Assert.Equal(new[] { "1.2.0", "1.2.1" }, TestedVersions.For("CampaignObjectives"));
        Assert.Equal(VersionVerdict.Tested, TestedVersions.Check("RealLifeOverhaul", "0.49.0"));
        Assert.Equal(VersionVerdict.Untested, TestedVersions.Check("RealLifeOverhaul", "0.51.0"));
        // A member with no table entry that starts exposing a version is reported, so the table gets updated.
        Assert.Equal(VersionVerdict.Untested, TestedVersions.Check("Bakery", "1.3.0"));
    }
}

public class TurnOnPlannerTests
{
    static SubscribedItem Item(ulong id, string name, bool enabled = false) => new SubscribedItem { Id = id, AssemblyName = name, Enabled = enabled };

    [Fact]
    public void Turns_on_required_members_left_off()
    {
        var plan = TurnOnPlanner.Plan(new[] { Item(1, "RealLifeOverhaul"), Item(2, "Bakery", true), Item(3, "Roadster") }, new string[0], new HashSet<ulong>());
        Assert.Equal(new ulong[] { 1, 3 }, plan.ToAdd.OrderBy(x => x));
    }

    [Fact]
    public void Never_turns_on_optional_unknown_or_excluded_mods()
    {
        var plan = TurnOnPlanner.Plan(new[] { Item(1, "CameraTools"), Item(2, "PersistentCity"), Item(3, "EconomyExpansion"), Item(4, null!) }, new string[0], new HashSet<ulong>());
        Assert.Empty(plan.ToAdd);
    }

    [Fact]
    public void Leaves_the_Workshop_copy_off_when_ModsLocal_has_the_mod()
    {
        var plan = TurnOnPlanner.Plan(new[] { Item(1, "CityPerformance") }, new[] { "cityperformance" }, new HashSet<ulong>());
        Assert.Empty(plan.ToAdd);
        Assert.Single(plan.Notes);
    }

    [Fact]
    public void Leaves_off_a_member_it_turned_on_before()
    {
        var plan = TurnOnPlanner.Plan(new[] { Item(7, "Bakery") }, new string[0], new HashSet<ulong> { 7 });
        Assert.Empty(plan.ToAdd);
        Assert.Contains("left off", plan.Notes.Single());
    }

    [Fact]
    public void Leaves_off_when_another_copy_is_already_on_or_two_copies_compete()
    {
        var already = TurnOnPlanner.Plan(new[] { Item(1, "Bakery", true), Item(2, "Bakery") }, new string[0], new HashSet<ulong>());
        Assert.Empty(already.ToAdd);
        var two = TurnOnPlanner.Plan(new[] { Item(1, "Bakery"), Item(2, "Bakery") }, new string[0], new HashSet<ulong>());
        Assert.Empty(two.ToAdd);
        Assert.Contains("More than one", two.Notes.Single());
        // Still ambiguous when one of the two was turned on before.
        var history = TurnOnPlanner.Plan(new[] { Item(1, "Bakery"), Item(2, "Bakery") }, new string[0], new HashSet<ulong> { 1 });
        Assert.Empty(history.ToAdd);
    }
}

public class StatusTests
{
    static LoadedCopy Copy(string name, ModSource source, string? info = null, Version? version = null) =>
        new LoadedCopy { AssemblyName = name, Source = source, ModId = source == ModSource.Workshop ? "1" : "/ModsLocal/" + name, InformationalVersion = info, AssemblyVersion = version ?? new Version(0, 0, 0, 0) };

    static MemberStatus Of(List<MemberStatus> all, string name) => all.Single(s => s.Member.AssemblyName == name);

    [Fact]
    public void Reports_each_state()
    {
        var snap = new PackSnapshot { Steam = SteamState.Listed };
        snap.Loaded.Add(Copy("RealLifeOverhaul", ModSource.Workshop, "0.51.0+abc"));
        snap.Loaded.Add(Copy("CityPerformance", ModSource.Workshop, "0.5.0"));
        snap.Loaded.Add(Copy("CityPerformance", ModSource.Local, "0.5.0"));
        snap.Loaded.Add(Copy("Bakery", ModSource.Local));
        snap.Subscribed.Add(new SubscribedItem { Id = 5, AssemblyName = "Roadster", Enabled = false });
        snap.Subscribed.Add(new SubscribedItem { Id = 6, AssemblyName = "DrivenRides", Enabled = false });
        snap.Subscribed.Add(new SubscribedItem { Id = 8, AssemblyName = "InternetCafe", Enabled = true });
        snap.TurnedOnThisLaunch.Add(6);
        snap.Failed.Add(new FailedMod { AssemblyName = "DynamicRivals", Reason = "Dynamic Rivals: duplicate" });
        var all = StatusEvaluator.Evaluate(snap);

        Assert.Equal(16, all.Count);
        Assert.Equal(VersionVerdict.Untested, Of(all, "RealLifeOverhaul").Verdict);
        Assert.Equal("0.51.0", Of(all, "RealLifeOverhaul").Version);
        Assert.True(Of(all, "CityPerformance").LoadedTwice);
        Assert.Equal(VersionVerdict.Tested, Of(all, "CityPerformance").Verdict);
        Assert.Equal(VersionVerdict.NotExposed, Of(all, "Bakery").Verdict);
        Assert.Equal(MemberState.SwitchedOff, Of(all, "Roadster").State);
        Assert.Equal(MemberState.TurnedOn, Of(all, "DrivenRides").State);
        Assert.Equal(MemberState.NotLoaded, Of(all, "InternetCafe").State);
        Assert.Equal(MemberState.Failed, Of(all, "DynamicRivals").State);
        Assert.Equal(MemberState.Missing, Of(all, "CameraTools").State);
    }

    [Fact]
    public void Notice_lists_required_problems_only()
    {
        var snap = new PackSnapshot { Steam = SteamState.Listed };
        foreach (var m in Pack.Members.Where(m => m.Required && m.AssemblyName != "Bakery")) snap.Loaded.Add(Copy(m.AssemblyName, ModSource.Workshop));
        var all = StatusEvaluator.Evaluate(snap);
        var notice = Texts.Check(all);
        Assert.True(notice.Any);
        Assert.Contains("Missing: Bakery - V1.2.", notice.Text);
        Assert.DoesNotContain("Camera Tools", notice.Text);
        Assert.StartsWith(Pack.Version + "|", notice.Fingerprint);
    }

    [Fact]
    public void Notice_names_restarts_and_failures()
    {
        var snap = new PackSnapshot { Steam = SteamState.Listed };
        foreach (var m in Pack.Members.Where(m => m.Required && m.AssemblyName != "Roadster" && m.AssemblyName != "Bakery")) snap.Loaded.Add(Copy(m.AssemblyName, ModSource.Workshop));
        snap.Subscribed.Add(new SubscribedItem { Id = 9, AssemblyName = "Roadster", Enabled = true });
        snap.Failed.Add(new FailedMod { AssemblyName = "Bakery", Reason = "Bakery: didn't start." });
        var text = Texts.Check(StatusEvaluator.Evaluate(snap)).Text;
        Assert.Contains("Restart the game to load: HART. RDSTR.", text);
        Assert.Contains("Didn't load: Bakery - V1.2.", text);
        Assert.DoesNotContain("Missing", text);
    }

    [Fact]
    public void Mod_discovered_after_startup_waits_for_a_restart()
    {
        var snap = new PackSnapshot { Steam = SteamState.Listed };
        snap.AwaitingRestart.Add("FoodTrucks");
        var all = StatusEvaluator.Evaluate(snap);
        Assert.Equal(MemberState.NotLoaded, Of(all, "FoodTrucks").State);
        Assert.Contains("Restart the game to load: HART. Street Food.", Texts.Check(all).Text);
    }

    [Fact]
    public void Notice_is_empty_when_the_pack_is_complete()
    {
        var snap = new PackSnapshot { Steam = SteamState.Listed };
        foreach (var m in Pack.Members.Where(m => m.Required)) snap.Loaded.Add(Copy(m.AssemblyName, ModSource.Workshop));
        snap.Loaded.Single(c => c.AssemblyName == "RealLifeOverhaul").InformationalVersion = "0.49.0";
        snap.Loaded.Single(c => c.AssemblyName == "CityPerformance").InformationalVersion = "0.5.0";
        snap.Loaded.Single(c => c.AssemblyName == "HARTRivalBalance").AssemblyVersion = new Version(0, 2, 0, 0);
        snap.Loaded.Single(c => c.AssemblyName == "CampaignObjectives").InformationalVersion = "1.2.0";
        var all = StatusEvaluator.Evaluate(snap);
        Assert.False(Texts.Check(all).Any);
        Assert.Equal("All 13 required mods are on.", Texts.Summary(all, SteamState.Listed));
    }

    [Fact]
    public void Optional_member_loaded_twice_is_reported()
    {
        var snap = new PackSnapshot();
        snap.Loaded.Add(Copy("HARTFreeCamera", ModSource.Workshop, null, new Version(1, 1, 0, 0)));
        snap.Loaded.Add(Copy("HARTFreeCamera", ModSource.Local, null, new Version(1, 1, 1, 0)));
        var notice = Texts.Check(StatusEvaluator.Evaluate(snap));
        Assert.Contains("Loaded from both the Workshop and ModsLocal: HART Free Camera", notice.Text);
        Assert.Contains("Steam isn't connected", Texts.Summary(StatusEvaluator.Evaluate(snap), SteamState.NotConnected));
        Assert.Contains("didn't list", Texts.Summary(StatusEvaluator.Evaluate(snap), SteamState.ListingFailed));
    }

    [Fact]
    public void Excluded_mod_that_is_on_is_mentioned()
    {
        var snap = new PackSnapshot();
        snap.Loaded.Add(Copy("PersistentCity", ModSource.Local, "0.6.2"));
        Assert.Equal(new[] { "PersistentCity" }, StatusEvaluator.ExcludedOn(snap));
        Assert.DoesNotContain(StatusEvaluator.Evaluate(snap), s => s.Member.AssemblyName == "PersistentCity");
    }

    [Fact]
    public void Lines_read_plainly()
    {
        var on = new MemberStatus { Member = Pack.Find("RealLifeOverhaul")!, State = MemberState.On, Version = "0.50.0", Verdict = VersionVerdict.Untested, FromWorkshop = true };
        Assert.Equal("Real Life Overhaul: on, 0.50.0, not tested with this pack, Workshop", Texts.Line(on));
        var optional = new MemberStatus { Member = Pack.Find("CameraTools")!, State = MemberState.Missing };
        Assert.Equal("Camera Tools: not installed (optional)", Texts.Line(optional));
        Assert.Equal("HART. Mod Pack turned on 3 mods. Restart the game to load them.", Texts.TurnedOn(3));
        Assert.Equal("HART. Mod Pack turned on 1 mod. Restart the game to load it.", Texts.TurnedOn(1));
    }

    [Fact]
    public void Shipped_text_has_no_em_dashes()
    {
        var snap = new PackSnapshot();
        var all = StatusEvaluator.Evaluate(snap);
        var texts = all.Select(Texts.Line).Concat(new[] { Texts.Check(all).Text, Texts.SettingsReminder(), Texts.Summary(all, SteamState.ListingFailed), Texts.TurnedOn(2) });
        foreach (var t in texts) Assert.DoesNotContain("—", t);
    }
}

public class NoticePolicyTests
{
    [Fact]
    public void Check_notice_stays_hidden_after_a_click_until_the_list_changes()
    {
        var notice = new Notice { Text = "x", Fingerprint = Pack.Version + "|aaaa" };
        Assert.True(NoticePolicy.ShowCheck(notice, null!, false, SteamState.Listed));
        Assert.False(NoticePolicy.ShowCheck(notice, Pack.Version + "|aaaa", false, SteamState.Listed));
        Assert.True(NoticePolicy.ShowCheck(notice, Pack.Version + "|bbbb", false, SteamState.Listed));
        Assert.True(NoticePolicy.ShowCheck(notice, "0.9.0|aaaa", false, SteamState.Listed));
        Assert.False(NoticePolicy.ShowCheck(notice, null!, true, SteamState.Listed));
        Assert.False(NoticePolicy.ShowCheck(new Notice(), null!, false, SteamState.Listed));
        Assert.False(NoticePolicy.ShowCheck(notice, null!, false, SteamState.ListingFailed));
        Assert.True(NoticePolicy.ShowCheck(notice, null!, false, SteamState.NotConnected));
    }

    [Fact]
    public void Settings_reminder_shows_once_per_pack_version()
    {
        Assert.True(NoticePolicy.ShowReminder(null!, true, false));
        Assert.False(NoticePolicy.ShowReminder(Pack.Version, true, false));
        Assert.True(NoticePolicy.ShowReminder("0.9.0", true, false));
        Assert.False(NoticePolicy.ShowReminder(null!, false, false));
        Assert.False(NoticePolicy.ShowReminder(null!, true, true));
    }
}

public class LocaleTests
{
    [Fact]
    public void Status_table_is_valid_json_and_round_trips()
    {
        var lines = new[] { "Plain", "Quote \" and backslash \\", "Tab\tand\nnewline", "\u0001" };
        var doc = JsonDocument.Parse(LocaleFile.Build(lines));
        for (int i = 0; i < lines.Length; i++) Assert.Equal(lines[i], doc.RootElement.GetProperty(LocaleFile.Key(i)).GetString());
    }

    [Fact]
    public void Shipped_locale_has_every_key()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Locales", "en.json");
        var doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var key in LocaleKeys.All) Assert.True(doc.RootElement.TryGetProperty(key, out _), key);
        Assert.Contains("{text}", doc.RootElement.GetProperty(LocaleKeys.Notice).GetString());
    }

    [Fact]
    public void Status_keys_are_lower_case_because_the_game_lowers_keys()
    {
        Assert.Equal(LocaleFile.Key(3), LocaleFile.Key(3).ToLowerInvariant());
        foreach (var key in LocaleKeys.All) Assert.Equal(key, key.ToLowerInvariant());
    }
}
