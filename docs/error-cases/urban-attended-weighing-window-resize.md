# 错误样例：Urban 无边框主窗口无法拖动缩放

> **修复状态**：✅ 已修复（2026-05-21）  
> **Git 状态**：改动可能尚未提交；以本文档 + `.patch` 为唯一重放来源。

## 重放修改（优先）

在仓库根目录 `MaterialClient/` 执行：

```powershell
# 方式 1：应用补丁（推荐）
git apply docs/error-cases/urban-attended-weighing-window-resize.patch

# 若行尾/路径冲突，可尝试：
git apply --ignore-whitespace docs/error-cases/urban-attended-weighing-window-resize.patch

# 方式 2：检查补丁能否应用（不修改文件）
git apply --check docs/error-cases/urban-attended-weighing-window-resize.patch
```

**涉及文件（仅 2 个）**：

| 文件 | 操作 |
|------|------|
| `MaterialClient.Urban/Views/UrbanAttendedWeighingWindow.axaml` | 修改 |
| `MaterialClient.Urban/Views/UrbanAttendedWeighingWindow.axaml.cs` | 修改 |

**验证**：编译 `MaterialClient.Urban`，运行后拖动窗口四边/四角，`Width`/`Height` 应变化，且不低于 `MinWidth=900`、`MinHeight=600`。

补丁全文见同目录：`urban-attended-weighing-window-resize.patch`。

---

## 代码改动摘要

| # | 文件 | 改动 |
|---|------|------|
| 1 | `.axaml` L11 | `SystemDecorations="None"` → `BorderOnly` |
| 2 | `.axaml` L17 | `ContentGrid` 增加 `ZIndex="0"` |
| 3 | `.axaml` L312–338 | 新增 8 个透明缩放热区 `Border`（四边 6px、四角 10px） |
| 4 | `.axaml.cs` | 新增 `ResizeGrip_OnPointerPressed`、`ResolveResizeEdge` |

---

## 完整 Diff（与 patch 一致）

### `UrbanAttendedWeighingWindow.axaml`

```diff
--- a/MaterialClient.Urban/Views/UrbanAttendedWeighingWindow.axaml
+++ b/MaterialClient.Urban/Views/UrbanAttendedWeighingWindow.axaml
@@ -8,13 +8,13 @@
         Width="1280" Height="800"
         MinWidth="900" MinHeight="600"
         WindowStartupLocation="CenterScreen"
-        SystemDecorations="None"
+        SystemDecorations="BorderOnly"
         CanResize="True"
         Icon="/Assets/fd-ico.ico"
         Background="{DynamicResource BackgroundGray}">
 
     <Grid>
-        <Grid x:Name="ContentGrid">
+        <Grid x:Name="ContentGrid" ZIndex="0">
             <Grid.RowDefinitions>
                 <RowDefinition Height="48" />
                 <RowDefinition Height="72" />
@@ -308,5 +308,33 @@
                 </ItemsControl>
             </Border>
         </Grid>
+
+        <!-- Wider resize grips; requires BorderOnly so Win32 WS_THICKFRAME is enabled -->
+        <Border x:Name="ResizeNorth" Height="6" VerticalAlignment="Top" HorizontalAlignment="Stretch"
+                Background="Transparent" Cursor="SizeNorthSouth" ZIndex="100"
+                PointerPressed="ResizeGrip_OnPointerPressed" />
+        <Border x:Name="ResizeSouth" Height="6" VerticalAlignment="Bottom" HorizontalAlignment="Stretch"
+                Background="Transparent" Cursor="SizeNorthSouth" ZIndex="100"
+                PointerPressed="ResizeGrip_OnPointerPressed" />
+        <Border x:Name="ResizeWest" Width="6" HorizontalAlignment="Left" VerticalAlignment="Stretch"
+                Background="Transparent" Cursor="SizeWestEast"
+                Margin="0,6,0,6" ZIndex="100"
+                PointerPressed="ResizeGrip_OnPointerPressed" />
+        <Border x:Name="ResizeEast" Width="6" HorizontalAlignment="Right" VerticalAlignment="Stretch"
+                Background="Transparent" Cursor="SizeWestEast"
+                Margin="0,6,0,6" ZIndex="100"
+                PointerPressed="ResizeGrip_OnPointerPressed" />
+        <Border x:Name="ResizeNorthWest" Width="10" Height="10" HorizontalAlignment="Left" VerticalAlignment="Top"
+                Background="Transparent" Cursor="TopLeftCorner" ZIndex="101"
+                PointerPressed="ResizeGrip_OnPointerPressed" />
+        <Border x:Name="ResizeNorthEast" Width="10" Height="10" HorizontalAlignment="Right" VerticalAlignment="Top"
+                Background="Transparent" Cursor="TopRightCorner" ZIndex="101"
+                PointerPressed="ResizeGrip_OnPointerPressed" />
+        <Border x:Name="ResizeSouthWest" Width="10" Height="10" HorizontalAlignment="Left" VerticalAlignment="Bottom"
+                Background="Transparent" Cursor="BottomLeftCorner" ZIndex="101"
+                PointerPressed="ResizeGrip_OnPointerPressed" />
+        <Border x:Name="ResizeSouthEast" Width="10" Height="10" HorizontalAlignment="Right" VerticalAlignment="Bottom"
+                Background="Transparent" Cursor="BottomRightCorner" ZIndex="101"
+                PointerPressed="ResizeGrip_OnPointerPressed" />
     </Grid>
 </Window>
```

