using Microsoft.Extensions.Configuration;

namespace iM1os.Infrastructure.Configuration;

public static class DotEnvConfigurationExtensions
{
    public static IConfigurationBuilder AddLocalDotEnvFile(this IConfigurationBuilder configuration, string fileName = ".env")
    {
        var path = FindFileInCurrentOrParentDirectories(fileName);
        if (path is null)
        {
            return configuration;
        }

        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var separator = line.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var key = line[..separator].Trim().Replace("__", ":", StringComparison.Ordinal);
            var value = line[(separator + 1)..].Trim().Trim('"');
            values[key] = value;
        }

        return configuration.AddInMemoryCollection(values);
    }

    private static string? FindFileInCurrentOrParentDirectories(string fileName)
    {
        foreach (var startPath in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory }.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var directory = new DirectoryInfo(startPath);
            while (directory is not null)
            {
                var candidate = Path.Combine(directory.FullName, fileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }
        }

        return null;
    }
}
