namespace SupportPilot.Infrastructure.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "SupportPilot";
    public string Audience { get; set; } = "SupportPilot.Client";
    public string SigningKey { get; set; } = "CHANGE_ME_TO_A_LONG_RANDOM_SECRET_32_CHARS";
    public int ExpirationMinutes { get; set; } = 480;
    public string SeedAdminEmail { get; set; } = "admin@supportpilot.local";
    public string SeedAdminPassword { get; set; } = "Admin123!";
}
