namespace iM1os.Application.Authentication;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "iM1os";

    public string Audience { get; set; } = "iM1os";

    public string SigningKey { get; set; } = "development-only-key-change-before-deploying-iM1os";

    public int ExpirationMinutes { get; set; } = 60;
}
