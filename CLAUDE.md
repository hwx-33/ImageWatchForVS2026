# CLAUDE.md

本文件为 Claude Code (claude.ai/code) 在此仓库中工作时提供指引。

## 项目概述

ImageWatch 是一个面向 Visual Studio 2022+（v17.0+）的扩展（VSIX），使用 C# 和 .NET Framework 4.7.2 构建，采用 VS SDK 的 AsyncPackage 模型。

## 构建

在 Visual Studio 中按 Ctrl+Shift+B，或通过 MSBuild 命令行构建：

```bash
msbuild ImageWatch.csproj /p:Configuration=Debug /p:Platform=AnyCPU
msbuild ImageWatch.csproj /p:Configuration=Release /p:Platform=AnyCPU
```

输出目录为 `bin/Debug/` 或 `bin/Release/`，构建产物为 `.vsix` 包。

调试/运行扩展时，在 Visual Studio 中按 F5，会以 `/rootsuffix Exp` 启动一个实验性 VS 实例。

## 架构

- **`ImageWatchPackage.cs`** — 入口点。继承 `AsyncPackage`，通过 `[PackageRegistration]` 注册。扩展启动逻辑写在 `InitializeAsync()` 中；凡涉及 UI 线程的操作，必须先执行 `await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken)`。
- **`source.extension.vsixmanifest`** — 扩展元数据（ID、版本、依赖项、目标 VS 版本）。
- **`Properties/AssemblyInfo.cs`** — 程序集版本信息。

## 关键依赖

- `Microsoft.VisualStudio.SDK` — VS 扩展性 API
- `Microsoft.VSSDK.BuildTools` — VSIX 构建工具

## VS 扩展开发模式

所有涉及 UI 的 VS API 调用必须在主线程上执行。调用前需使用 `JoinableTaskFactory.SwitchToMainThreadAsync()`。在异步上下文中应通过 `GetServiceAsync<T>()` 获取服务，而非同步的 `GetService<T>()`。
