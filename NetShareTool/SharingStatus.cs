namespace NetShareTool;

public sealed class SharingStatus
{
    public required string Name { get; init; }
    public required bool SharingEnabled { get; init; }
    public required string SharingConnectionType { get; init; }
}
