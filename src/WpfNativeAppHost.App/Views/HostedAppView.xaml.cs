using System.Windows;
using System.Windows.Controls;

using WpfNativeAppHost.Hosting;

namespace WpfNativeAppHost.Views;

/// <summary>
/// The pane that shows the embedded application. Starting the guest and waiting for its window can
/// take seconds, so that work happens on a background thread once the pane has loaded rather than
/// in the constructor, which would freeze the shell before it ever appeared.
/// </summary>
public partial class HostedAppView : UserControl
{
    private NativeAppHost? _host;

    public HostedAppView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        var app = (App)Application.Current;
        if (app.HostedApp is not { } options)
        {
            ShowStatus(app.ConfigurationError ?? "No hosted application has been configured.");
            return;
        }

        // Tear down while the WPF window still exists. Once it has closed, its HWND is gone and the
        // guest's window would go with it before we ever got the chance to hand it back. (A shell
        // that can cancel closing would need to defer this until the close is known to go ahead.)
        if (Window.GetWindow(this) is { } window)
        {
            window.Closing += (_, _) => DisposeHost();
        }

        // Backstop for shutdown paths that never raise Closing, so a hosted CAD application can
        // never be left running with no window and no way to reach it.
        Dispatcher.ShutdownStarted += (_, _) => DisposeHost();

        ShowStatus($"Starting {options.ExecutablePath}...");

        try
        {
            (HostedProcess process, IntPtr windowHandle) = await Task.Run(() => StartAndWait(options));

            _host = new NativeAppHost(process, windowHandle);
            HostContainer.Content = _host;
            StatusText.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            ShowStatus($"Could not host {options.ExecutablePath}\n\n{ex.Message}");
        }
    }

    private static (HostedProcess Process, IntPtr WindowHandle) StartAndWait(HostedAppOptions options)
    {
        HostedProcess process = HostedProcess.Start(options);
        try
        {
            return (process, process.WaitForMainWindow(options.StartupTimeout));
        }
        catch
        {
            // Never leave the guest running if we cannot adopt its window.
            process.Dispose();
            throw;
        }
    }

    private void DisposeHost()
    {
        _host?.Dispose();
        _host = null;
    }

    private void ShowStatus(string message)
    {
        StatusText.Text = message;
        StatusText.Visibility = Visibility.Visible;
    }
}
