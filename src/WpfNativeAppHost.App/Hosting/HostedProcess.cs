using System.Diagnostics;
using System.IO;

namespace WpfNativeAppHost.Hosting;

/// <summary>
/// Launches the application to be embedded and waits for its top-level window to appear.
/// </summary>
/// <remarks>
/// Deliberately free of WPF dependencies: everything that can actually go wrong when hosting a
/// foreign application — the executable is missing, it dies on startup, it never opens a window —
/// happens here, where it can be tested without a message loop.
/// </remarks>
internal sealed class HostedProcess : IDisposable
{
    /// <summary>How long to wait for a polite shutdown before killing the process.</summary>
    private static readonly TimeSpan ShutdownGracePeriod = TimeSpan.FromSeconds(5);

    /// <summary>Gap between window-handle polls. Short enough to feel instant, long enough not to spin.</summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);

    private readonly Process _process;
    private bool _disposed;

    private HostedProcess(Process process) => _process = process;

    /// <summary>True once the hosted application has exited, for whatever reason.</summary>
    public bool HasExited => _disposed || _process.HasExited;

    /// <summary>Path of the executable being hosted, for error messages and logging.</summary>
    public string ExecutablePath { get; private init; } = string.Empty;

    /// <summary>Starts the configured application.</summary>
    /// <exception cref="FileNotFoundException">The executable does not exist.</exception>
    /// <exception cref="InvalidOperationException">Windows refused to start the process.</exception>
    public static HostedProcess Start(HostedAppOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!File.Exists(options.ExecutablePath))
        {
            throw new FileNotFoundException(
                $"Cannot host '{options.ExecutablePath}' because no such file exists. " +
                "Check 'HostedApp:ExecutablePath' in appsettings.json.",
                options.ExecutablePath);
        }

        var startInfo = new ProcessStartInfo(options.ExecutablePath)
        {
            Arguments = options.Arguments,
            // Take the process handle under our own control rather than handing off to the shell,
            // and start it in its own folder: Qt applications such as FreeCAD resolve plugins and
            // resources relative to the executable's directory.
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(options.ExecutablePath) ?? string.Empty,
        };

        Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException(
                $"Windows did not return a process handle for '{options.ExecutablePath}'.");

        return new HostedProcess(process) { ExecutablePath = options.ExecutablePath };
    }

    /// <summary>
    /// Blocks until the hosted application has opened its main window, and returns that window's
    /// handle. Splash screens and other transient windows are skipped.
    /// </summary>
    /// <exception cref="TimeoutException">No window appeared within <paramref name="timeout"/>.</exception>
    /// <exception cref="InvalidOperationException">The process exited before showing a window.</exception>
    public IntPtr WaitForMainWindow(TimeSpan timeout)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var elapsed = Stopwatch.StartNew();
        while (elapsed.Elapsed < timeout)
        {
            if (_process.HasExited)
            {
                throw new InvalidOperationException(
                    $"'{ExecutablePath}' exited with code {_process.ExitCode} before opening a window.");
            }

            // Deliberately not Process.MainWindowHandle: during startup that often returns a splash
            // screen. See MainWindowFinder for why, and for what it looks for instead.
            IntPtr handle = MainWindowFinder.Find(_process.Id);
            if (handle != IntPtr.Zero)
            {
                return handle;
            }

            Thread.Sleep(PollInterval);
        }

        throw new TimeoutException(
            $"'{ExecutablePath}' did not open a top-level window within {timeout.TotalSeconds:0.#}s. " +
            "Console applications and background services cannot be hosted; for a slow-starting " +
            "application, raise 'HostedApp:StartupTimeoutSeconds'.");
    }

    /// <summary>
    /// Asks the hosted application to close, then kills it if it will not. Safe to call more than once.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            if (!_process.HasExited)
            {
                // CloseMainWindow reports false when there is no window to send WM_CLOSE to, in
                // which case waiting out the grace period would achieve nothing.
                bool closeRequested = _process.CloseMainWindow();
                if (!closeRequested || !_process.WaitForExit((int)ShutdownGracePeriod.TotalMilliseconds))
                {
                    _process.Kill(entireProcessTree: true);
                }
            }
        }
        catch (InvalidOperationException)
        {
            // The process was never started, or exited between the check and the call.
        }
        finally
        {
            _process.Dispose();
        }
    }
}
