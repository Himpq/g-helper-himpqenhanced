# Taskbar 浮窗置顶问题调试总结

## 背景

HimpqEnhanced 的任务栏监控窗口支持两种显示形式：

1. 嵌入任务栏。
2. 浮窗模式，贴近任务栏区域显示 Monitor 信息。

本次问题出现在浮窗模式。配置状态如下：

```text
taskbar_window_enabled=1
taskbar_window_floating_enabled=1
taskbar_floating_topmost=1
taskbar_floating_click_through=1
```

用户观察到的现象：

1. 点击任意窗口后再点击任务栏，Monitor 会闪一下，这属于任务栏重排时的正常响应。
2. 打开 Windows 徽标、搜索、通知中心、音量、WiFi、电池等 Shell 面板后，Monitor 被挤到后面；再次点击关闭面板后 Monitor 仍不显示，直到焦点移出任务栏才恢复。

## 关键日志

日志显示监听器已经识别到 Shell 面板，并且关闭后也执行了恢复流程：

```text
Himpq taskbar window: shell surface active mode=floating surface=0x200EC/Windows.UI.Core.CoreWindow:搜索
Himpq floating window: restored after shell surface=0x200EC/Windows.UI.Core.CoreWindow:搜索 nativeVisible=True managedVisible=True cloaked=0 rect=10,1516,786,1596 topMost=False foreground=0x1014A/Shell_TrayWnd

Himpq taskbar window: shell surface active mode=floating surface=0x421850/Windows.UI.Core.CoreWindow:通知中心
Himpq floating window: restored after shell surface=0x421850/Windows.UI.Core.CoreWindow:通知中心 nativeVisible=True managedVisible=True cloaked=0 rect=10,1516,786,1596 topMost=False foreground=0x20ADC/Chrome_WidgetWin_1:Codex
```

这里最关键的是：

```text
nativeVisible=True
managedVisible=True
cloaked=0
topMost=False
```

这说明窗口没有被隐藏、没有被 DWM cloak，也没有丢失矩形位置。窗口实际仍然存在，只是 Z 序被 Windows Shell 面板和任务栏重排压到了后面。

## 原机制

原浮窗机制更像“独立窗口 + 监听器拉回置顶”：

1. `CreateParams` 里根据配置添加 `WS_EX_TOPMOST`。
2. `ForceFloatingTopMost()` 调用 `SetWindowPos(HWND_TOPMOST)`。
3. `KeepFloatingTopMost()` 用定时器周期性重新压回 topmost。
4. `MonitorShellSurface()` 发现 Shell 面板关闭后调用 `RestoreFloatingTaskbarWindow()`。

问题是窗口虽然尝试置顶，但它不是任务栏窗口 `Shell_TrayWnd` 的 owner。Windows 打开开始菜单、搜索、通知中心、音量、WiFi、电池等面板后，Shell 自己会重排任务栏相关窗口的 Z 序。独立浮窗不在任务栏 owner 链里，就可能被 Shell 层级压到后面。

另一个关键不一致是 WinForms 托管层的 `TopMost` 仍可能是 `false`。也就是原生层尝试用 `WS_EX_TOPMOST` / `SetWindowPos(HWND_TOPMOST)` 置顶，但托管状态日志显示 `topMost=False`，两层状态不统一。

## 根因

根因不是监听不到关闭事件，也不是窗口被 Hide，而是浮窗的窗口层级关系不完整：

1. Monitor 浮窗是独立 topmost 工具窗。
2. 它没有 owner 到任务栏窗口。
3. Windows Shell 面板关闭后任务栏重排 Z 序时，浮窗不属于任务栏体系。
4. 恢复流程执行了，但只恢复可见性和 topmost 请求，没有恢复任务栏 owner 关系。
5. 焦点离开任务栏时，系统又触发一次 Z 序变化，浮窗才重新出现在前面。

## 修复策略

修复后的机制是“任务栏 owner 体系里的真实 topmost 工具窗”：

1. 浮窗创建时，WinForms `TopMost` 与 `taskbar_floating_topmost` 保持一致。
2. 浮窗绑定 owner 到当前 `Shell_TrayWnd`。
3. 每次应用窗口选项、应用浮窗位置、Shell 面板关闭恢复时，都确认 owner 仍然是当前任务栏。
4. 置顶恢复时同步托管 `TopMost`、原生 `WS_EX_TOPMOST` 和 `SetWindowPos(HWND_TOPMOST | SWP_SHOWWINDOW)`。
5. 日志增加 `nativeTopMost` 和 `owner`，后续可以直接判断是原生置顶失败还是 owner 绑定失败。

## 修改函数

主要修改文件：

```text
app/HimpqEnhanced/HimpqTaskbarWindow.cs
```

涉及函数和字段：

