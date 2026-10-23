using HARTModPack.Core;
using Xunit;

public class ButtonCaptionTests
{
    [Theory]
    [InlineData("hardmode_series_apply", "hartmodpack_button_apply")]
    [InlineData("hartmodpack_check_again", "hartmodpack_check_again")]
    [InlineData("another_mod_button", null)]
    [InlineData("Apply HART series settings", null)]
    [InlineData(null, null)]
    public void Only_the_two_pack_labels_select_a_caption(string label, string key)
    {
        Assert.Equal(key, ButtonCaptions.KeyFor(label));
    }
}
