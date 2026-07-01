using System.Net.NetworkInformation;

namespace NetShareTool;

public sealed class NetworkAdapterInfo
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required OperationalStatus Status { get; init; }
    public required NetworkInterfaceType Type { get; init; }

    public string DisplayName => $"{Name} ({Status})";
}
