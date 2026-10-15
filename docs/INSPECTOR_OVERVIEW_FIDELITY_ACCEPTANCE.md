# 检查器概览共享布局与原生对照验收

## 2026-10-12 实现与自检

- 概览卡片继续使用 `AppCard`、`AppText`、`AppActionButton` 和现有共享图标；
  没有新增另一套视觉组件，也没有改变授权、审批、命令、颜色或阴影。
- `Styles/CardInsetVariants.axaml` 的 `card-inset-inspector` 内边距由
  `20,16,10,16` 调整为 `20,10,10,13`，不在页面覆盖 Padding。
- `Styles/InspectorVariants.axaml` 拥有概览外层、标题/状态、来源标题和来源列表的
  四个共享布局变体。间距分别为 10、4、5 DIP，来源标题的上边距为 6 DIP。
  `InspectorPane` 只组合分组并保留业务绑定，不使用负 margin 光学校正。
- 第一轮校正标题/状态分组和顶部留白，第二轮校正列表节奏及底部留白；
  卡片仍随实际内容变化，不锁定内容高度，也不通过缩放截图制造一致性。

实现前五项自检：

1. 行为归 App 的 Views/Styles，测试归 App.Tests；模块依赖方向不变。
2. 复用现有检查器、附件、项目目录及会话命令，无需增加应用端口或工具。
3. 本次样式调整不执行模型或工具，也不改变超时、取消、输出限制或审批管线。
   原生宿主只有独立合成数据库和临时授权目录，不使用用户凭据或数据库。
4. 新手写 C# 文件只有一个类型、128 个物理行，最长方法声明 42 行，语法错误为 0。
   三个生产 AXAML 文件均未超过 500 行硬上限；布局原语保持纯结构用途。
5. 新增八项真实页面回归覆盖明暗主题、三种宽度、内容边界、来源命令和草稿保留，
   并检查没有当前会话时概览隐藏，不通过占位命令代替真实行为。

## 被动截图与测量

