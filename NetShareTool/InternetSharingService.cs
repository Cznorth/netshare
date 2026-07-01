using System.Collections;
using System.Net.NetworkInformation;

namespace NetShareTool;

public sealed class InternetSharingService
{
    private const int PublicConnection = 0;
    private const int PrivateConnection = 1;

    public IReadOnlyList<NetworkAdapterInfo> GetAdapters()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(adapter => adapter.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .OrderByDescending(adapter => adapter.OperationalStatus == OperationalStatus.Up)
            .ThenBy(adapter => adapter.Name)
            .Select(adapter => new NetworkAdapterInfo
            {
                Name = adapter.Name,
                Description = adapter.Description,
                Status = adapter.OperationalStatus,
                Type = adapter.NetworkInterfaceType
            })
            .ToList();
    }

    public IReadOnlyList<SharingStatus> GetSharingStatus()
    {
        dynamic netShare = CreateNetShare();
        var rows = new List<SharingStatus>();

        foreach (var connection in EnumerateConnections(netShare))
        {
            dynamic props = netShare.NetConnectionProps(connection);
            dynamic config = netShare.INetSharingConfigurationForINetConnection(connection);
            rows.Add(new SharingStatus
            {
                Name = props.Name,
                SharingEnabled = config.SharingEnabled,
                SharingConnectionType = config.SharingEnabled
                    ? ConvertSharingType(config.SharingConnectionType)
                    : "未共享"
            });
        }

        return rows.OrderBy(row => row.Name).ToList();
    }

    public void EnableSharing(string publicName, string privateName)
    {
        EnableSharing(publicName, null, privateName, null);
    }

    public void EnableSharing(string publicName, string? publicDescription, string privateName, string? privateDescription)
    {
        if (string.Equals(publicName, privateName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("来源网卡和共享出口不能相同。");
        }

        dynamic netShare = CreateNetShare();
        DisableAllSharing(netShare);

        var publicConnection = FindConnection(netShare, publicName, publicDescription)
            ?? throw new InvalidOperationException($"未找到来源网卡：{publicName}");
        var privateConnection = FindConnection(netShare, privateName, privateDescription)
            ?? throw new InvalidOperationException($"未找到共享出口网卡：{privateName}");

        dynamic publicConfig = netShare.INetSharingConfigurationForINetConnection(publicConnection);
        dynamic privateConfig = netShare.INetSharingConfigurationForINetConnection(privateConnection);

        publicConfig.EnableSharing(PublicConnection);
        privateConfig.EnableSharing(PrivateConnection);
    }

    public void DisableSharing(string? publicName = null, string? privateName = null)
    {
        dynamic netShare = CreateNetShare();

        if (string.IsNullOrWhiteSpace(publicName) && string.IsNullOrWhiteSpace(privateName))
        {
            DisableAllSharing(netShare);
            return;
        }

        foreach (var name in new[] { publicName, privateName }.Where(name => !string.IsNullOrWhiteSpace(name)))
        {
            var connection = FindConnection(netShare, name!, null);
            if (connection is null)
            {
                continue;
            }

            dynamic config = netShare.INetSharingConfigurationForINetConnection(connection);
            if (config.SharingEnabled)
            {
                config.DisableSharing();
            }
        }
    }

    private static object CreateNetShare()
    {
        var type = Type.GetTypeFromProgID("HNetCfg.HNetShare")
            ?? throw new InvalidOperationException("无法创建 HNetCfg.HNetShare。请确认 Windows ICS 服务可用。");

        return Activator.CreateInstance(type)
            ?? throw new InvalidOperationException("无法初始化 Windows 网络共享组件。");
    }

    private static IEnumerable<object> EnumerateConnections(dynamic netShare)
    {
        foreach (object connection in (IEnumerable)netShare.EnumEveryConnection)
        {
            yield return connection;
        }
    }

    private static object? FindConnection(dynamic netShare, string name, string? description)
    {
        foreach (var connection in EnumerateConnections(netShare))
        {
            dynamic props = netShare.NetConnectionProps(connection);
            var connectionName = (string)props.Name;
            var deviceName = (string)props.DeviceName;

            if (string.Equals(connectionName, name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(deviceName, name, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(description) &&
                 (string.Equals(connectionName, description, StringComparison.OrdinalIgnoreCase) ||
                  string.Equals(deviceName, description, StringComparison.OrdinalIgnoreCase))))
            {
                return connection;
            }
        }

        return null;
    }

    private static void DisableAllSharing(dynamic netShare)
    {
        foreach (var connection in EnumerateConnections(netShare))
        {
            dynamic config = netShare.INetSharingConfigurationForINetConnection(connection);
            if (config.SharingEnabled)
            {
                config.DisableSharing();
            }
        }
    }

    private static string ConvertSharingType(int type)
    {
        return type switch
        {
            PublicConnection => "公共/来源",
            PrivateConnection => "私有/出口",
            _ => $"未知({type})"
        };
    }
}
