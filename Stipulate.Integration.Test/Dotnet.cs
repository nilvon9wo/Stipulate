using System.Diagnostics;
using System.Reflection;

namespace Net.NowhereAtAll.Stipulate.Integration.Test;

/// <summary>Runs the <c>dotnet</c> that runs the tests, and knows where and how they were built.</summary>
internal static class Dotnet
{
    private const string DotnetHostPath = "DOTNET_HOST_PATH";
    private const string DefaultDotnet = "dotnet";

    public static string RepositoryRoot { get; } = Metadata("RepositoryRoot");

    /// <summary>The configuration the tests, and so the tools they use, were built in.</summary>
    public static string Configuration { get; } = Metadata("Configuration");

    /// <summary>The version this build packs Stipulate at: <c>0.0.0-dev</c>, or a release's.</summary>
    public static string PackageVersion { get; } = Metadata("PackageVersion");

    public static ProcessResult Run(string workingDirectory, IEnumerable<string> arguments)
    {
        ProcessStartInfo start = new(Environment.GetEnvironmentVariable(DotnetHostPath) ?? DefaultDotnet, arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using Process process = Process.Start(start)!;
        Task<string> error = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return new ProcessResult(process.ExitCode, output + error.Result);
    }

    private static string Metadata(string key)
        => typeof(Dotnet).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == key)
            .Value!;
}
