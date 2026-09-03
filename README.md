# Desktop Pet

一个运行在 Windows 桌面上的轻量级桌宠原型，使用 C#、WPF 和少量 WinForms 系统托盘能力编写。

## 功能

- 透明、无边框、始终置顶的桌宠窗口
- 待机动画和拖拽动画
- 鼠标拖拽，并限制在当前屏幕工作区内
- 兼容多屏幕和常见 DPI 缩放场景
- 右键设置透明度、播放速度和桌宠大小
- 系统托盘图标、显示和退出菜单
- 自动保存桌宠位置与设置
- 支持 PNG 序列帧动画；素材缺失时提供代码绘制的备用形象

## 技术栈

- C#
- .NET 8
- WPF
- WinForms NotifyIcon
- PowerShell 素材处理脚本

## 运行

系统要求：Windows 10/11 和 .NET 8 SDK。

```powershell
dotnet restore
dotnet run
```

构建发布版本：

```powershell
dotnet publish -c Release --self-contained false
```

如果机器没有 .NET SDK，但安装了 Windows .NET Framework 开发工具，也可以尝试兼容构建脚本：

```powershell
.\build-legacy.ps1
.\bin\DesktopPet.exe
```

## 操作方式

- 左键按住桌宠：拖动位置
- 右键松开：打开设置面板
- 托盘图标双击：显示桌宠
- 设置面板中的“关闭桌宠”：退出程序

## 项目结构

```text
DesktopPet.cs          # 程序入口、窗口、动画、设置面板和自绘控件
DesktopPet.csproj      # .NET 8 WPF 项目配置
assets/                # 动画帧、托盘图标及素材源文件
prepare-assets.ps1     # 生成透明 PNG 和托盘图标
build-legacy.ps1       # 无 .NET SDK 时的兼容构建脚本
```

运行时设置保存在 `%LOCALAPPDATA%\\DesktopPet\\settings.json`。编译输出位于 `bin/`，该目录不会提交到 Git 仓库。

## 替换角色

可以将 `assets/idle` 和 `assets/drag` 中的 PNG 替换为自己的序列帧素材。更复杂的角色行为可以继续扩展 `PetVisual`，或者将其改造成独立的动画播放器。

## 素材说明

仓库中的角色素材由 AI 生成并经过透明背景处理，相关源文件保存在 `assets/source`。使用或再分发素材时，请遵守生成平台的使用条款。

## License

代码以 MIT License 发布，详见 [LICENSE](LICENSE)。素材可能受不同于代码的使用条款约束。
by codex
