using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace WpfNativeAppHost.TestTarget;

/// <summary>
/// A minimal windowed application for the integration tests to launch and adopt.
/// </summary>
/// <remarks>
/// <para>
/// The tests need a guest whose behaviour they control exactly. Windows' own built-in applications
/// are a poor substitute: on Windows 11 several of them - Notepad among them - are packaged apps
/// whose launcher hands off to a different process, which makes <c>Process.MainWindowHandle</c>
/// report nothing useful.
/// </para>
/// <para>Command line:</para>
/// <list type="bullet">
///   <item><c>--delay &lt;ms&gt;</c> wait this long before showing the window, to exercise the
///   caller's polling; without it the window is up almost immediately.</item>
///   <item><c>--headless</c> never show a window at all, to exercise the caller's timeout.</item>
///   <item><c>--exit-now</c> quit straight away, standing in for a guest that fails on startup.</item>
///   <item><c>--splash &lt;ms&gt;</c> show a tool-window "splash" for this long first, the way
///   FreeCAD does, before the real window replaces it.</item>
/// </list>
/// </remarks>
internal static class Program
{
    /// <summary>Window caption, so a test can confirm it adopted the right window.</summary>
    internal const string WindowTitle = "WpfNativeAppHost test target";

    /// <summary>Caption of the fake splash screen, which no test should ever end up adopting.</summary>
    internal const string SplashTitle = "WpfNativeAppHost splash";

    /// <summary>How long a headless instance lingers before giving up, so strays cannot pile up.</summary>
    private static readonly TimeSpan HeadlessLifetime = TimeSpan.FromMinutes(2);

    /// <summary>Reported by <c>--exit-now</c>, so a test can tell a deliberate failure from a crash.</summary>
    private const int FailedStartExitCode = 3;

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Contains("--exit-now", StringComparer.OrdinalIgnoreCase))
        {
            return FailedStartExitCode;
        }

        if (args.Contains("--headless", StringComparer.OrdinalIgnoreCase))
        {
            Thread.Sleep(HeadlessLifetime);
            return 0;
        }

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        Window window = CreateWindow();
        window.Closed += (_, _) => app.Shutdown();

        TimeSpan splash = ParseDuration(args, "--splash");
        Window? splashWindow = null;
        if (splash > TimeSpan.Zero)
        {
            splashWindow = CreateSplashWindow();
            splashWindow.Show();
        }

        TimeSpan delay = ParseDuration(args, "--delay");
        TimeSpan showAfter = delay > splash ? delay : splash;

        if (showAfter > TimeSpan.Zero)
        {
            var timer = new DispatcherTimer { Interval = showAfter };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                splashWindow?.Close();
                window.Show();
            };
            timer.Start();
        }
        else
        {
            window.Show();
        }

        return app.Run();
    }

    private static Window CreateWindow() => new()
    {
        Title = WindowTitle,
        Width = 640,
        Height = 480,
        Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)),
        Content = new TextBlock
        {
            Text = WindowTitle,
            Foreground = Brushes.Gainsboro,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        },
    };

    /// <summary>
    /// A stand-in for FreeCAD's splash screen, matching the two properties that make the real one
    /// awkward: it is frameless with the <c>WS_EX_TOOLWINDOW</c> extended style, and it has no
    /// owner window. Without the second of those, the host would reject it for the wrong reason and
    /// the test would prove nothing.
    /// </summary>
    /// <remarks>
    /// <c>ShowInTaskbar</c> is deliberately left alone: setting it to false makes WPF invent a
    /// hidden owner window, which is exactly what a real splash screen does not have.
    /// </remarks>
    private static Window CreateSplashWindow()
    {
        var splash = new Window
        {
            Title = SplashTitle,
            Width = 400,
            Height = 200,
            WindowStyle = WindowStyle.None,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Background = new SolidColorBrush(Color.FromRgb(0x40, 0x40, 0x40)),
            Content = new TextBlock
            {
                Text = SplashTitle,
                Foreground = Brushes.Gainsboro,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };

        // WPF has no property for WS_EX_TOOLWINDOW that does not also add an owner, so set it by hand.
        splash.SourceInitialized += (_, _) =>
        {
            IntPtr handle = new WindowInteropHelper(splash).Handle;
            int extendedStyle = GetWindowLong(handle, GwlExStyle);
            SetWindowLong(handle, GwlExStyle, extendedStyle | WsExToolWindow);
        };

        return splash;
    }

    private const int GwlExStyle = -20;
    private const int WsExToolWindow = 0x0000_0080;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr window, int index, int value);

    private static TimeSpan ParseDuration(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(args[i + 1], out int milliseconds) &&
                milliseconds > 0)
            {
                return TimeSpan.FromMilliseconds(milliseconds);
            }
        }

        return TimeSpan.Zero;
    }
}
