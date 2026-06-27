namespace iM1os.Infrastructure.Configuration;

public sealed class PlatformOptions
{
    public const string SectionName = "Platform";

    public string Name { get; set; } = "iM1 OS";

    public string Domain { get; set; } = "im1os.com";

    public string DefaultOrganizationSlug { get; set; } = "im1os";
}
