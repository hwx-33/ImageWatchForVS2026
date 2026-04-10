# ImageWatch for Visual Studio 2022+

在 Visual Studio 调试器中实时预览 OpenCV `cv::Mat` 图像变量的扩展插件。

---

## 功能概述

调试 C++ 项目时，Image Watch 会自动扫描当前栈帧中的所有 `cv::Mat` 局部变量，将其以图像形式呈现在专用工具窗口中，无需手动导出或打印。

### 主要功能

| 功能 | 说明 |
|------|------|
| **实时图像预览** | 每次命中断点时自动刷新，即时显示当前帧的图像内容 |
| **变量列表** | 列出当前作用域内所有 `cv::Mat` 变量，点击切换预览 |
| **缩略图** | 每个变量旁自动生成缩略图，快速浏览多张图像 |
| **像素值查看** | 鼠标悬停图像时，状态栏实时显示像素坐标及各通道数值 |
| **像素值叠加** | 缩放至 8× 以上时，每个像素格内直接绘制通道数值 |
| **缩放与平移** | 滚轮缩放（最大 64×），左键拖拽平移，双击恢复适应窗口 |
| **调试会话联动** | 自动响应调试启动 / 停止 / 单步事件，停止调试后自动清空列表 |
| **缓存机制** | 切换变量时优先使用缓存，避免重复读取进程内存 |

### 支持的数据类型

| OpenCV 类型 | 显示名称 |
|-------------|---------|
| `CV_8U` | UINT8 |
| `CV_8S` | INT8 |
| `CV_16U` | UINT16 |
| `CV_16S` | INT16 |
| `CV_32S` | INT32 |
| `CV_32F` | FLOAT32 |
| `CV_64F` | FLOAT64 |

支持单通道（灰度）、三通道（BGR）、四通道（BGRA）等所有常见通道数。

---

## 环境要求

- **Visual Studio 2022 及更高版本**（v17.0+，x64）
- **.NET Framework 4.7.2**
- 调试目标为使用 OpenCV 的 C++ 项目

---

## 安装

1. 从 [Releases](../../releases) 页面下载最新的 `.vsix` 文件
2. 双击 `.vsix` 文件，按提示完成安装
3. 重启 Visual Studio

---

## 使用方法

1. 在 Visual Studio 中打开工具窗口：**视图 → 其他窗口 → Image Watch**
2. 启动 C++ 项目的调试（F5）
3. 在包含 `cv::Mat` 变量的代码行设置断点
4. 命中断点后，Image Watch 窗口自动显示所有可用的 Mat 变量
5. 在左侧变量列表中点击变量名切换预览图像

### 交互操作

| 操作 | 效果 |
|------|------|
| 鼠标滚轮 | 以鼠标位置为中心缩放 |
| 左键拖拽 | 平移图像 |
| 双击图像 | 恢复适应窗口大小 |
| 鼠标悬停 | 状态栏显示像素坐标和通道值 |

---

## 项目结构

```
ImageWatch/
├── Commands/               # VS 命令：打开工具窗口
├── Controls/               # ZoomableImageCanvas 自定义控件
├── Debugger/               # 调试器集成
│   ├── DebugMemoryReader.cs       # 直接读取被调试进程内存
│   ├── DebugSessionManager.cs     # 监听调试事件（断点/停止）
│   └── MatExpressionEvaluator.cs  # 通过 DTE 表达式求值读取 Mat 字段
├── Imaging/                # 图像处理
│   ├── MatBitmapConverter.cs      # Mat 原始数据 → WPF BitmapSource
│   └── PixelValueFormatter.cs     # 像素通道值格式化
├── Models/                 # 数据模型
│   ├── MatInfo.cs                 # Mat 元数据（尺寸、类型、内存指针）
│   └── MatTypeHelper.cs           # OpenCV 类型常量与转换工具
├── ToolWindow/             # 工具窗口宿主与 XAML 视图
├── ViewModels/             # MVVM ViewModel
│   ├── ImageWatchViewModel.cs     # 主 ViewModel，协调调试与 UI
│   └── MatVariableItem.cs         # 变量列表单项（含缩略图缓存）
└── ImageWatchPackage.cs    # AsyncPackage 入口，扩展注册
```

---

## 构建

在 Visual Studio 中按 `Ctrl+Shift+B`，或使用 MSBuild：

```bash
msbuild ImageWatch.csproj /p:Configuration=Release /p:Platform=AnyCPU
```

输出产物为 `bin/Release/ImageWatch.vsix`。

按 `F5` 可在实验性 VS 实例中调试扩展本身。

---

## 工作原理

1. **变量发现**：命中断点时，通过 DTE `Debugger.CurrentStackFrame.Locals` 枚举局部变量，筛选类型包含 `cv::Mat` 的项。
2. **元数据读取**：对每个 Mat 变量，使用 `Debugger.GetExpression()` 求值 `rows`、`cols`、`flags`、`step`、`data` 字段，构造 `MatInfo`。
3. **内存读取**：通过 `ReadProcessMemory` Win32 API，根据 `data` 指针和计算出的数据大小，从被调试进程读取原始像素字节。
4. **图像转换**：将原始字节按 OpenCV 内存布局（行步长、通道顺序）转换为 WPF `BitmapSource`，同时生成缩略图。
5. **渲染**：`ZoomableImageCanvas` 使用 WPF `DrawingContext` 直接绘制图像、像素网格和通道值叠加。

---

## License

[MIT](LICENSE)
