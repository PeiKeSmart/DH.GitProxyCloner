namespace DH.GitProxyCloner.Configuration;

public class GitProxyOptions
{
    public const string SectionName = "GitProxy";

    public bool EnableCaching { get; set; } = false;
    public int CacheExpirationMinutes { get; set; } = 30;
    public int MaxRequestSizeMB { get; set; } = 100;
    public int TimeoutMinutes { get; set; } = 10;
    public List<string> AllowedDomains { get; set; } = new() { "github.com" };
    public string UserAgent { get; set; } = "git/2.0.0 (GitProxyCloner/1.0)";
}
