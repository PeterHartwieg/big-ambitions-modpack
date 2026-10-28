using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using HARTModPack.Core;
using HardModePatch.Core;
using Xunit;
using static HARTModPack.Core.CompanionSnapshot;

public class CompanionTests
{
    [Fact]
    public void Module_failure_isolated_and_capture_calls_each_reader_once()
    {
        int calls=0;
        var readers=new Dictionary<string,Func<Dictionary<string,object>>> {
            ["ok"]=()=>{ calls++; return Module("ready",new object[0]); },
            ["throw"]=()=>throw new InvalidOperationException(),
            ["bad"]=()=>Module("ready",double.NaN),
            ["off"]=()=>Module("disabled") };
        var value=Capture("1.1.1",Row("character","fixture","day",1,"hour",0,"minute",0),readers);
        var modules=JsonDocument.Parse(value).RootElement.GetProperty("modules");
        Assert.Equal(1,calls);
        Assert.Equal("ready",modules.GetProperty("ok").GetProperty("status").GetString());
        Assert.Equal(0,modules.GetProperty("ok").GetProperty("data").GetArrayLength());
        foreach(var key in new[]{"throw","bad"})Assert.Equal("failed",modules.GetProperty(key).GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null,modules.GetProperty("off").GetProperty("data").ValueKind);
    }
    [Fact]
    public void Serialization_is_order_independent_and_has_hard_limits()
    {
        Assert.Equal(Json(Row("b",2,"a",1)),Json(Row("a",1,"b",2)));
        Assert.Equal("quote\" slash\\ emoji😀",JsonDocument.Parse(Json("quote\" slash\\ emoji😀")).RootElement.GetString());
        Assert.Throws<ArgumentException>(()=>Json(9007199254740992L));
        Assert.Throws<ArgumentException>(()=>Json(new int[10001]));
        Assert.Throws<ArgumentException>(()=>Json(new string('x',1025)));
        Assert.Throws<ArgumentException>(()=>Module("disabled",Row()));
        object deep=0;for(int i=0;i<17;i++)deep=new[]{deep};
        Assert.Throws<ArgumentException>(()=>Json(deep));
    }
    [Fact]
    public void Publisher_only_replaces_or_removes_its_own_line()
    {
        var data=new Dictionary<string,string>{{"other","retain"},{Key,"stale"}};
        Publish(data,"fresh");Assert.Equal("fresh",data[Key]);Assert.Equal("retain",data["other"]);
        Publish(data,null);Assert.Single(data);Assert.Equal("retain",data["other"]);
        Publish(null,"fresh");
    }
    [Theory]
    [InlineData(null,"ready")]
    [InlineData("v=1","ready")]
    [InlineData("garbage","unavailable")]
    [InlineData("v=1~garbage","unavailable")]
    [InlineData("v=2~c:1#18|0|1|0|0|0|0|3600|1|0|0|0|0","unavailable")]
    [InlineData("v=1~c:invalid","unavailable")]
    [InlineData("v=1~c:1#999|0|1|0|0|0|0|3600|1|0|0|0|0","unavailable")]
    public void Held_format_distinguishes_empty_and_unknown(string? line,string status)
    {
        Assert.Equal(status,JsonDocument.Parse(Json(DiplomaHold.Inspect(line!))).RootElement.GetProperty("status").GetString());
    }
    [Fact]
    public void Held_records_are_projected_without_restoring_or_pausing()
    {
        const string line="v=1~c:9#18|0|1|0|0|0|0|3600|1|0|0|0|0~r:9#34|0|1|8|8|20|4|3800|1|0|0|0|2";
        string first=Json(DiplomaHold.Inspect(line));Assert.Equal(first,Json(DiplomaHold.Inspect(line)));
        var rows=JsonDocument.Parse(first).RootElement.GetProperty("data");
        Assert.Equal(2,rows.GetArrayLength());Assert.Equal(18,rows[0].GetProperty("courseId").GetInt32());
        Assert.Equal("enrollment",rows[0].GetProperty("kind").GetString());Assert.Equal(9,rows[1].GetProperty("heldSinceDay").GetInt32());
    }
}
