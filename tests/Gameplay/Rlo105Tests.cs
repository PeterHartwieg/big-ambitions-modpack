using HardModePatch.Core;
using Xunit;

// RLO 1.0.5 review (docs/rlo-compatibility.md): what changed for the pack's rules.
public class Rlo105Tests
{
    [Fact]
    public void Rlo1CatalogueOwnsTheDiplomaIds()
    {
        // RLO 0.50.0 left 18, 19 and 34 free; RLO 1.0 uses them for bricklaying, the crane ticket and construction management.
        Assert.False(FactoryDiplomas.IdsTaken(new[] { 0, 10, 17, 20, 33, 35, 61 }));
        Assert.False(FactoryDiplomas.IdsTaken(new int[0]));
        Assert.True(FactoryDiplomas.IdsTaken(new[] { 0, 17, 18, 19, 20, 34, 62 }));
        foreach (var c in FactoryDiplomas.All) Assert.True(FactoryDiplomas.IdsTaken(new[] { 1, c.Id }));
        Assert.False(FactoryDiplomas.CanInject(new[] { 0, 17, 18, 19, 20, 34, 62 }, 64));
    }

    // HMP's records as RLO 0.50.0 wrote them (24 fields), and RLO 1.0's own building-trade records (25 fields).
    const string Hmp19 = "19|0|20|3|3|26|1|3900|0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|-1";
    const string Hmp18Passed = "18|0|15|8|8|21|4|3600|0|0|0|0|2|22|27|0|71|2|23|0|0|0|0|-1";
    const string Ordinary = "5|0|2|1|1|8|0|900|0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|-1";
    const string Bricklaying = "18|0|40|3|3|46|1|2400|0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|-1|0";
    const string CraneTicket = "19|0|30|3|3|35|1|650|0|0|0|0|2|36|38|0|70|2|38|0|0|0|0|-1|0";

    [Fact]
    public void Rlo1HoldsOnlyRecordsWrittenBeforeRloTookTheIds()
    {
        // A 0.50.0 save with HMP diplomas, loaded under RLO 1.0: both diploma records are held, the ordinary course is not.
        string legacy = "v=1~h:7|1|30~c:" + Hmp19 + ";" + Ordinary + "~r:" + Hmp18Passed + "~w:~q:~t:-1|0";
        string held = DiplomaHold.KeepWrittenBeforeRlo1(legacy, null, 31)!;
        Assert.Equal(2, DiplomaHold.Count(held));
        Assert.Contains("31#" + Hmp19, held);
        Assert.Contains("31#" + Hmp18Passed, held);
        Assert.DoesNotContain(Ordinary, held);
        // RLO 1.0 read the records as its own courses and saved them with 25 fields: the hold stays exactly as it was.
        string resaved = "v=1~h:7|1|40~c:" + Hmp19 + "|0;" + Ordinary + "|0~r:" + Hmp18Passed + "|0~w:~q:~t:-1|0";
        Assert.Equal(held, DiplomaHold.KeepWrittenBeforeRlo1(resaved, held, 41));
        // RLO 1.0's own building-trade records are never held.
        string own = "v=1~h:7|1|50~c:" + Bricklaying + "~r:" + CraneTicket + "~w:~q:~t:-1|0";
        Assert.Null(DiplomaHold.KeepWrittenBeforeRlo1(own, null, 51));
        Assert.Equal(held, DiplomaHold.KeepWrittenBeforeRlo1(own, held, 51));
        // Back on an RLO that reads HMP's courses, the held originals are given back.
        var back = DiplomaHold.Restore("v=1~h:7|1|60~c:" + Ordinary + "~r:~w:~q:~t:-1|0", held, 61, 4, 40)!;
        Assert.True(back.Done);
        Assert.Equal(2, back.Records);
        Assert.Contains(Hmp18Passed, back.Careers);
        Assert.Contains("19|0|", back.Careers);
    }

    [Fact]
    public void Rlo1PhoneApplicationUsesTheApplyLabel()
    {
        Assert.Equal("hardmode_closed_apply", UtilityClosureReason.Key("reallife_util_closed_apply_app", true, 47, 49));
        Assert.Null(UtilityClosureReason.Key("reallife_util_closed_apply_app", true, 49, 49));
        Assert.Null(UtilityClosureReason.Key("reallife_util_closed_apply_app", false, 47, 49));
    }
}
