# 工作区页面导航与检查器空状态验收

## 范围与设计自检

- 本轮使用真实 `MossAgent.App.exe`、独立 `MOSSAGENT_DATA_DIR` 和合成数据，
  不读取日常数据库或复制凭据。Python 截图脚本与种子程序保留在仓库外。
- 行为归属 App：目录导航发出明确的对话导航意图，页面协调器决定展示对话
  或定时任务。后台目录刷新、标题更新不是用户导航，不应抢走当前页面。
- 复用现有目录命令、仓库端口和历史记录；不引入数据库迁移、文件操作、
  provider 请求或工具执行旁路。已有锁定状态和未知项目拒绝逻辑保持有效。
- 将历史命令提取为独立 `WorkspaceNavigationController`，防止目录模型突破
  500 行硬限制。每个手写 C# 文件一个类型，订阅随页面协调器释放。
- 共享 `AppEmptyState` 新增 `compact` 外观变体，用于 120 DIP 高的产物区域；
  不在页面中覆盖尺寸、字体或颜色。

## 实际发现

- 在定时任务页点击侧栏里当前已选中的对话，页面不会返回对话。
- 侧栏“新对话”、切换到没有任务的项目、后退到空草稿也可能停留在定时任务页。
- 后台对话标题更新或目录重新加载反而可能把定时任务页关闭。
- 原生右侧审计面板中，“暂无生成产物”的说明被固定高度区域裁切。
- 修复前新增回归用例 21 项中 11 项失败，覆盖真实侧栏共享按钮、后台更新、
  无任务导航以及窄窗口内的文字几何；其余拒绝/释放边界用例通过。

## 验证方式

- `WorkspacePageNavigationTests` 覆盖实际 MainWindow 绑定、重复选中、空草稿、
  项目导航、后退、后台刷新、锁定、无效目标与释放订阅。
- `WorkspaceNavigationControllerTests` 覆盖前进后退、可用性通知、锁定后直接
  执行拒绝、历史不被消耗和已移除任务的剪枝。
- `InspectorArtifactEmptyLayoutTests` 在 980×640、1280×820、1920×1032 的
  明暗主题下逐个检查文字的实际边界，而非仅检查控件的 IsVisible。
- 所有交互用例断言没有向本地合成 provider 发出模型请求。

## 证据与限制

本轮外部证据目录：`%TEMP%\MossAgent-workspace-review-20261009-01`。
普通权限的真实产品程序已复测：定时任务返回当前对话、侧栏新对话、审计
面板、窗口还原、实际拖动到 980×640、菜单切换深色和最大化。截图包含
1920×1032 的明暗主题、1280×820 浅色以及 980×640 的明暗主题。右侧产物
说明均完整显示。交互记录保留在 `native-interactions.json`，两个本轮实例
均正常关闭，stderr 为空。没有执行定时计划或发送模型请求。

对比图：`artifact-empty-before-after.png` 使用原始像素裁切显示本轮修复；
`codex-mossagent-current-layout.png` 为当前两程序的等尺寸全页布局对照。
当前 Codex 使用被动 PrintWindow 获取参考图；没有自动化操作 Codex 界面。
Codex 参考图与 MossAgent 的会话内容和已打开面板不同，不能据此宣称整页
逐像素一致。定时任务页面与编辑弹窗仍需要对应的当前 Codex 参考截图。
本轮不是付费 provider、长时间任务或完整产品目标的最终验收。

## 检查记录

- 定向回归 73/73 通过；解决方案构建 0 警告、0 错误。
- 六个相关 C# 文件的格式检查、九个源码/AXAML 文件的 Roslyn/XML 拆分检查
  和工作区 `git diff --check` 均通过；没有暂存、提交或推送。
- 首轮全量 3684 项中 3683 项通过，一项旧预览交互测试在
  `HeadlessUnitTestSession.DisposeAsync` 抛出 NullReferenceException。
  原日志和 TRX 保留在 `full`；预览交互类单独复跑 6/6 通过，没有修改该测试。
  全量再次运行的结果单独保留在 `full-retry`，不覆盖首次失败证据。
- 全量第二次运行 3684/3684 通过，7 个 TRX，零失败、零跳过；App 2801、
  Windows 129、Providers 283、Tools 188、Browser 76、Infrastructure 144、
  Agent 63。汇总保留在 `full-retry-summary.csv`。一次清理异常不被宣称已修复。
