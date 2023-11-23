using WpfNativeAppHost.Hosting;
using WpfNativeAppHost.Interop;

using Xunit;

namespace WpfNativeAppHost.Tests;

/// <summary>
/// Covers the style rewriting that turns a borrowed top-level window into an embedded child.
/// Getting these bits wrong is what produces the classic symptoms - a guest that paints over the
/// whole shell, flickers on resize, or refuses to appear at all.
/// </summary>
public class ChildWindowStyleTests
{
    /// <summary>A plausible starting point: what an ordinary application window looks like.</summary>
    private const WindowStyles TypicalTopLevelWindow =
        WindowStyles.OverlappedWindow | WindowStyles.Visible | WindowStyles.ClipSiblings;

    // Theory data is passed as uint rather than WindowStyles: xUnit only discovers public test
    // methods, and a public method cannot take an internal type as a parameter.

    [Theory]
    [InlineData((uint)WindowStyles.Child)]
    [InlineData((uint)WindowStyles.Visible)]
    [InlineData((uint)WindowStyles.ClipChildren)]
    [InlineData((uint)WindowStyles.ClipSiblings)]
    public void ForEmbedding_SetsStylesAnEmbeddedWindowNeeds(uint requiredBits)
    {
        var required = (WindowStyles)requiredBits;

        WindowStyles result = ChildWindowStyle.ForEmbedding(TypicalTopLevelWindow);

        Assert.Equal(required, result & required);
    }

    [Theory]
    [InlineData((uint)WindowStyles.Popup)]
    [InlineData((uint)WindowStyles.Caption)]
    [InlineData((uint)WindowStyles.ThickFrame)]
    [InlineData((uint)WindowStyles.SysMenu)]
    [InlineData((uint)WindowStyles.MinimizeBox)]
    [InlineData((uint)WindowStyles.MaximizeBox)]
    [InlineData((uint)WindowStyles.Maximize)]
    [InlineData((uint)WindowStyles.Minimize)]
    public void ForEmbedding_ClearsStylesThatOnlyMakeSenseForATopLevelWindow(uint unwantedBits)
    {
        var unwanted = (WindowStyles)unwantedBits;

        // Start from a window that has every one of these set, so nothing passes by accident.
        WindowStyles everything = TypicalTopLevelWindow | WindowStyles.Popup |
                                  WindowStyles.Maximize | WindowStyles.Minimize;

        WindowStyles result = ChildWindowStyle.ForEmbedding(everything);

        Assert.Equal(default, result & unwanted);
    }

    [Fact]
    public void ForEmbedding_LeavesUnrelatedStylesAlone()
    {
        // Scroll bars say nothing about whether a window is top-level, so the guest keeps them.
        WindowStyles withScrollBars = TypicalTopLevelWindow | WindowStyles.VScroll | WindowStyles.HScroll;

        WindowStyles result = ChildWindowStyle.ForEmbedding(withScrollBars);

        Assert.Equal(WindowStyles.VScroll | WindowStyles.HScroll,
                     result & (WindowStyles.VScroll | WindowStyles.HScroll));
    }

    [Fact]
    public void ForEmbedding_NeverProducesBothChildAndPopup()
    {
        // Windows rejects the combination outright, so a guest that was a pop-up must lose that bit.
        WindowStyles result = ChildWindowStyle.ForEmbedding(WindowStyles.Popup | WindowStyles.Visible);

        Assert.Equal(WindowStyles.Child, result & WindowStyles.Child);
        Assert.Equal(default, result & WindowStyles.Popup);
    }

    [Fact]
    public void ForEmbedding_IsIdempotent()
    {
        // BuildWindowCore can run more than once for the same guest if the host is re-created.
        WindowStyles once = ChildWindowStyle.ForEmbedding(TypicalTopLevelWindow);
        WindowStyles twice = ChildWindowStyle.ForEmbedding(once);

        Assert.Equal(once, twice);
    }
}
