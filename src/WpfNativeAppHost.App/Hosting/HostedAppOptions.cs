using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WpfNativeAppHost.Hosting;

/// <summary>
/// Which executable to launch and adopt, where it came from, and how long to wait for its window.
/// </summary>
/// <remarks>
/// Values are read from <c>appsettings.json</c> and may be overridden on the command line. Nothing
/// here touches WPF, so the whole type is straightforward to unit test.
/// </remarks>
internal sealed class HostedAppOptions
{
    /// <summary>Object name under which these options live in <c>appsettings.json</c>.</summary>
    internal const string SectionName = "HostedApp";

    /// <summary>Used when the configuration file omits a timeout or gives a non-positive one.</summary>
    internal const int DefaultStartupTimeoutSeconds = 60;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        // The shipped appsettings.json carries commented-out examples for FreeCAD and friends.
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// Path to the executable to host. May be relative, and may contain environment variables such
    /// as <c>%ProgramFiles%</c>; <see cref="Normalize"/> resolves both.
    /// </summary>
    public string ExecutablePath { get; set; } = string.Empty;

    /// <summary>Command-line arguments handed to the hosted executable. Optional.</summary>
    public string Arguments { get; set; } = string.Empty;

    /// <summary>
    /// How long to wait for the hosted application to put a top-level window on screen. Generous
    /// by default: a cold FreeCAD start can take a while.
    /// </summary>
    public int StartupTimeoutSeconds { get; set; } = DefaultStartupTimeoutSeconds;

    /// <summary><see cref="StartupTimeoutSeconds"/> as a <see cref="TimeSpan"/>.</summary>
    [JsonIgnore]
    public TimeSpan StartupTimeout => TimeSpan.FromSeconds(StartupTimeoutSeconds);

    /// <summary>
    /// Reads <c>appsettings.json</c> (if present), applies any command-line overrides, and resolves
    /// the result against <paramref name="baseDirectory"/>.
    /// </summary>
    /// <exception cref="ArgumentException">No usable executable path was configured.</exception>
    public static HostedAppOptions Load(string baseDirectory, IReadOnlyList<string> commandLineArgs)
    {
        string configPath = Path.Combine(baseDirectory, "appsettings.json");
        HostedAppOptions options = File.Exists(configPath)
            ? FromJson(File.ReadAllText(configPath))
            : new HostedAppOptions();

        options.ApplyCommandLineOverrides(commandLineArgs);
        return options.Normalize(baseDirectory);
    }

    /// <summary>Parses the <c>HostedApp</c> section out of an <c>appsettings.json</c> document.</summary>
    /// <exception cref="JsonException">The document is not valid JSON.</exception>
    public static HostedAppOptions FromJson(string json)
    {
        ConfigurationFile? file = JsonSerializer.Deserialize<ConfigurationFile>(json, JsonOptions);
        return file?.HostedApp ?? new HostedAppOptions();
    }

    /// <summary>
    /// Applies <c>--host-exe</c>, <c>--host-args</c> and <c>--startup-timeout</c>, so a target can
    /// be tried without editing the configuration file. Unrecognised arguments are ignored.
    /// </summary>
    public void ApplyCommandLineOverrides(IReadOnlyList<string> args)
    {
        for (int i = 0; i < args.Count - 1; i++)
        {
            string value = args[i + 1];
            switch (args[i].ToLowerInvariant())
            {
                case "--host-exe":
                    ExecutablePath = value;
                    break;
                case "--host-args":
                    Arguments = value;
                    break;
                case "--startup-timeout" when int.TryParse(value, out int seconds):
                    StartupTimeoutSeconds = seconds;
                    break;
            }
        }
    }

    /// <summary>
    /// Returns a copy with the executable path expanded and made absolute, and with a sane timeout.
    /// </summary>
    /// <param name="baseDirectory">Directory that relative executable paths are resolved against.</param>
    /// <exception cref="ArgumentException"><see cref="ExecutablePath"/> is empty.</exception>
    public HostedAppOptions Normalize(string baseDirectory)
    {
        string path = Environment.ExpandEnvironmentVariables(ExecutablePath.Trim());
        if (path.Length == 0)
        {
            throw new ArgumentException(
                $"No executable to host. Set '{SectionName}:{nameof(ExecutablePath)}' in appsettings.json, " +
                "or pass --host-exe <path> on the command line.",
                nameof(ExecutablePath));
        }

        return new HostedAppOptions
        {
            ExecutablePath = Path.GetFullPath(path, baseDirectory),
            Arguments = Arguments.Trim(),
            StartupTimeoutSeconds = StartupTimeoutSeconds > 0
                ? StartupTimeoutSeconds
                : DefaultStartupTimeoutSeconds,
        };
    }

    /// <summary>Shape of the <c>appsettings.json</c> document, so the section name stays in one place.</summary>
    private sealed class ConfigurationFile
    {
        public HostedAppOptions? HostedApp { get; set; }
    }
}