1. `HimpqTaskbarWindow()`
   - 构造阶段将 `TopMost` 设置为 `!IsFloatingMode || config.taskbar_floating_topmost == 1`。
   - 避免浮窗配置要求置顶，但 WinForms 托管层仍显示 `TopMost=false`。

2. `EnsureFloatingTaskbarOwner()`
   - 获取当前 `Shell_TrayWnd`。
   - 如果 owner 已经正确则直接返回。
   - 如果 owner 不正确，则调用 `SetWindowOwner()` 写入。
   - 写入后调用 `GetWindowOwner()` 读回确认。
   - 如果失败，写日志：`failed to set taskbar owner=...`。

3. `SetWindowOwner()` / `GetWindowOwner()`
   - 使用 `GWLP_HWNDPARENT` 设置和读取 owner。
   - 分别处理 64 位 `SetWindowLongPtr/GetWindowLongPtr` 和 32 位 `SetWindowLong/GetWindowLong`。

4. `ForceFloatingTopMost()`
   - 先调用 `EnsureFloatingTaskbarOwner()`。
   - 再调用 `EnsureFloatingExtendedStyles()`。
   - 同步 WinForms `TopMost=true`。
   - 最后调用 `SetWindowPos(HWND_TOPMOST, ..., SWP_SHOWWINDOW)`。

5. `ApplyWindowOptions()`
   - 浮窗模式下先确认 taskbar owner。
   - 同步 `TopMost = topMost`。
   - 再按配置执行 `HWND_TOPMOST` 或 `HWND_NOTOPMOST`。

6. `ApplyFloatingPosition()`
   - 每次设置浮窗坐标和尺寸前确认 taskbar owner。
   - 避免任务栏重建或重排后 owner 丢失。

7. `LogFloatingWindowState()` / `LogFloatingWindowRestore()`
   - 日志新增 `nativeTopMost`。
   - 日志新增 `owner`。
   - 用于区分托管层 `TopMost`、原生 `WS_EX_TOPMOST` 和 owner 关系是否一致。

## 修复后流程

Shell 面板打开和关闭时的恢复流程：

```text
MonitorShellSurface()
  -> TryDescribeActiveTaskbarShellSurface()
  -> 记录 shell surface active
  -> Shell 面板关闭
  -> RestoreAfterShellSurfaceClosed()
  -> RestoreFloatingTaskbarWindow()
  -> ApplyWindowOptions()
       -> EnsureFloatingTaskbarOwner()
       -> EnsureFloatingExtendedStyles()
       -> TopMost = true
       -> SetWindowPos(HWND_TOPMOST)
  -> ApplyPosition()
       -> ApplyFloatingPosition()
       -> EnsureFloatingTaskbarOwner()
       -> SetWindowPos(HWND_TOPMOST, x, y, w, h)
  -> ShowNoActivate()
  -> ForceFloatingTopMost()
       -> EnsureFloatingTaskbarOwner()
       -> TopMost = true
       -> SetWindowPos(HWND_TOPMOST | SWP_SHOWWINDOW)
  -> LogFloatingWindowRestore()
```

核心变化是：恢复不再只是“显示并请求置顶”，而是先确保窗口属于任务栏 owner 体系，再统一托管层和原生层的 topmost 状态。

## 原来和现在的区别

原来：

```text
独立浮窗
  + WS_EX_TOPMOST
  + SetWindowPos(HWND_TOPMOST)
  + topmost keeper 定时拉回
  - 没有任务栏 owner
  - WinForms TopMost 可能仍是 false
```

现在：

```text
任务栏 owner 下的浮窗
  + owner = Shell_TrayWnd
  + WinForms TopMost = true
  + WS_EX_TOPMOST
  + SetWindowPos(HWND_TOPMOST | SWP_SHOWWINDOW)
  + topmost keeper 继续作为状态保持
```

所以现在的表现更接近 MSI Afterburner / NVIDIA Overlay 这类真实 topmost 浮窗，而不是仅靠监听器反复把窗口拉回来。

## 验证

已执行：

```powershell
dotnet build app\GHelper.sln --no-restore
dotnet publish app\GHelper.csproj -c Release -r win-x64 --self-contained false -o publish
```

结果：

```text
0 errors
```

发布输出：

```text
publish/GHelper.exe
publish/GHelper.zip
```

未使用浏览器或 Playwright 调试。

## 后续排查日志标准

如果后续再次出现类似问题，看恢复日志里的这几个字段：

```text
nativeVisible=True
managedVisible=True
cloaked=0
topMost=True
nativeTopMost=True
owner=0x.../Shell_TrayWnd
```

判断方式：

1. `nativeVisible=False` 或 `managedVisible=False`：窗口显示状态异常。
2. `cloaked!=0`：DWM cloak 状态异常。
3. `topMost=False`：WinForms 托管层 TopMost 没同步。
4. `nativeTopMost=False`：原生 `WS_EX_TOPMOST` 没同步。
5. `owner` 不是 `Shell_TrayWnd`：任务栏 owner 关系丢失。

