using System.Linq;
using HardModePatch.Core;
using Xunit;

public class DiplomaHoldTests
{
    // Records as RLO 0.49.0's CareersStore writes them: course|mode|start|seen|done|settled|homework|fee|instalments|loan|
    // on loan|grant|stage|exam from|exam to|retakes|score|grade|ended|graduated|fee late|start told|excused|excused day.
    const string Course19 = "19|0|20|3|3|26|1|3900|0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|-1";        // studying since day 20
    const string Course34Exam = "34|0|10|8|8|30|4|3800|0|0|0|0|1|31|36|0|0|0|0|0|0|0|0|-1";   // exam window days 31 to 36
    const string Failed18 = "18|0|1|8|8|7|4|3600|0|0|0|0|3|8|13|1|30|4|13|0|0|0|0|-1";
    const string Passed18 = "18|0|15|8|8|21|4|3600|0|0|0|0|2|22|27|0|71|2|23|0|0|0|0|-1";
    const string Withdrawn19 = "19|0|20|0|0|19|0|0|0|0|0|0|4|0|0|0|0|0|20|0|0|0|0|-1";      // withdrawn the day it started
    const string Ordinary = "5|0|2|1|1|8|0|900|0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|-1";
    static readonly string Line = "v=1~h:7|1|30|0|0|0|1|0|0|-1|0~s:3|5000|5000|8~c:" + Course19 + ";" + Ordinary + ";" + Course34Exam
        + "~r:" + Failed18 + ";" + Passed18 + ";" + Withdrawn19 + ";2|0|1|5|5|6|2|800|0|0|0|0|2|10|12|0|60|3|12|0|0|0|0|-1~w:~q:~t:-1|0";
    const string Resaved = "v=1~h:7|1|50~s:3|5000|5000|8~c:" + Ordinary + "~r:2|0|1|5|5|6|2|800|0|0|0|0|2|10|12~w:~q:~t:-1|0";

    [Fact]
    public void KeepHoldsEveryRecordOfOurCoursesAndNothingElse()
    {
        string held = DiplomaHold.Keep(Line, null, 31)!;
        Assert.Equal(5, DiplomaHold.Count(held));
        Assert.Contains("31#" + Failed18, held);
        Assert.Contains("31#" + Passed18, held);
        Assert.Contains("31#" + Withdrawn19, held);
        Assert.DoesNotContain(Ordinary, held);
        Assert.DoesNotContain("~h:", held);
        Assert.True(DiplomaHold.HasOurs(Line));
        Assert.False(DiplomaHold.HasOurs(Resaved));
    }

    [Fact]
    public void NothingToHoldWithoutOurCourses()
    {
        Assert.Null(DiplomaHold.Keep("v=1~h:7|1|9~c:" + Ordinary + "~r:2|0|1", null, 9));
        Assert.Null(DiplomaHold.Keep(null, null, 9));
        Assert.Null(DiplomaHold.Keep("garbage", null, 9));
    }

    [Fact]
    public void KeepLeavesTheHoldWhenTheLineLostThemAndKeepsTheFirstDay()
    {
        string held = DiplomaHold.Keep(Line, null, 31)!;
        Assert.Equal(held, DiplomaHold.Keep(Resaved, held, 50));
        // RLO left its line alone for days (its study switched off): the records count as held since day 31.
        Assert.Equal(held, DiplomaHold.Keep(Line, held, 45));
        // A changed record is seen anew.
        string newer = "v=1~h:7|1|33~c:19|0|20|5|5|28|1|3900|0|0|0|0|0|0|0|0|0|0|0|0|0|0|0|-1~r:";
        Assert.Contains("33#19|0|20|5|5|28", DiplomaHold.Keep(newer, held, 33));
        Assert.Equal(held, DiplomaHold.Keep("v=2~h:1~c:" + Course19, held, 40));
    }