证据目录在仓库外：
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-main-dark-narrow-20261012-144117`。
临时文件不提交仓库，系统清理临时目录后这些证据可能不再存在。

- `capture_passive.py` 使用 PrintWindow，校验唯一目标进程/PID、稳定窗口边界和非黑像素。
  脚本不激活、移动、缩放或点击窗口；Codex 参考进程的实际名称是 `ChatGPT.exe`。
- 首次旧 ImageGrab 脚本因参考窗口不在前台而拒绝捕获；没有为截图自动操作 Codex，
  改用被动 PrintWindow 后取得 `codex-reference-current.png`。
- 三张测量原图均为 1920×1032，桌面 DPI 为 96。`overview-comparison.png`
  使用相同坐标 `(1600,96,1920,310)` 的原尺寸裁片，没有拉伸。
- `compare_overview.py` 在四个指定 ROI 内寻找暗像素行，判定条件为
  `max(R,G,B) < 185` 且每行至少三个像素满足条件；报告为 `overview-pixels.json`。
  查看全部的 ROI 从 y=252 开始，避免把默认目录文字误计为目标按钮。

| 指定 ROI | 坐标范围，右/下边界不包含 | Codex 首墨迹行 | 原始 MossAgent | 最终 MossAgent |
| --- | --- | --- | --- | --- |
| 项目标题 | `(1620,114,1730,145)` | 123 | 129 | 123 |
| 状态 | `(1654,148,1745,183)` | 152 | 164 | 152 |
| 来源标题 | `(1625,186,1680,221)` | 195 | 201 | 195 |
| 查看全部 | `(1654,252,1750,290)` | 256 | 267 | 256 |

这里一致的仅是指定阈值和 ROI 下的第一墨迹行，不是整张截图逐像素一致。
Codex 参考是对话/MCP 来源，MossAgent 是合成空对话/目录来源；业务内容、字体、
抗锯齿、图标和颜色仍有差异。图中蓝色指针光圈不属于应用阴影。

## 原生窗口与交互

- 最终真实 App 程序集 SHA256：
  `872BD4FF7CE04196BCA39800E6C6D0569AF8C758B80266D417A32E5134675854`。
  `host-final/MossAgent.App.dll` 与仓库 Debug Windows x64 构建的哈希相同。
- 最终普通权限宿主的 `dark-narrow-final.png` 为 980×640；请求的 560 高度被实际
  MinHeight=640 限幅，不能把这张图称为 980×560 验证。
  `dark-max-final.png` 和 `light-max-after.png` 分别是最终明暗主题 1920×1032 帧。
- 最终宿主实际点击“查看全部”进入来源检查器，收起检查器、最大化，并从实际设置页
  切换浅色主题后返回。来源检查器证据为 `dark-narrow-final-sources.png`。
- 早期/中间真实宿主另验证了更多菜单、Escape、完整视图进入/恢复、检查器及概览
  开关和未发送草稿。`dark-narrow-after-restored-draft-stable.png` 是稳定后的草稿帧；
  立即切换第一帧曾短暂未绘制文本，后续新观察与稳定截图确认恢复。
  这些交互帧不替代最终构建的几何截图，也不代表真实 agent 执行成功。
- 所有输入只作用于任务自建宿主，没有发送草稿、点击删除/归档或自动操作 Codex UI。
  初始 PID 23208、中间 PID 21472 和最终 PID 2836 均已退出，宿主日志记录 `QA_EXIT: 0`；
  最终进程消失还由 `final-qa-exit-check.log` 精准复核，不以滞后窗口绑定认定关闭。

## 红绿回归与最终验证

- 正确原始基线 `overview-red-final.trx`：八项中六项布局失败、两项行为通过。
  首次校正与既有标题栏/检查器回归合计 `overview-focused.trx` 为 67/67。
- 底部留白第二轮基线 `overview-footer-red.trx`：六项布局失败、两项行为通过，
  预期底部 Padding=13、实际为 8。最终 `overview-final-focused.trx` 为 67/67。
- 第二轮生产修改前的完整 App 回归 `app-full.trx` 为 3902/3902，Visual 为 8/8；
  这是中间结果，不替代最终源码的全量验证。
- 最终八个程序集顺序执行完成，各自的 `final.log` 和 `final.trx` 位于
  `final/<程序集>/`，汇总 `final-results.json`：5433/5433，无失败或未执行项。

| 最终测试程序集 | 通过/总数 |
| --- | --- |
| MossAgent.Agent.Tests | 112/112 |
| MossAgent.App.Tests | 3902/3902 |
| MossAgent.App.Visual.Tests | 8/8 |
| MossAgent.Tools.Tests | 483/483 |
| MossAgent.Platform.Windows.Tests | 137/137 |
| MossAgent.Browser.Tests | 76/76 |
| MossAgent.Infrastructure.Tests | 199/199 |
| MossAgent.Providers.Tests | 516/516 |

- `solution-final-build.log`：解决方案构建 0 警告、0 错误。
  新测试的 `dotnet format whitespace --verify-no-changes --no-restore` 退出码为 0；
  `source-audit.log` 的文件、单类型、方法硬上限检查通过。
  `diff-check-final.log` 记录本轮快照差异、新文件空白和 AXAML XML 检查。

## 夹具失败记录与验收限制

- 初期夹具引用不存在的 Workspace.Prompt，已改为真实 WorkspaceComposerView 的 AppInput。
- 曾 await Workspace.LoadAsync，Headless 运行没有所需消息泵，测试 PID 20740 仍存活。
  `overview-test-stacks.log` 和 `overview-test-async.log` 保留诊断；核验 PID 和路径后终止。
  该次运行不完整，`overview-red.log` 有 runner crashed -1，不能用作有效布局红基线。
  ClrMD 诊断包含 Metadata error，不被表述为完整准确的 awaiter 链。
- 夹具改为既有测试使用的 Catalog.ReloadAsync；显式 ShutdownAsync 后再 Close 的试验
  又产生五项字体创建错误和三项真实布局失败，共 0/8，通过记录见 `overview-red-catalog.log`。
  最终采用 window.Close 后由 DI async disposal 释放，与已有夹具一致，未修改生产加载逻辑。
  后续正确红绿和全量通过，不等于已查明或修复全局字体间歇问题。
- `overview-red.trx` 被后续八项失败运行覆盖，`overview-fixture-terminated.trx` 也误复制了
  该覆盖结果；两者都不能作为终止运行的完成计数证据。
- 本轮没有高 DPI 原生验收、公网真实提供商验收或全页面/全状态视觉验收。
  Debug 保持普通 JIT；Windows x64 单文件发布配置未改，没有恢复 AOT 或 JSON 源生成。
  未暂存、提交、推送或发布，完整产品与 Codex 一比一还原目标仍未完成。
