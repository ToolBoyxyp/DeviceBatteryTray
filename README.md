# DeviceBatteryTray

Windows 任务栏外接设备电量托盘，原生 C# WinForms 实现。

支持读取以下设备的电量：

- **蓝牙 LE 设备**：标准 GATT Battery Service（`0x180F` / `0x2A19`）
- **经典蓝牙耳机**：Windows HFP 电池属性（`{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2`）
- **VXE / Compx 2.4G 接收器**：厂商 HID 报告（VID `0x3554`）

## 功能

- 左键托盘图标：紧凑卡片，只显示当前已连接设备
- 右键托盘图标：完整卡片，显示全部设备、电量、来源和进度条
- 深色 / 浅色 / 跟随系统主题
- 可自定义强调色、电池图标样式（电池 / 圆环 / 数字）、电量阈值、字号、弹窗宽度、圆角
- 按 DPI 缩放，支持高 DPI 屏幕
- 每 30 秒自动刷新，可调整刷新间隔
- 支持开机自启
- 单文件自包含 EXE，不需要额外安装 .NET 运行时

## 系统要求

- Windows 10 1809+ 或 Windows 11
- 蓝牙设备需要已与 Windows 配对
- 经典蓝牙耳机需要在 Windows 设置中能看到电量（表示设备通过 HFP 上报电量）

## 下载与安装

从 Releases 下载：

- `DeviceBatteryTray.exe`：绿色单文件版，双击即可运行
- `DeviceBatteryTray-Setup.exe`：安装包，安装到当前用户目录并创建快捷方式

也可以自行构建，见下文。

## 使用

1. 启动 `DeviceBatteryTray.exe`，托盘区会出现电池图标。
2. 左键点击：查看已连接设备。
3. 右键点击：查看全部设备、打开设置、切换开机自启、打开蓝牙设置或退出。
4. 在“设置”中可以调整主题、图标、颜色、阈值、字号、弹窗宽度和圆角。

配置文件位于：

```text
%APPDATA%\DeviceBatteryTray\settings.json
```

## 构建

需要 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)。

```powershell
dotnet build src\DeviceBatteryTray\DeviceBatteryTray.csproj -c Release

dotnet publish src\DeviceBatteryTray\DeviceBatteryTray.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -o publish
```

或直接运行：

```powershell
.\build.ps1
```

生成安装包（需要 Inno Setup 6）：

```powershell
.\build.ps1 -Installer
```

## 诊断

```powershell
DeviceBatteryTray.exe --test
```

会把当前读取结果写入：

```text
%TEMP%\DeviceBatteryTray_test.txt
```

```powershell
DeviceBatteryTray.exe --preview
```

会显示一个示例弹窗，6 秒后自动退出，用于检查界面。

## 已知限制

- 只支持走蓝牙且暴露标准电池服务的设备。
- 2.4G 私有协议目前适配 **VXE / Compx（VID 3554）** 接收器；罗技 Unifying/Bolt、雷蛇 HyperSpeed 等使用其他协议，需要单独适配。
- 经典蓝牙耳机能否显示电量取决于耳机是否通过 HFP 上报；Windows 设置里没有电量的耳机，这里也无法读取。
- 设备休眠时可能暂时读不到电量，唤醒后下一次刷新会恢复。

## 代码签名

仓库默认构建的是未签名 EXE，Windows SmartScreen 可能提示“未知发布者”。正式分发时建议在 GitHub Actions 中配置代码签名证书，对 `publish/DeviceBatteryTray.exe` 和安装包进行签名。

## 项目结构

```text
src/DeviceBatteryTray/
  Program.cs                    入口、单实例、DPI
  AppConfig.cs                  配置读写
  Theme.cs                      主题和颜色
  Models.cs                     数据模型
  BatteryReader.cs              多来源合并
  BleBatterySource.cs           BLE GATT
  ClassicHfpBatterySource.cs    经典蓝牙 HFP + SetupAPI
  VxeHidBatterySource.cs        VXE 2.4G HID
  TrayApplicationContext.cs     托盘生命周期
  PopupForm.cs                  圆角弹窗
  SettingsForm.cs               设置界面
  TrayIconFactory.cs            托盘图标绘制
  NativeMethods.cs              P/Invoke
installer/DeviceBatteryTray.iss Inno Setup 安装脚本
.github/workflows/build.yml     GitHub Actions
```

## 许可证

MIT，见 [LICENSE](LICENSE)。
