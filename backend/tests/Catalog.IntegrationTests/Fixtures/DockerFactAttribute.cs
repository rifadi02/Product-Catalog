namespace Catalog.IntegrationTests.Fixtures;

/// <summary>
/// A <see cref="FactAttribute"/> that skips instead of failing when there is no Docker daemon.
///
/// The alternative — a hard failure — means `dotnet test` is red on any machine without Docker,
/// which trains people to ignore a red test run. A skip is honest: the test did not run, and it
/// says so.
/// </summary>
public sealed class DockerFactAttribute : FactAttribute
{
    public DockerFactAttribute()
    {
        if (!DockerAvailability.IsAvailable)
            Skip = DockerAvailability.SkipReason;
    }
}

/// <inheritdoc cref="DockerFactAttribute"/>
public sealed class DockerTheoryAttribute : TheoryAttribute
{
    public DockerTheoryAttribute()
    {
        if (!DockerAvailability.IsAvailable)
            Skip = DockerAvailability.SkipReason;
    }
}

internal static class DockerAvailability
{
    public const string SkipReason =
        "Docker is not available on this machine; integration tests need a Postgres container.";

    private static readonly Lazy<bool> Probe = new(() =>
    {
        try
        {
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "info --format \"{{.ServerVersion}}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process is null) return false;

            return process.WaitForExit(10_000) && process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    });

    public static bool IsAvailable => Probe.Value;
}
