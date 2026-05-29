namespace SupportPilot.Infrastructure.Services;

public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    public string Provider { get; set; } = "Local";
    public string RootPath { get; set; } = "storage/attachments";
    public string Endpoint { get; set; } = "localhost:9000";
    public string AccessKey { get; set; } = "supportpilot";
    public string SecretKey { get; set; } = "supportpilot123";
    public string BucketName { get; set; } = "supportpilot-attachments";
    public bool UseSsl { get; set; }
}
