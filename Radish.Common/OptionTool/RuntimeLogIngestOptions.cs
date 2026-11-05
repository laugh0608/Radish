using System.Security.Cryptography;
using System.Text;
using Radish.Common.LogTool;

namespace Radish.Common.OptionTool;

/// <summary>候选接收端配置；生产传输预算仍由 L1 联验冻结。</summary>
public sealed class RuntimeLogIngestOptions
{
    public bool Enabled { get; set; }
    public string Mode { get; set; } = "Production";
    public int Port { get; set; }
    public string Credential { get; set; } = string.Empty;
    public string DeploymentId { get; set; } = string.Empty;
    public string[] Services { get; set; } = [];
    public int MaxConcurrentRequests { get; set; } = 2;
    public int RequestTimeoutSeconds { get; set; } = 30;
    public int DatabaseTimeoutSeconds { get; set; } = 5;
    public const int MaxEnvelopeBytes = 16 * 1024 * 1024;
    public const int MaxRecords = 32768;
    public const int BatchSize = 200;

    public void Validate()
    {
        if (!Enabled) return;
        if (Mode is not ("Development" or "Production") || Port is < 1024 or > 65535 || Credential.Length is < 32 or > 256 ||
            Credential.Any(c => c < 33 || c > 126) || Services.Length is < 1 or > 16 ||
            Services.Distinct(StringComparer.Ordinal).Count() != Services.Length ||
            MaxConcurrentRequests is < 1 or > 8 || RequestTimeoutSeconds is < 1 or > 120 ||
            DatabaseTimeoutSeconds is < 1 or > 30)
            throw new InvalidOperationException("Invalid runtime log ingestion configuration.");
        foreach (var service in Services)
            _ = new RuntimeLogPolicy(new RuntimeLogSource(DeploymentId, service, "ingest", "unversioned"));
    }

    public bool Authenticate(string credential) => credential.Length is >= 32 and <= 256 &&
        CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(Credential)),
            SHA256.HashData(Encoding.UTF8.GetBytes(credential)));
}
