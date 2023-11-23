using System.IO;

using WpfNativeAppHost.Hosting;

using Xunit;

namespace WpfNativeAppHost.Tests;

/// <summary>Covers reading, overriding and resolving the "which executable do we host?" settings.</summary>
public class HostedAppOptionsTests
{
    private static readonly string BaseDirectory = Path.GetFullPath(AppContext.BaseDirectory);

    [Fact]
    public void FromJson_ReadsTheHostedAppSection()
    {
        const string Json = """
            {
              "HostedApp": {
                "ExecutablePath": "C:\\apps\\guest.exe",
                "Arguments": "--flag",
                "StartupTimeoutSeconds": 12
              }
            }
            """;

        HostedAppOptions options = HostedAppOptions.FromJson(Json);

        Assert.Equal(@"C:\apps\guest.exe", options.ExecutablePath);
        Assert.Equal("--flag", options.Arguments);
        Assert.Equal(TimeSpan.FromSeconds(12), options.StartupTimeout);
    }

    [Fact]
    public void FromJson_IgnoresCommentsAndTrailingCommas()
    {
        // The shipped appsettings.json documents its alternatives in comments, so the reader has
        // to tolerate them.
        const string Json = """
            {
              // Which application to embed.
              "HostedApp": {
                "ExecutablePath": "C:\\apps\\guest.exe",
              }
            }
            """;

        HostedAppOptions options = HostedAppOptions.FromJson(Json);

        Assert.Equal(@"C:\apps\guest.exe", options.ExecutablePath);
    }

    [Fact]
    public void FromJson_FallsBackToDefaultsWhenTheSectionIsMissing()
    {
        HostedAppOptions options = HostedAppOptions.FromJson("{}");

        Assert.Equal(string.Empty, options.ExecutablePath);
        Assert.Equal(HostedAppOptions.DefaultStartupTimeoutSeconds, options.StartupTimeoutSeconds);
    }

    [Fact]
    public void Normalize_RejectsAnEmptyExecutablePath()
    {
        var options = new HostedAppOptions { ExecutablePath = "   " };

        ArgumentException error = Assert.Throws<ArgumentException>(() => options.Normalize(BaseDirectory));

        // The message has to tell the reader how to fix it, since this is the first thing they hit
        // if they clone the repository and delete appsettings.json.
        Assert.Contains("--host-exe", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Normalize_ResolvesRelativePathsAgainstTheBaseDirectory()
    {
        var options = new HostedAppOptions { ExecutablePath = @"tools\guest.exe" };

        HostedAppOptions normalized = options.Normalize(BaseDirectory);

        Assert.Equal(Path.Combine(BaseDirectory, "tools", "guest.exe"), normalized.ExecutablePath);
    }

    [Fact]
    public void Normalize_LeavesAbsolutePathsAlone()
    {
        var options = new HostedAppOptions { ExecutablePath = @"C:\apps\guest.exe" };

        HostedAppOptions normalized = options.Normalize(BaseDirectory);

        Assert.Equal(@"C:\apps\guest.exe", normalized.ExecutablePath);
    }

    [Fact]
    public void Normalize_ExpandsEnvironmentVariables()
    {
        // Lets appsettings.json say %SystemRoot% rather than hard-coding a drive letter.
        var options = new HostedAppOptions { ExecutablePath = @"%SystemRoot%\System32\charmap.exe" };

        HostedAppOptions normalized = options.Normalize(BaseDirectory);

        Assert.Equal(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "charmap.exe"),
            normalized.ExecutablePath);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Normalize_ReplacesANonPositiveTimeoutWithTheDefault(int configured)
    {
        // A zero timeout would make every launch fail instantly, which reads as "hosting is broken"
        // rather than "the configuration is wrong".
        var options = new HostedAppOptions
        {
            ExecutablePath = @"C:\apps\guest.exe",
            StartupTimeoutSeconds = configured,
        };

        HostedAppOptions normalized = options.Normalize(BaseDirectory);

        Assert.Equal(HostedAppOptions.DefaultStartupTimeoutSeconds, normalized.StartupTimeoutSeconds);
    }

    [Fact]
    public void ApplyCommandLineOverrides_TakesPrecedenceOverTheConfigurationFile()
    {
        var options = new HostedAppOptions
        {
            ExecutablePath = @"C:\apps\from-config.exe",
            StartupTimeoutSeconds = 60,
        };

        options.ApplyCommandLineOverrides(
            ["--host-exe", @"C:\apps\from-args.exe", "--host-args", "--verbose", "--startup-timeout", "5"]);

        Assert.Equal(@"C:\apps\from-args.exe", options.ExecutablePath);
        Assert.Equal("--verbose", options.Arguments);
        Assert.Equal(5, options.StartupTimeoutSeconds);
    }

    [Fact]
    public void ApplyCommandLineOverrides_IgnoresArgumentsItDoesNotRecognise()
    {
        var options = new HostedAppOptions { ExecutablePath = @"C:\apps\guest.exe" };

        options.ApplyCommandLineOverrides(["--unknown", "value", "--host-exe"]);

        // A trailing --host-exe has no value to consume, so nothing changes.
        Assert.Equal(@"C:\apps\guest.exe", options.ExecutablePath);
    }
}
