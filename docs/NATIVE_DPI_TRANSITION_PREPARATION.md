# 原生 DPI 切换诊断准备

## 本轮进展与边界

上一轮 `GLOBAL_APPROVAL_PRESENTATION_ACCEPTANCE.md` 完成了生产呈现修改和回归。
本轮继续原生尺寸错配诊断，不把更多健康的 96-DPI 恢复窗口当作修复。
没有改动产品窗口、布局、渲染、依赖、工具、安全政策、JSON 或发布实现。

外部证据位于 `%TEMP%\MossAgent-dpi-transition-20261014`。
生产 App 为上轮已验证的 Debug/JIT 程序，SHA-256：
`E5C485EC9234AB06F546389E88F1CD92FC54F52A324B12492783C7A7A61BB841`。

## 源码与待证假设

- 当前 Avalonia.Win32 12.1.3 的 NuGet 清单确认提交为
  `8eeda4f6f546165b3f72e63c9f42247abb306905`，与保存的源树一致。
  `WindowImpl.AppWndProc.cs` 的 blob 为
  `63ee3dbd7c8cbfb809d54927beeb0a5442f51448`。
- `WM_DPICHANGED` 更新缩放、调用 `ScalingChanged`，随后 `SetWindowPos`；
  `WM_SIZE` 才把新的逻辑尺寸交给 `Resized`。TopLevel 的缩放处理本身不更新
  缓存的 `ClientSize`。这些是当前框架源码事实，不是已捕获的失败事件历史。
- 待验证的具体路径：缩放变更时建议的物理矩形是否已经等于现有矩形，导致后续
  没有新的尺寸通知，逻辑客户区与原生客户区长期不一致。必须取得真实 DPI 消息、
  建议矩形、后续 resize/scaling/bounds 顺序及失败状态快照，不能靠 Headless
  模拟、历史错配图片或缺少同状态 Codex 图片直接认定原因。
- 未升级包，未复制框架补丁，未在产品中触发 resize 回调、强制 Width/Height、
  `UpdateLayout` 或 `InvalidateMeasure`。

## 只读诊断工具

1. 外部启动 hook 只绑定真实 MainWindow，记录 WM_SIZE、WM_DPICHANGED、
   WM_DISPLAYCHANGE 和拖动尺寸消息，以及 ClientSize、Bounds、状态、缩放和 resize。
   新增建议矩形和原生矩形，以区分同物理尺寸路径。最多 2,048 条，正常关闭后输出。
2. hook 不设置 `handled`、不改变 native result、不额外排队 UI 工作，不调整窗口。
   事件与回调在关闭时解除。它从 WindowOpened 开始，不覆盖创建前消息，仍可能影响
   时序，不能把有追踪的成功运行推广为无追踪的故障修复。
3. 独立 CLR 快照只取选中进程第一个观测到的 MainWindow 及其直接拥有的 WindowImpl，
   只读明确白名单的几何、有效标记和状态值。最多扫描两百万对象或二十秒，找到后
   即停止，不声称全堆唯一，不读取聊天、配置、凭据或字符串值，不保存 dump 文件。
4. Python 验证器绑定快照 HWND、PID 和捕获元数据；核对当前原生尺寸与 DPI 后，
   比较缓存 ClientSize、Bounds、arrange bounds、布局有效性和缩放。失效窗口、
   尺寸变化、缺少接受的图像、非白名单快照均拒绝，不用黑屏检测代替布局一致性。
5. Python 截图、诊断源文件、快照与合成 fixture 都在仓库外。生产 App 未改。

## 已取得的起始证据

- 独立生产实例 PID15400、HWND7669100，普通权限、最大化、96 DPI、1920×1032。
  `baseline-clean.png/json` 是不激活窗口的 PrintWindow 原始帧。
- 首次 screen 图有 Codex 系统通知遮挡，不当作干净像素参考；随后 PrintWindow 帧
  没有这个遮挡，标题栏、主区和输入框均可见。没有自动操作或关闭 Codex 通知。
- `managed-baseline-complete.json` 与 `baseline-geometry-verified.json` 确认原生客户区、
  缓存 ClientSize、Bounds、arrange bounds 都为 1920×1032；渲染缩放 1、DPI96，
  measure/arrange 有效。此次快照在扫描 106,975 个对象后找到目标；这不是全堆统计。
- 独立 fixture 起始审计确认 19 个表的结构和内容、3 个历史 artifact、4-request
  历史以及浅色 UI 配置均未变化。没有执行模型、浏览器、终端或审批设置操作。
- 外部 hook 与快照程序构建均为 0 警告、0 错误。

已经询问用户能否手动临时切换系统缩放至 125%，并提示测试窗口已准备好。
助手不自动更改系统显示设置。真实 DPI 切换尚未取得；当前健康基线不修复旧故障。
本轮启动会话和 PID 必须重新检查，不能因保存的 launch 文件推断进程仍存活，
也不能在进程仍活着时复用 fixture 或重启。

原生尺寸错配根因、125%/100% 切换、同状态 Codex 外观/PI-Desktop 参考和完整产品
验收仍未完成。没有暂存、提交、推送或发布，完整 goal 保持 active。

## 本次实例终态

后续继续工作时重新核实了 PID15400/HWND7669100 和生产程序集身份；原生 DPI
仍为 96。`continued-geometry-verified.json` 再次确认布局有效且原生/缓存/arrange
尺寸一致。这是健康状态核验，不是旧故障修复。

原生标题栏正常关闭后启动会话取得 App/observer 两个零退出，进程和监听端口均
消失。observer 自身有 1,200 秒生存上限，关闭前已结束，未重启或复用该 fixture。
`final-closed.audit.json` 确认十九个表、三个原 artifact、四次历史请求及 UI 字节
未变化。没有模型请求、工具执行或审批政策/决策操作。

`native-dpi.jsonl` 有十四条记录、三个 `WM_SIZE`/resize 配对，未到输出上限。
没有 `WM_DPICHANGED` 或 scaling 事件，不能建立 125%→100% 故障的真实历史。
新增外部 `analyze_dpi_trace.py` 使用每条记录的缩放做物理尺寸比较，拒绝截断、
非法缩放和乱序；有效 bounds 不匹配仅标为需独立稳定快照的候选，不认定根因。
五项分析器测试通过，其中高 DPI 与旧尺寸示例是合成输入，不是原生转换验收。

后续共享侧栏标题与箭头修正单独记录在 `SIDEBAR_BRAND_FIDELITY_ACCEPTANCE.md`，
不改变窗口尺寸、原生通知、渲染、依赖或 DPI 处理。真实 DPI 故障仍保持开放。