    [Fact]
    public void RestoreGivesBackEveryRecordTogetherAndMovesCoursesOnByWholeWeeks()
    {
        string held = DiplomaHold.Keep(Line, null, 31)!;
        var back = DiplomaHold.Restore(Resaved, held, 51, 4, 40)!;
        Assert.True(back.Done);
        Assert.Equal(5, back.Records);
        // Held 20 days: moved 21, three whole weeks, so the classes keep their weekdays.
        Assert.Contains("19|0|41|3|3|47|1|3900", back.Careers);
        Assert.Contains("34|0|10|8|8|30|4|3800|0|0|0|0|1|52|57|", back.Careers);
        Assert.Contains(Failed18, back.Careers);
        Assert.Contains(Passed18, back.Careers);
        Assert.Contains(Withdrawn19, back.Careers);
        Assert.Contains("~h:7|1|50~s:3|5000|5000|8~", back.Careers);
        Assert.EndsWith("~w:~q:~t:-1|0", back.Careers);
    }

    [Theory]
    [InlineData(1, 7)]
    [InlineData(7, 7)]
    [InlineData(8, 14)]
    [InlineData(0, 0)]
    public void APauseIsAlwaysWholeWeeksAndNeverShorterThanTheHold(int held, int moved)
    {
        string hold = DiplomaHold.Keep("v=1~h:1~c:" + Course19, null, 30)!;
        var back = DiplomaHold.Restore("v=1~h:1~c:~r:", hold, 30 + held, 4, 40)!;
        Assert.Contains("19|0|" + (20 + moved) + "|3|3|" + (26 + moved) + "|", back.Careers);
    }

    [Fact]
    public void NothingGoesBackUnlessEverythingFits()
    {
        string held = DiplomaHold.Keep(Line, null, 31)!;
        string full = "v=1~h:7|1|50~c:" + Ordinary + "~r:" + string.Join(";", Enumerable.Range(100, 39).Select(i => i + "|0|1|5|5|6|2|800|0|0|0|0|2|10|12|0|60|3|12|0|0|0|0|-1"));
        var waits = DiplomaHold.Restore(full, held, 51, 4, 40)!;
        Assert.False(waits.Done);
        Assert.Null(waits.Careers);
        Assert.Contains("40 results", waits.Waiting);
        var noRoom = DiplomaHold.Restore("v=1~h:1~c:" + Ordinary + ";6|0|2|0|0|8;7|0|2|0|0|8;8|0|2|0|0|8~r:", held, 51, 4, 40)!;
        Assert.False(noRoom.Done);
        Assert.Contains("4 courses", noRoom.Waiting);
        // A held course under way waits while the same course is under way again in RLO's line.
        var again = DiplomaHold.Restore("v=1~h:1~c:19|0|45|1|1|46~r:", DiplomaHold.Keep("v=1~h:1~c:" + Course19, null, 30)!, 51, 4, 40)!;
        Assert.False(again.Done);
        Assert.Contains("already under way", again.Waiting);
        Assert.Contains("30#" + Course19, again.Hold);
    }

    [Fact]
    public void ANewEnrolmentWhileTheHoldWaitsNeverDropsTheHeldRecords()
    {
        // A pass waits for room in a save with 40 results; the player enrols in another of our courses meanwhile.
        string held = DiplomaHold.Keep("v=1~h:1~c:~r:" + Passed18, null, 31)!;
        string forty = string.Join(";", Enumerable.Range(100, 40).Select(i => i + "|0|1|5|5|6|2|800|0|0|0|0|2|10|12|0|60|3|12|0|0|0|0|-1"));
        string withNew = "v=1~h:1~c:" + Course19 + "~r:" + forty;
        var waits = DiplomaHold.Restore(withNew, held, 51, 4, 40)!;
        Assert.False(waits.Done);
        Assert.Null(waits.Careers);
        Assert.Equal(1, DiplomaHold.Count(waits.Hold));
        Assert.Contains("31#" + Passed18, waits.Hold);
        // Back to an unreviewed RLO: the hold keeps the pass and adds the new course.
        string kept = DiplomaHold.Keep(withNew, waits.Hold, 60)!;
        Assert.Contains("31#" + Passed18, kept);
        Assert.Contains("60#" + Course19, kept);
        // Room again: the pass goes back, the new course is RLO's own and stays as it is.
        string room = "v=1~h:1~c:" + Course19 + "~r:" + string.Join(";", Enumerable.Range(100, 39).Select(i => i + "|0|1|5|5|6|2|800|0|0|0|0|2|10|12|0|60|3|12|0|0|0|0|-1"));
        var back = DiplomaHold.Restore(room, waits.Hold, 70, 4, 40)!;
        Assert.True(back.Done);
        Assert.Contains(Passed18, back.Careers);
        Assert.Contains("~c:" + Course19 + "~", back.Careers);
    }

