using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

using WpfNativeAppHost.Hosting;

using Xunit;

namespace WpfNativeAppHost.Tests;

/// <summary>
/// Exercises the guest lifecycle against a real process. These launch actual windows, so they need
/// an interactive desktop session and are excluded from CI with
/// <c>dotnet test --filter Category!=Integration</c>.
/// </summary>
[Trait("Category", "Integration")]
public class HostedProcessTests
{
    /// <summary>Long enough for a cold .NET start, short enough that a hung test is obvious.</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    [Fact]
    public void WaitForMainWindow_ReturnsTheGuestsWindow()
    {
        using HostedProcess process = HostedProcess.Start(TestTarget());

        IntPtr window = process.WaitForMainWindow(Timeout);

        Assert.NotEqual(IntPtr.Zero, window);
    }

    [Fact]
    public void WaitForMainWindow_KeepsPollingUntilTheWindowAppears()
    {
        // Guests rarely have a window ready the moment they start - FreeCAD shows a splash screen
        // first - so a single check is never enough. This one deliberately waits before showing.
        using HostedProcess process = HostedProcess.Start(TestTarget("--delay 1500"));

        var elapsed = Stopwatch.StartNew();
        IntPtr window = process.WaitForMainWindow(Timeout);
        elapsed.Stop();

        Assert.NotEqual(IntPtr.Zero, window);
        Assert.True(elapsed.Elapsed < Timeout, $"Found the window only after {elapsed.Elapsed}.");
    }

    [Fact]
    public void WaitForMainWindow_SkipsTheSplashScreenAndWaitsForTheRealWindow()
    {
        // This is the FreeCAD case, reproduced deliberately: a tool-window splash appears first and
        // the real window only follows seconds later. Process.MainWindowHandle returns the splash,
        // which is then adopted and destroyed the moment it closes - leaving an empty pane.
        using HostedProcess process = HostedProcess.Start(TestTarget("--splash 2000"));

        IntPtr window = process.WaitForMainWindow(Timeout);

        Assert.Equal("WpfNativeAppHost test target", TitleOf(window));
    }

    [Fact]
    public void WaitForMainWindow_TimesOutForAGuestThatNeverOpensAWindow()
    {
        using HostedProcess process = HostedProcess.Start(TestTarget("--headless"));
        var budget = TimeSpan.FromSeconds(2);

        var elapsed = Stopwatch.StartNew();
        TimeoutException error = Assert.Throws<TimeoutException>(() => process.WaitForMainWindow(budget));
        elapsed.Stop();

        // The whole wait must fit in the caller's budget, not be spent once per internal phase.
        Assert.True(elapsed.Elapsed < budget * 2, $"Waiting overran the budget: {elapsed.Elapsed}.");
        Assert.Contains("did not open a top-level window", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WaitForMainWindow_FailsFastWhenTheGuestDiesDuringStartup()
    {
        // A guest that cannot start is the case where the original code hung the UI thread for
        // good: nothing checked whether the process was still alive, so the wait never ended.
        using HostedProcess process = HostedProcess.Start(TestTarget("--exit-now"));

        var elapsed = Stopwatch.StartNew();
        InvalidOperationException error =
            Assert.Throws<InvalidOperationException>(() => process.WaitForMainWindow(Timeout));
        elapsed.Stop();

        Assert.Contains("before opening a window", error.Message, StringComparison.Ordinal);
        Assert.True(elapsed.Elapsed < Timeout, $"Took {elapsed.Elapsed} to notice the guest had died.");
    }

    [Fact]
    public void Start_ThrowsImmediatelyWhenTheExecutableDoesNotExist()
    {
        var options = new HostedAppOptions
        {
            ExecutablePath = Path.Combine(AppContext.BaseDirectory, "no-such-application.exe"),
        };

        FileNotFoundException error = Assert.Throws<FileNotFoundException>(() => HostedProcess.Start(options));

        Assert.Contains("appsettings.json", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Dispose_ShutsTheGuestDown()
    {
        HostedProcess process = HostedProcess.Start(TestTarget());
        process.WaitForMainWindow(Timeout);

        process.Dispose();

        Assert.True(process.HasExited);
    }

    [Fact]
    public void Dispose_CanBeCalledMoreThanOnce()
    {
        // NativeAppHost disposes the guest from both DestroyWindowCore and Dispose.
        HostedProcess process = HostedProcess.Start(TestTarget());
        process.WaitForMainWindow(Timeout);

        process.Dispose();
        process.Dispose();

        Assert.True(process.HasExited);
    }

    private static string TitleOf(IntPtr window)
    {
        var text = new StringBuilder(256);
        GetWindowText(window, text, text.Capacity);
        return text.ToString();
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetWindowText(IntPtr window, StringBuilder text, int maxCount);

    /// <summary>
    /// Options pointing at the purpose-built guest that the build copies next to these tests.
    /// </summary>
    private static HostedAppOptions TestTarget(string arguments = "")
    {
        string path = Path.Combine(AppContext.BaseDirectory, "TestTarget", "WpfNativeAppHost.TestTarget.exe");

        Assert.True(
            File.Exists(path),
            $"The test target was not copied to '{path}'. Build the solution rather than this project alone.");

        return new HostedAppOptions { ExecutablePath = path, Arguments = arguments };
    }
}
