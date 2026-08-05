namespace DocGenerator.Application.Common;

public class JwtOptions
{
    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "DocGenerator";
    public string Audience { get; set; } = "DocGeneratorClients";
    public int ExpiryMinutes { get; set; } = 480;
}
