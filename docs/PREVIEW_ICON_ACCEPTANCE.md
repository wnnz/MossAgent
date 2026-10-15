# 文件预览工具栏图标验收

日期：2026-10-10/11。上一轮完整回归已结束，本轮继续修正运行窗口中仍存在的
视觉差异，不以三个图标的局部对照代表整体界面一比一还原。

## 差异与设计自检

- App 的共享 `AppIcon` 和 `FilePreviewVariants.axaml` 拥有绘制与光学尺寸。
  复用 `AppIconGeometryLayout` 的有限值检查、等比缩放和居中，不新增模块依赖。
- `AppIcon.Drawing` 支持多层矢量资源；有 Drawing 时优先绘制，空 Drawing 不绘制，
  清除后恢复原有 Geometry/Fill/Stroke。未选择绘图资源的其他图标保持原实现。
- 打开按钮仍执行现有 `filesystem.reveal_file` 管线，颜色图标不是一个新的打开
  实现。复制、缩放、刷新、审批、授权路径、超时、取消、输出与审计均不改变。
  没有新增文件读取、进程启动、模型请求、数据库迁移或外部编辑器选择功能。
- 五个涉及的手写 C# 文件经 Roslyn 检查：每文件一个类型，无文件/方法硬限失败。
  图标资源、大小和笔画均在共享样式中，不在业务视图内添加颜色或尺寸。
- 编译门禁原先把非 Control 的 DrawingGroup/GeometryDrawing 也当作视觉控件拒绝。
  现在仅允许它们作为 Styles.Resources、Application.Resources 或 ResourceDictionary
  内的媒体资源；在布局子项中仍拒绝它们，资源内的 Button 等原生控件仍拒绝。
  三个资源位置的正/负测试同时检查测试侧规则和真实 MSBuild XPath，未关闭门禁。
- 新的 Skia 像素测试使用独立 Headless 会话，验证真实黄色、蓝色和背景像素，
  负坐标源的等比居中、绘图优先级、空绘图与恢复 Geometry。没有付费 API 调用。

## 原像素对照

证据、Python 脚本、QA 宿主、独立合成 SQLite 和 TRX 均留在仓库外：
`%TEMP%/MossAgent-preview-toolbar-20261011`。

- `codex-current.png` 是对当前 Codex 的被动 Python 捕获，原始 1936×1048，含 DWM 外框。
  没有向 Codex/ChatGPT 注入输入或改变设置。
- `before-light.png` 和 `after-light.png` 来自生产 App/MainWindow，普通权限，
  浅色 1920×1032，render scale 1。同一合成图片通过真实授权预览工具加载。
  `toolbar-icon-comparison.png` 只裁取工具栏原像素并拼接，未缩放或对齐内部图标。
- 在固定亮度阈值 180 下，复制图标的墨迹包围框从 16×16 改为 14×14，参考为
  14×14；方直角改为两个圆角复制页面。图片选项从 14×16 改为 12×13，参考为
  12×13。源数据、阈值、坐标和实测结果保存在 `toolbar-icon-measurements.json`。
- 复制图标最初尝试 13 DIP，原生墨迹仍为 15×15；保留 `after-light-trial.png`
  及 `toolbar-icon-trial-measurements.json`。最终共享变体为 12 DIP、一 DIP 笔画。
  复制按钮仍为 32×32，图片选项仍为 34×34；缩小绘制不缩小点击区域。
- 打开按钮复用 14-DIP 图标槽，通过共享五层 Drawing 绘制黄色文件夹、蓝色标记
  和浅色方形底，不再用单色轮廓替代 Windows 资源管理器图标。该资源仅作用于
  打开按钮的前导图标；隐藏的尾部图标及其他按钮不受影响。

## 验证记录

- 最终定向测试 `focused/accepted-icons.trx`：85/85 通过，无跳过；保留此前四个
  新断言失败的 TRX。失败原因是 AppActionButton 还拥有一个隐藏尾部图标；最终
  按实际前导角色定位并另外断言尾部不使用 Drawing，不用 First 忽略重复。