### `UrbanAttendedWeighingWindow.axaml.cs`

```diff
--- a/MaterialClient.Urban/Views/UrbanAttendedWeighingWindow.axaml.cs
+++ b/MaterialClient.Urban/Views/UrbanAttendedWeighingWindow.axaml.cs
@@ -41,6 +31,32 @@ public partial class UrbanAttendedWeighingWindow : Window, ITransientDependency
             BeginMoveDrag(e);
     }
 
+    private void ResizeGrip_OnPointerPressed(object? sender, PointerPressedEventArgs e)
+    {
+        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
+            return;
+
+        var edge = ResolveResizeEdge(sender);
+        if (edge is null)
+            return;
+
+        BeginResizeDrag(edge.Value, e);
+    }
+
+    private static WindowEdge? ResolveResizeEdge(object? sender) =>
+        sender switch
+        {
+            Border { Name: "ResizeNorth" } => WindowEdge.North,
+            Border { Name: "ResizeSouth" } => WindowEdge.South,
+            Border { Name: "ResizeWest" } => WindowEdge.West,
+            Border { Name: "ResizeEast" } => WindowEdge.East,
+            Border { Name: "ResizeNorthWest" } => WindowEdge.NorthWest,
+            Border { Name: "ResizeNorthEast" } => WindowEdge.NorthEast,
+            Border { Name: "ResizeSouthWest" } => WindowEdge.SouthWest,
+            Border { Name: "ResizeSouthEast" } => WindowEdge.SouthEast,
+            _ => null,
+        };
+
     private void OnMinimizeButtonClick(object? sender, RoutedEventArgs e)
         => WindowState = WindowState.Minimized;
```

（注释删除、括号风格等次要差异见 `.patch`；重放以 patch 为准。）

---

## 修复后完整文件：`UrbanAttendedWeighingWindow.axaml.cs`

补丁应用后，该文件应包含以下完整内容（便于手工对照或覆盖）：

```csharp
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MaterialClient.Urban.ViewModels;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.Urban.Views;

public partial class UrbanAttendedWeighingWindow : Window, ITransientDependency
{
    public UrbanAttendedWeighingWindow()
    {
        InitializeComponent();
    }

    public UrbanAttendedWeighingWindow(UrbanAttendedWeighingViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        VehicleList.ItemsSource = viewModel.WeighingRecords;
        DeviceStatusList.ItemsSource = viewModel.DeviceStatuses;
    }

    public UrbanAttendedWeighingViewModel? ViewModel => DataContext as UrbanAttendedWeighingViewModel;

    private void TitleBar_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void ResizeGrip_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        var edge = ResolveResizeEdge(sender);
        if (edge is null)
            return;

        BeginResizeDrag(edge.Value, e);
    }

    private static WindowEdge? ResolveResizeEdge(object? sender) =>
        sender switch
        {
            Border { Name: "ResizeNorth" } => WindowEdge.North,
            Border { Name: "ResizeSouth" } => WindowEdge.South,
            Border { Name: "ResizeWest" } => WindowEdge.West,
            Border { Name: "ResizeEast" } => WindowEdge.East,
            Border { Name: "ResizeNorthWest" } => WindowEdge.NorthWest,
            Border { Name: "ResizeNorthEast" } => WindowEdge.NorthEast,
            Border { Name: "ResizeSouthWest" } => WindowEdge.SouthWest,
            Border { Name: "ResizeSouthEast" } => WindowEdge.SouthEast,
            _ => null,
        };

    private void OnMinimizeButtonClick(object? sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void OnCloseButtonClick(object? sender, RoutedEventArgs e)
        => Close();

    private void OnTabClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button clickedTab) return;
        if (ViewModel == null) return;

        TabAll.Classes.Remove("active");
        TabNormal.Classes.Remove("active");
        TabAbnormal.Classes.Remove("active");

        clickedTab.Classes.Add("active");

        var tabText = clickedTab.Content?.ToString();
        if (tabText != null)
            ViewModel.SetFilterTab(tabText);
    }

    private void OnRecordClick(object? sender, PointerPressedEventArgs e)
    {
        // TODO: Load photos for selected record
    }
}
```

---

