# NetShareTool

NetShareTool 是一个 Windows 桌面小工具，用来快速开启或关闭 Windows ICS（Internet Connection Sharing）网络共享。

## 功能

- 列出本机网络适配器
- 选择上网来源网卡和共享出口网卡
- 一键开启 ICS 网络共享
- 一键禁用全部 ICS 共享
- 一键尝试开启 Mihomo TUN 透明代理热点
- 显示当前 ICS 共享状态
- 后台执行共享操作，避免界面卡顿
- 写入本地日志，方便排查问题

## 运行要求

- Windows 10/11
- 管理员权限
- 小体积发布版需要安装 .NET 9 Desktop Runtime

程序启动时会请求管理员权限，因为 Windows ICS 配置需要提升权限。

## 透明代理热点

使用“开启透明代理热点”前，请先在 Mihomo/Clash 中开启 TUN 模式。程序会尝试启动 Windows 移动热点，然后自动设置 ICS：

```text
Mihomo TUN 网卡 -> Windows 热点虚拟网卡
```

如果 Windows 不允许程序直接开启移动热点，程序会打开系统移动热点设置页。手动开启热点后，再点击一次“开启透明代理热点”即可。

## 开发

```powershell
cd NetShareTool
dotnet build
dotnet run
```

## 发布

小体积单文件版：

```powershell
dotnet publish -c Release -r win-x64 --self-contained false -o ..\dist-small-single /p:PublishSingleFile=true /p:EnableCompressionInSingleFile=false /p:DebugType=none /p:DebugSymbols=false
```

自包含单文件版：

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -o ..\dist /p:PublishSingleFile=true
```

## 注意

开启共享前，程序会先禁用当前已有 ICS 共享，以避免 Windows ICS 配置冲突。
