using System.Windows;

namespace NetShareTool;

public partial class MainWindow : Window
{
    private readonly InternetSharingService _sharingService = new();
    private readonly HotspotService _hotspotService = new();

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
        PublicAdapterCombo.SelectionChanged += (_, _) => UpdateAdapterDescriptions();
        PrivateAdapterCombo.SelectionChanged += (_, _) => UpdateAdapterDescriptions();
        AppLogger.Info($"程序启动，日志文件：{AppLogger.LogFilePath}");
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await RefreshAdaptersAndStatusAsync();
    }

    private async void EnableButton_Click(object sender, RoutedEventArgs e)
    {
        if (PublicAdapterCombo.SelectedItem is not NetworkAdapterInfo publicAdapter ||
            PrivateAdapterCombo.SelectedItem is not NetworkAdapterInfo privateAdapter)
        {
            ShowMessage("请先选择来源网卡和共享出口。");
            return;
        }

        await RunOperationAsync($"开启共享：{publicAdapter.Name} -> {privateAdapter.Name}", async () =>
        {
            await StaTaskRunner.Run(() => _sharingService.EnableSharing(publicAdapter.Name, privateAdapter.Name));
            await RefreshStatusAsync();
            ShowMessage($"已开启共享：{publicAdapter.Name} -> {privateAdapter.Name}");
        });
    }

    private async void DisableButton_Click(object sender, RoutedEventArgs e)
    {
        await RunOperationAsync("禁用全部网络共享", async () =>
        {
            await StaTaskRunner.Run(() => _sharingService.DisableSharing());
            await RefreshStatusAsync();
            ShowMessage("已禁用全部网络共享。");
        });
    }

    private async void TransparentHotspotButton_Click(object sender, RoutedEventArgs e)
    {
        await RunOperationAsync("开启透明代理热点", async () =>
        {
            var hotspotResult = await _hotspotService.StartMobileHotspotAsync();
            if (!hotspotResult.Started)
            {
                AppLogger.Info($"移动热点自动启动未完成：{hotspotResult.Message}");
                ShowMessage("请在打开的系统设置中手动开启移动热点，然后点刷新或重试。");
                MessageBox.Show(
                    this,
                    "Windows 没有允许程序直接开启移动热点，已打开系统热点设置页。\n\n请手动开启移动热点后，再点击“开启透明代理热点”。",
                    "需要手动开启热点",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            await Task.Delay(2500);
            var adapters = _sharingService.GetAdapters();
            var mihomoAdapter = SelectPreferredAdapter(adapters, "Mihomo", "Clash", "Meta")
                ?? throw new InvalidOperationException("未找到 Mihomo TUN 网卡。请先在 Mihomo/Clash 中开启 TUN 模式。");
            var hotspotAdapter = SelectHotspotAdapter(adapters)
                ?? throw new InvalidOperationException("未找到移动热点虚拟网卡。请确认 Windows 移动热点已经开启。");

            PublicAdapterCombo.ItemsSource = adapters;
            PrivateAdapterCombo.ItemsSource = adapters;
            PublicAdapterCombo.SelectedItem = mihomoAdapter;
            PrivateAdapterCombo.SelectedItem = hotspotAdapter;
            UpdateAdapterDescriptions();

            await StaTaskRunner.Run(() => _sharingService.EnableSharing(mihomoAdapter.Name, hotspotAdapter.Name));
            await RefreshStatusAsync();
            ShowMessage($"透明代理热点已开启：{mihomoAdapter.Name} -> {hotspotAdapter.Name}");
        });
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshAdaptersAndStatusAsync();
    }

    private void OpenLogButton_Click(object sender, RoutedEventArgs e)
    {
        AppLogger.OpenLogFolder();
    }

    private async Task RefreshAdaptersAndStatusAsync()
    {
        await RunOperationAsync("刷新网卡和共享状态", async () =>
        {
            var adapters = _sharingService.GetAdapters();
            PublicAdapterCombo.ItemsSource = adapters;
            PrivateAdapterCombo.ItemsSource = adapters;

            PublicAdapterCombo.SelectedItem = SelectPreferredAdapter(adapters, "WLAN", "Wi-Fi", "无线");
            PrivateAdapterCombo.SelectedItem = SelectPreferredAdapter(adapters, "以太网", "Ethernet");

            await RefreshStatusAsync();
            UpdateAdapterDescriptions();
            ShowMessage("状态已刷新。");
        });
    }

    private async Task RefreshStatusAsync()
    {
        StatusGrid.ItemsSource = await StaTaskRunner.Run(() => _sharingService.GetSharingStatus());
    }

    private static NetworkAdapterInfo? SelectPreferredAdapter(IReadOnlyList<NetworkAdapterInfo> adapters, params string[] names)
    {
        return names
            .Select(name => adapters.FirstOrDefault(adapter =>
                adapter.Name.Contains(name, StringComparison.OrdinalIgnoreCase) ||
                adapter.Description.Contains(name, StringComparison.OrdinalIgnoreCase)))
            .FirstOrDefault(adapter => adapter is not null)
            ?? adapters.FirstOrDefault();
    }

    private static NetworkAdapterInfo? SelectHotspotAdapter(IReadOnlyList<NetworkAdapterInfo> adapters)
    {
        var preferredNames = new[]
        {
            "本地连接*",
            "Local Area Connection*",
            "Wi-Fi Direct",
            "Microsoft Wi-Fi Direct",
            "热点",
            "Hotspot"
        };

        return adapters
            .Where(adapter => adapter.Status == System.Net.NetworkInformation.OperationalStatus.Up)
            .FirstOrDefault(adapter => preferredNames.Any(name =>
                adapter.Name.Contains(name, StringComparison.OrdinalIgnoreCase) ||
                adapter.Description.Contains(name, StringComparison.OrdinalIgnoreCase)))
            ?? adapters.FirstOrDefault(adapter =>
                adapter.Description.Contains("Wi-Fi Direct", StringComparison.OrdinalIgnoreCase));
    }

    private void UpdateAdapterDescriptions()
    {
        PublicDescriptionText.Text = BuildDescription(PublicAdapterCombo.SelectedItem as NetworkAdapterInfo);
        PrivateDescriptionText.Text = BuildDescription(PrivateAdapterCombo.SelectedItem as NetworkAdapterInfo);
    }

    private static string BuildDescription(NetworkAdapterInfo? adapter)
    {
        if (adapter is null)
        {
            return string.Empty;
        }

        return $"{adapter.Type} | {adapter.Description}";
    }

    private async Task RunOperationAsync(string operationName, Func<Task> operation)
    {
        try
        {
            SetBusy(true);
            ShowMessage($"{operationName}...");
            AppLogger.Info($"开始：{operationName}");
            await operation();
            AppLogger.Info($"完成：{operationName}");
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, $"失败：{operationName}");
            ShowMessage(ex.Message);
            MessageBox.Show(this, ex.Message, "操作失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool isBusy)
    {
        EnableButton.IsEnabled = !isBusy;
        TransparentHotspotButton.IsEnabled = !isBusy;
        DisableButton.IsEnabled = !isBusy;
        RefreshButton.IsEnabled = !isBusy;
        OpenLogButton.IsEnabled = !isBusy;
    }

    private void ShowMessage(string message)
    {
        StatusText.Text = message;
    }
}
