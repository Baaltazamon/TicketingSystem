namespace SupportPilot.Infrastructure.Services;

public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    public string RootPath { get; set; } = "storage/attachments";
}