    [Fact]
    public void IdenticalAttemptsAreMatchedOneForOne()
    {
        // Two identical withdrawals the same day, held; the line has one copy of it.
        string twice = DiplomaHold.Keep("v=1~h:1~c:~r:" + Withdrawn19 + ";" + Withdrawn19, null, 31)!;
        Assert.Equal(2, DiplomaHold.Count(twice));
        string oneInLine = "v=1~h:1~c:~r:" + Withdrawn19;
        Assert.Equal(2, DiplomaHold.Count(DiplomaHold.Keep(oneInLine, twice, 40)));
        var back = DiplomaHold.Restore(oneInLine, twice, 40, 4, 40)!;
        Assert.True(back.Done);
        Assert.Equal(2, back.Records);
        Assert.Equal(2, back.Careers!.Split(';').Count(r => r.EndsWith(Withdrawn19) || r.Contains(Withdrawn19)));
    }

    [Fact]
    public void RecordsRLONeverRewroteAreMovedOnInPlace()
    {
        string held = DiplomaHold.Keep(Line, null, 31)!;
        var back = DiplomaHold.Restore(Line, held, 45, 4, 40)!;
        Assert.True(back.Done);
        Assert.Equal(5, back.Records);
        Assert.Contains("19|0|34|3|3|40|", back.Careers);
        Assert.Contains("|1|45|50|", back.Careers);
        Assert.Contains(Passed18, back.Careers);
        Assert.Contains(Ordinary, back.Careers);
        Assert.Equal(Line.Split('~').Length, back.Careers!.Split('~').Length);
        // Same day: nothing to move, the hold is done.
        var sameDay = DiplomaHold.Restore(Line, held, 31, 4, 40)!;
        Assert.True(sameDay.Done);
        Assert.Null(sameDay.Careers);
    }

    [Fact]
    public void RestoreRefusesOtherVersionsAndMissingLines()
    {
        string held = DiplomaHold.Keep(Line, null, 31)!;
        Assert.Null(DiplomaHold.Restore("v=2~h:7|1|10", held, 40, 4, 40));
        Assert.Null(DiplomaHold.Restore(null, held, 40, 4, 40));
        var added = DiplomaHold.Restore("v=1~h:7|1|10", held, 40, 4, 40)!;
        Assert.True(added.Done);
        Assert.Contains("~c:19|", added.Careers);
    }

    [Fact]
    public void ReviewIsByExactHashPrefixAndUnreadableIsNotReviewed()
    {
        Assert.True(TargetGate.Reviewed("4F82B4F88F919178", Targets.RealLifeOverhaul.VerifiedHashes));
        Assert.True(TargetGate.Reviewed("2f360a365cf7915f", Targets.RealLifeOverhaul.VerifiedHashes));
        Assert.False(TargetGate.Reviewed("f1b93c176b33f79d", Targets.RealLifeOverhaul.VerifiedHashes));
        Assert.False(TargetGate.Reviewed("0000000000000000", Targets.RealLifeOverhaul.VerifiedHashes));
        Assert.False(TargetGate.Reviewed(null, Targets.RealLifeOverhaul.VerifiedHashes));
    }
}