- 初次 QA 宿主编译因 lambda 的 `_` 参数与弃元混淆而失败；改用具名参数。
  新测试编译阶段还修正了资源扩展 using、异步 dispatch 返回值与 PNG 编码器 API。
  未用产品运行时修改或关闭警告掩盖这些验收代码错误。
- 最终解决方案和 QA 宿主构建零警告、零错误；五个 C# 文件的定向 whitespace
  验证及 Roslyn 单类型/规模审计通过。
- 最终浅色窗口中实际点击图片选项、放大至 125%、再适应窗口。原图确实放大并
  出现滚动，再恢复适应布局；两标签和未发送合成草稿始终保留。证据为
  `light-zoom.png`、`light-fit.png` 及 `final-light.log` 的状态变化。
- 适应窗口后 `IsFit=true`，保留的 `Scale` 仍为 1.25；不能把布局恢复描述为
  缩放数值重置为 1。深色 980×640 窗口实际展开、恢复和关闭右栏，两标签及
  草稿保留，关闭后浮动卡恢复。证据为 `after-dark-narrow.png`、
  `after-dark-expanded.png`、`after-dark-restored.png`、`after-dark-closed.png`
  和 `final-dark.log`。这是 MossAgent 交互证据，不是深色 Codex 同状态验收。
- 首次观察及一次点击后的 accessibility 为空；未重复点击。重新观察取得控件树，
  或从当前返回的截图及截图 ID 执行下一步输入。独立 popup 也由 Computer Use
  观察，不能仅凭主窗口 PNG 推断弹框行为。
- 最终完整回归实际结束，八程序集 4,423/4,423 通过，无失败或跳过：Agent 63、
  App 3292、Visual 8、Tools 316、Windows 137、Browser 76、Infrastructure 165、
  Providers 366。原始 TRX 在 `regression` 下；并非根据局部测试推断全量通过。

## 被动截图守卫与收尾

- 仓库外 `mossagent_window_capture.py` 实际使用 ImageGrab 屏幕裁片，不是
  PrintWindow。最初 `dark-occluded-invalid.png` 捕获了遮挡目标的屏幕内容，
  明确无效，不能作为深色 QA 证据。激活独立 QA 后重拍的深色图片有效。
- 脚本现在捕获前后均验证目标可见、非最小化，前台根 owner 属于目标（允许
  目标所属 popup），且窗口 bounds 未变化。`--passive` 不主动激活任何窗口。
  该守卫不宣称排除所有透明或 topmost 遮挡，也不把屏幕裁片当成离屏窗口捕获。
- Python 语法检查和 `capture_guard_tests.py` 的七个确定性用例通过：目标与
  所属 popup 成功、外部前台/隐藏/最小化拒绝、被动模式不激活及移动不保存。
- 两个独立 QA 宿主复现遮挡：目标首次实际在前台，得到
  `guard-foreground-native.png`，这是成功而不是拒绝。第二个宿主遮挡目标后，
  对目标 PID 的被动捕获返回 exit 1，记录 `guard-rejected-native.log`，拒绝图片
  不存在。只激活独立 QA 目标后重新捕获 exit 0，`guard-recovered-native.png`
  经检查为真实深色 980×640 QA 内容。未向 Codex/ChatGPT 注入输入。
- 全部六个 QA 宿主均正常终止，exit 0、stderr 为空；最后两个通过各自合成
  数据目录的 `close.signal` 关闭，实际等待进程返回后确认没有遗留 QA 进程。
  用户数据、截图脚本和临时验收工程均不纳入提交；没有提交、推送或发布。

## 剩余范围

本轮没有打开真实外部编辑器、使用用户数据库或调用真实远端模型。其他工具栏
动作的 Codex 语义、打开菜单的编辑器选择、不同 DPI/字体及深色同状态参考仍需
继续核对。右端刷新按钮仍明确执行刷新，未只把图标换成未知语义的参考按钮。
包围框尺寸相同不代表抗锯齿、轮廓和整个工具栏像素相同。整体产品目标仍进行中。
