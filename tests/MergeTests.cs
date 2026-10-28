using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using HARTModPack.Core;
using Xunit;

public class MergeTests
{
    [Fact]
    public void Old_patch_is_never_a_required_member_or_turned_on()
    {
        Assert.Null(Pack.Find("HardModePatch"));
        Assert.Empty(TurnOnPlanner.Plan(new[] { new SubscribedItem { Id = 42, AssemblyName = "HardModePatch" } }, Array.Empty<string>(), new HashSet<ulong>()).ToAdd);
        Assert.Contains(Pack.Members, m => m.AssemblyName == "HARTRivalBalance" && m.Required);
        Assert.Contains(Pack.Members, m => m.AssemblyName == "CityPerformance" && m.Required);
        Assert.Equal("HART. Mod Pack", Pack.SettingsModTitle);
        Assert.Equal("HARTModPack", HardModePatch.Core.SeriesSettings.Self);
    }

    [Fact]
    public void Old_patch_first_prevents_start_and_never_recovers_without_restart()
    {
        int started = 0, stopped = 0, notices = 0;
        using var guard = new LegacyPatchGuard(() => started++, () => stopped++, () => notices++);
        guard.Observe("HardModePatch");
        guard.Begin();
        guard.Observe("hardmodepatch");
        guard.Dispose();
        guard.Begin();
        Assert.True(guard.Blocked);
        Assert.False(guard.Active);
        Assert.Equal(0, started);
        Assert.Equal(0, stopped);
        Assert.Equal(1, notices);
    }

    [Fact]
    public void Pack_first_stops_synchronously_before_the_legacy_loader_continues()
    {
        var events = new List<string>();
        using var guard = new LegacyPatchGuard(() => events.Add("pack started"), () => events.Add("pack stopped"), () => events.Add("notice"));
        guard.Begin();
        guard.Observe("CityPerformance");
        Assert.True(guard.Active);
        guard.Observe("HardModePatch");
        events.Add("legacy entry runs");
        guard.Observe("HardModePatch");
        guard.Dispose();
        Assert.Equal(new[] { "pack started", "pack stopped", "notice", "legacy entry runs" }, events);
        Assert.False(guard.Active);
    }

    [Fact]
    public void Failed_start_cleans_up_and_unload_is_idempotent()
    {
        int stops = 0;
        using var guard = new LegacyPatchGuard(() => throw new InvalidOperationException(), () => stops++, () => { });
        Assert.Throws<InvalidOperationException>(() => guard.Begin());
        guard.Dispose();
        Assert.Equal(1, stops);
        Assert.False(guard.Active);
    }

    [Fact]
    public void Conflict_notice_has_both_removal_paths_and_restart()
    {
        Assert.Contains("Unsubscribe from HART. Mod Pack Patch", Texts.LegacyPatchNotice);
        Assert.Contains("ModsLocal", Texts.LegacyPatchNotice);
        Assert.Contains("restart", Texts.LegacyPatchNotice);
        Assert.Contains("paused", Texts.LegacyPatchNotice);
    }

    [Fact]
    public void Release_manifest_matches_the_compiled_pack_and_version_pins()
    {
        var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "modpack.json"))).RootElement;
        Assert.Equal(Pack.Version, manifest.GetProperty("version").GetString());
        var members = manifest.GetProperty("members").EnumerateArray().ToArray();
        Assert.Equal(Pack.Members.Select(m => m.AssemblyName), members.Select(m => m.GetProperty("assembly").GetString()));
        foreach (var member in members)
        {
            var name = member.GetProperty("assembly").GetString()!;
            Assert.Equal(Pack.Find(name)!.Required, member.GetProperty("required").GetBoolean());
            Assert.Equal(TestedVersions.For(name), member.GetProperty("testedVersions").EnumerateArray().Select(x => x.GetString()));
        }
    }
}
