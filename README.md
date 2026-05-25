# My Caption

语言：简体中文 | [English](README.en.md)

My Caption 是一个面向 Windows Live Captions 的字幕辅助工具。它会捕获实时字幕文本，稳定快速变化的临时字幕，可选地进行翻译，并通过轻量悬浮窗显示字幕和单词查询结果。

## 功能特点

- 通过 UI Automation 捕获 Windows Live Captions 文本。
- 减少实时字幕片段快速变化带来的闪烁。
- 使用轻量 WPF 悬浮窗显示字幕。
- 内置 Argos Translate 离线翻译资源。
- 内置 MDict 词典查询能力。
- 全程离线运行；安装完成后，即使没有网络，也可以正常使用字幕捕获、离线翻译和词典查询。
- 按住 `Alt` 可临时与悬浮窗交互。
- 用户设置保存到 AppData 下的可写目录。
- 提供基于 Inno Setup 的 Windows 安装器。

## 发布版本

推荐从 GitHub Releases 下载 Windows 安装包：

- [下载最新版](https://github.com/JSLEE-0703/My_Caption/releases/latest)
- [查看全部版本](https://github.com/JSLEE-0703/My_Caption/releases)

## 安装

从发布页下载安装包，然后在 Windows 上运行安装程序。

安装器会把应用安装到 `Program Files`，并一并安装默认离线翻译和词典查询需要的 `runtime`、`dictionary`、`tools` 资源。

应用目标运行环境是 `.NET Framework 4.8`。安装器会检测 .NET Framework 4.8，未检测到时会显示提示，但不会自动安装 .NET。

## 使用方式

> 使用前请确认电脑运行 Windows 11，并且 Windows 实时字幕功能可以正常启用。My Caption 会自动启动并连接 Windows 实时字幕；如果实时字幕无法启动，本软件的字幕捕获、翻译和查词流程都无法正常工作。

1. 启动 My Caption。
2. 等待软件自动启动并连接 Windows 实时字幕。
3. 在控制面板中配置字幕显示、翻译和词典查询。
4. 日常使用时保持悬浮窗点击穿透。
5. 按住 `Alt` 后可以移动悬浮窗，或点击英文单词打开词典弹窗。

首次生成配置时，应用会优先使用内置离线资源：

- 翻译默认使用内置 Python 运行时和 Argos 桥接脚本。
- 词典查询默认使用 `dictionary\default.mdx`。

## 设置

设置文件保存在：

```text
%AppData%\My Caption\settings.json
```

如果旧版本曾在应用目录中保存 `settings.json`，并且 AppData 中还没有设置文件，My Caption 会在首次加载时把旧配置复制到 AppData 位置。

卸载应用时，`%AppData%\My Caption` 默认会保留。卸载程序会询问是否删除这个用户设置目录。

## 卸载行为

Inno Setup 卸载程序会删除已安装的程序文件和快捷方式。

安装目录下由运行时产生的缓存也会随这些安装载荷目录一起递归清理：

- `runtime`
- `tools`
- `dictionary`

用户设置与安装目录分离，只有在卸载时选择删除 `%AppData%\My Caption` 才会被移除。

## 内置资源

当前发布版本包含：

- `runtime\python\`：翻译和 MDict 查询使用的内置 Python 运行时。
- `runtime\argos-data\`：Argos Translate 离线数据。
- `dictionary\default.mdx`：默认 MDict 词典。
- `dictionary\ATTRIBUTION.txt`：内置词典的来源和许可说明。
- `tools\argos_translate_stdin.py`：Argos 翻译桥接脚本。
- `tools\mdict_query_stdin.py`：MDict 查询桥接脚本。
- `assets\icon\MyCaption.ico`：应用和安装器图标。

仓库对部分大型运行时文件使用 Git LFS。如果从源码构建或打包，请先初始化 Git LFS 并拉取完整文件，再验证内置离线运行时。

## 仓库结构

- `src\`：WPF 应用源码。
- `installer\`：Inno Setup 安装器脚本。
- `runtime\`：内置离线运行时资源。
- `dictionary\`：内置词典和来源说明。
- `tools\`：翻译、查询和词典导入相关辅助脚本。
- `assets\icon\`：源图标和 Windows 图标文件。
- `docs\notes\`：不属于用户说明页的项目记录。

## 致谢

默认内置词典来自 [skywind3000/ECDICT](https://github.com/skywind3000/ECDICT)。

内置词典的来源和许可信息记录在 `dictionary\ATTRIBUTION.txt`。