## 修复后 `.axaml` 关键片段（其余 UI 不变）

**Window 根属性**（约 L7–14）：

```xml
        WindowStartupLocation="CenterScreen"
        SystemDecorations="BorderOnly"
        CanResize="True"
```

**根 `Grid` 结构**（`ContentGrid` 闭合后、`</Grid>` 前插入热区）：

```xml
    <Grid>
        <Grid x:Name="ContentGrid" ZIndex="0">
            <!-- 原有 Row 0–3 内容不变 -->
        </Grid>

        <!-- Wider resize grips; requires BorderOnly so Win32 WS_THICKFRAME is enabled -->
        <Border x:Name="ResizeNorth" Height="6" VerticalAlignment="Top" HorizontalAlignment="Stretch"
                Background="Transparent" Cursor="SizeNorthSouth" ZIndex="100"
                PointerPressed="ResizeGrip_OnPointerPressed" />
        <Border x:Name="ResizeSouth" Height="6" VerticalAlignment="Bottom" HorizontalAlignment="Stretch"
                Background="Transparent" Cursor="SizeNorthSouth" ZIndex="100"
                PointerPressed="ResizeGrip_OnPointerPressed" />
        <Border x:Name="ResizeWest" Width="6" HorizontalAlignment="Left" VerticalAlignment="Stretch"
                Background="Transparent" Cursor="SizeWestEast"
                Margin="0,6,0,6" ZIndex="100"
                PointerPressed="ResizeGrip_OnPointerPressed" />
        <Border x:Name="ResizeEast" Width="6" HorizontalAlignment="Right" VerticalAlignment="Stretch"
                Background="Transparent" Cursor="SizeWestEast"
                Margin="0,6,0,6" ZIndex="100"
                PointerPressed="ResizeGrip_OnPointerPressed" />
        <Border x:Name="ResizeNorthWest" Width="10" Height="10" HorizontalAlignment="Left" VerticalAlignment="Top"
                Background="Transparent" Cursor="TopLeftCorner" ZIndex="101"
                PointerPressed="ResizeGrip_OnPointerPressed" />
        <Border x:Name="ResizeNorthEast" Width="10" Height="10" HorizontalAlignment="Right" VerticalAlignment="Top"
                Background="Transparent" Cursor="TopRightCorner" ZIndex="101"
                PointerPressed="ResizeGrip_OnPointerPressed" />
        <Border x:Name="ResizeSouthWest" Width="10" Height="10" HorizontalAlignment="Left" VerticalAlignment="Bottom"
                Background="Transparent" Cursor="BottomLeftCorner" ZIndex="101"
                PointerPressed="ResizeGrip_OnPointerPressed" />
        <Border x:Name="ResizeSouthEast" Width="10" Height="10" HorizontalAlignment="Right" VerticalAlignment="Bottom"
                Background="Transparent" Cursor="BottomRightCorner" ZIndex="101"
                PointerPressed="ResizeGrip_OnPointerPressed" />
    </Grid>
```

---

## 问题说明

### 规则

1. `CanResize="True"` 不自动提供可拖动缩放热区。
2. Windows 上需 `SystemDecorations != None` 才有 `WS_THICKFRAME`；`BeginResizeDrag` 依赖该样式。
3. **ViewModel 不参与窗口尺寸**；勿在 `UrbanAttendedWeighingViewModel` 中修窗口缩放。

### 根因（修复前）

```xml
SystemDecorations="None"
CanResize="True"
```

Avalonia Win32（11.3.x）：

```csharp
if (newProperties.Decorations != WindowDecorations.None && newProperties.IsResizable)
    style |= WindowStyles.WS_THICKFRAME;
```

### 参考

`MaterialClient.Demo/Views/WeighingSystemWindow.axaml` 使用 `SystemDecorations="BorderOnly"`，可正常缩放。

---

## 验证记录

日志 `MaterialClient.Urban/bin/Debug/net10.0/win-x64/Logs/MaterialClient.Urban-*.log`（2026-05-21 16:37）：

```text
Opened: SystemDecorations="BorderOnly", CanResize=true, Width=1280, Height=800
SizeChanged: 1280x800 -> 900x600
SizeChanged: 900x600 -> 1426x833
```

曾临时添加 `[WindowResize]` 诊断日志，确认后已移除，**不在**本修复补丁内。

---

## 备选方案

| 方案 | 说明 |
|------|------|
| **A. BorderOnly（本修复）** | 与 Demo 一致，改动最小 |
| **B. None + 手动改 Width/Height** | 完全无边框时 |
| **C. WindowDecorationProperties.ElementRole** | 较新 Avalonia + `ExtendClientAreaToDecorationsHint` |

---

## 相关

- [Avalonia: How to work with windows](https://docs.avaloniaui.net/docs/how-to/window-how-to)
- `AGENTS.md` → 错误样例文档 → `docs/error-cases/`
