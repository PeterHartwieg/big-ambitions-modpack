using System.IO;
using System.Linq;
using System.Text.Json;
using HardModePatch.Core;
using Xunit;

public class FactoryDiplomaTests
{
    [Fact]
    public void DiplomasUseFreeStableMaskIdsAndThreeWeekSchoolTimetables()
    {
        Assert.Equal(new[] { 18, 19, 34 }, FactoryDiplomas.All.Select(c => c.Id));
        Assert.Equal(8, FactoryDiplomas.Sessions);
        Assert.Equal(4, FactoryDiplomas.Homework);
        Assert.Equal(4, FactoryDiplomas.Hours);
        Assert.All(FactoryDiplomas.All, c =>
        {
            Assert.InRange(c.Id, 0, 63);
            Assert.InRange(c.Fee, 3500, 4000);
            Assert.Equal(3, Enumerable.Range(0, 7).Count(d => (c.Days & (1 << d)) != 0));
            Assert.InRange(c.Hour, 8, 18);
        });
        for (int i = 0; i < FactoryDiplomas.All.Length; i++)
        for (int j = i + 1; j < FactoryDiplomas.All.Length; j++)
        {
            var a = FactoryDiplomas.All[i]; var b = FactoryDiplomas.All[j];
            Assert.True((a.Days & b.Days) == 0 || a.Hour + FactoryDiplomas.Hours <= b.Hour || b.Hour + FactoryDiplomas.Hours <= a.Hour);
        }
    }

    [Fact]
    public void CollisionAndMaskOverflowPreventTheWholeInjection()
    {
        Assert.True(FactoryDiplomas.CanInject(new[] { 0, 10, 17, 20, 33, 40, 61 }, 64));
        foreach (var c in FactoryDiplomas.All) Assert.False(FactoryDiplomas.CanInject(new[] { c.Id }, 64));
        Assert.False(FactoryDiplomas.CanInject(new int[0], 34));
    }

    [Fact]
    public void EveryDependencyMustBeReadyBeforeGateCanRefuse()
    {
        for (int bits = 0; bits < 32; bits++)
            Assert.Equal(bits == 31, FactoryDiplomas.GateReady((bits & 1) != 0, (bits & 2) != 0, (bits & 4) != 0, (bits & 8) != 0, (bits & 16) != 0));
    }

    [Fact]
    public void OnlyTheMatchingDiplomaUnlocksItsMachineAndLaserStaysFree()
    {
        foreach (var c in FactoryDiplomas.All)
        {
            Assert.Same(c, FactoryDiplomas.ForItem(c.Item));
            Assert.Same(c, FactoryDiplomas.ForId(c.Id));
            foreach (bool ready in new[] { false, true })
            foreach (bool held in new[] { false, true })
                Assert.Equal(ready && !held, FactoryDiplomas.Refuse(c.Item, ready, held));
        }
        foreach (var item in new[] { null, "", "ba:itemname_lasercuttingmachine", "ba:itemname_consumergoodsassemblymachine" })
            Assert.False(FactoryDiplomas.Refuse(item, true, false));
    }

    [Fact]
    public void CourseNamesDescriptionsAndRefusalsUseCampusLocaleKeys()
    {
        foreach (var locale in Directory.GetFiles("Locales", "*.json"))
        {
            var labels = JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, string>>(File.ReadAllText(locale))!;
            foreach (var c in FactoryDiplomas.All)
            foreach (var prefix in new[] { "reallife_study_course_", "reallife_study_about_", "hardmode_factory_requires_" })
                Assert.False(string.IsNullOrWhiteSpace(labels[prefix + c.Key]));
        }
        Assert.False(Rules.FactoryDiplomas.Default);
    }
}
