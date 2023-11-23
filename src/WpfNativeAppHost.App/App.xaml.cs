using System.Windows;

using WpfNativeAppHost.Hosting;

namespace WpfNativeAppHost;

/// <summary>
/// Application entry point. Its only job is to work out which executable to host; starting that
/// executable is left to <see cref="Views.HostedAppView"/>, which can do it without freezing the UI.
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// The resolved configuration, or <see langword="null"/> if it could not be read - in which case
    /// <see cref="ConfigurationError"/> explains why.
    /// </summary>
    internal HostedAppOptions? HostedApp { get; private set; }

    /// <summary>Why configuration failed, ready to be shown to the user.</summary>
    internal string? ConfigurationError { get; private set; }

    /// <inheritdoc/>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            HostedApp = HostedAppOptions.Load(AppContext.BaseDirectory, e.Args);
        }
        catch (Exception ex)
        {
            // Surfaced in the hosting pane rather than thrown, so the shell still starts and the
            // user can see what needs fixing.
            ConfigurationError = ex.Message;
        }
    }
}
