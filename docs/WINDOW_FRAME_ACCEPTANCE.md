# 共享工作区外框验收

## 2026-10-06 实现与约束

- 生产 App/MainWindow 的侧边栏、对话和检查器统一置于现有 `AppBorder`，
  使用 `WindowVariants.axaml` 的 `window-content-frame`，不在页面定义局部外观。
  外框统一 12-DIP 圆角、右侧和底部 4-DIP 留白、内容裁剪及 SharedSurfaceShadow。
- 导航轨、标题栏、原生浏览器常驻宿主、审批遮罩和窗口缩放命中区留在外框之外，
  不把原生缩放交互裁掉，也不重新创建浏览器会话。
- 修复侧边栏的 DataContext 边界：目录对象不拥有 IsLeftPanelOpen，展开状态显式
  绑定窗口 ViewModel。原生点击修复前仍显示侧栏，修复后释放 264-DIP 侧栏空间。
- 行为和样式由 App 所有，不改变跨模块依赖、工具管线、授权、审批、取消、输出限制
  或持久化结构；继续使用既有控件和窗口命令，不新增运行时端口。
- 外框布局 6 项及标记 1 项新增回归；更新现有壳布局测试以表达外框内外两层网格，
  不削弱原生命中区或功能绑定断言。新增 C# 均为一个类型，文件和方法未超过硬限制。

## 自动验证

- 外框及相邻组件专项 114 项通过，壳布局专项 13 项通过。
- 最终完整解决方案 1,644 项通过；解决方案构建 0 警告、0 错误。
- 本轮相关 C# 空白格式及差异空白检查通过。测试覆盖 980×640、1280×820、
  1920×1032，明暗主题、收起侧栏和展开检查器，断言外框实际边界和内容空间。
- 现有原生组件禁用、实例外观禁用、共享阴影及浏览器承载回归保持通过。
- 全仓库审计 827 个 C#/AXAML 文件：文件体积和单类型约束无失败。
  另发现两个既有测试方法各 86 行，超过 80 行限制：ModelSettingsViewModelTests:154
  和 SubagentWorkspaceFlowTests:21；均不在本轮外框修改范围，未改动，不声称全仓库方法约束通过。
  后续流式安全验收已拆分这两处测试并保留全部断言；最新全仓库方法审计无失败，
  见 PROVIDER_STREAM_LIMIT_ACCEPTANCE.md。

## 原生截图与交互

- 仓库外普通权限验收宿主加载生产 App/MainWindow，使用独立临时数据库、
  项目和 loopback 合成模型服务。未修改真实项目、凭据或生产配置。
- Python 只被动截取实际窗口像素，无拉伸。当前 Codex 参考的 1936×1048 原生窗口
  只裁去四周各 8 像素的外框，得到 1920×1032 有效内容；不操作 Codex 原生界面。
- 对照右上、右下、左下角落：修改前工作区铺到窗口边缘，修改后具备完整圆角和
  右侧、底部留白。浅色标题栏/导航轨采样为 #EEF4F9，侧边栏为 #F9FBFD。
- 实际点击验证侧栏收起/展开、工具结果展开、检查器收起及项目上下文卡片恢复。
  普通窗口还原至 1280×820，深色恢复后最大化并收起侧栏，布局保持正确。
- 合成成功场景经过真实输入发送，完成一次 filesystem.read_file 和最终回复。
  临时数据库确认 Completed、4 条消息和唯一成功 chosen-read 审计；合成文件未变。
  成功宿主最终因其 7 分钟验收期限结束，日志保留 TaskCanceledException；
  这不作为正常关闭证据，也不归因于生产 UI。原生视图菜单输入时宿主已退出，
  重新枚举确认窗口不存在后停止输入，没有使用失效索引重复点击。
- 同一数据集以 --reopen 恢复已完成会话，模型请求为零，正常关闭日志无失败标记、
  退出码为零；没有再次发送或自动重放。兼容接口详情见 COMPATIBLE_STREAM_ACCEPTANCE.md。

图片、对照脚本、日志和宿主保留在：
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-window-fidelity-20261006-1125`。

- `window-frame-corner-comparison.png`：Codex、修改前、修改后原始像素裁片。
- `sidebar-collapse-before-fix.png`、`sidebar-collapse-fixed.png`：绑定问题修复对照。
- `native-tool-expanded.png`、`native-context-restored.png`：工具与检查器交互。
- `native-normal-light.png`、`native-reopened-normal-dark.png`：1280×820 明暗主题。
- `native-reopened-maximized-dark.png`、`native-reopened-dark-collapsed.png`：深色最大化。

## 未完成范围

参考与验收窗口的正文和检查器内容不同，只核对公共外框及指定交互，不声称整页像素相同。
所有 DPI、窗口边缘拖动、悬停状态、设置页及全部图标仍未穷尽；新对话图标等几何仍有差异。
本轮不修复 Codex 截图工具，不将封装、构建通过或局部圆角修正等同于全产品目标完成。
没有暂存、提交或推送；截图和 Python 脚本不进入仓库。
