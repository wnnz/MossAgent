# 共享数字输入组件验收

## 2026-10-07 归属与边界

1. 行为和外观归 App 的 `Controls` 与共享 `Styles`，不新增基础设施/UI 反向依赖。
2. 复用 `AppNumericUpDown` 与 Avalonia 原有数值处理、RepeatButton 和输入事件，
   不添加工具或运行时端口；`AppRepeatButton` 只封装连按按钮。
3. 本次没有文件、网络、命令、审批或持久化行为变更；测试不发起真实模型请求。
4. 新增类各自独立文件，共享主题与测试均低于文件/方法硬限制，最终规模审计另行记录。
5. 回归验证明暗主题、左右按钮、禁用、只读、范围边界、隐藏/恢复、主题切换、
   对齐和无障碍名称；C#/AXAML 禁止独立外观覆盖，同时保留合法功能属性。

## 原因与实现

- 旧包装保留 Fluent 的数字输入模板，内部两枚箭头仍为原生 PathIcon，
  图形 16×8、按钮宽 34 DIP，独立分隔边框使端口字段的外观与共享输入不一致。
- `NumericFieldVariants.axaml` 统一内部表面、两枚 `AppRepeatButton` 和 `AppIcon`：
  图标槽 10×10 DIP、按钮宽 22 DIP、字段高 32 DIP，点击区域不按图形墨迹缩小。
  图标与按钮中心一致，表面圆角裁剪，无独立分隔线，明暗/悬停/按下/焦点/禁用
  使用共享主题颜色。数值解析、限制和连按逻辑仍由框架处理，不改写业务数值。
- 增大/减小使用本地化可访问名称与提示；实际 UIA 返回名称而非 `AppIcon` 类型名。
- C# 已按 Button 继承链覆盖连按按钮；新增 `ValidateSharedRepeatButtons` 补齐 AXAML
  的尺寸、Padding 和内容对齐门禁，包括原生所有者属性元素。共享 Styles 可以配置，
  业务页只提供功能属性。没有放宽原生视觉元素白名单。

## 失败基线与自动化

- `numeric-before.trx`：新增 12 项中 8 失败、4 通过；真实失败包括 34-DIP 宽度
  与隐藏按钮后仍占 68 DIP，功能边界原先已经通过。
- `numeric-boundary-before.trx`：新门禁查询尚不存在时 10 失败、C# 既有拒绝 3 通过；
  这些是门禁注册检查失败，不能当作 10 个真实编译绕过。
- `components-proxy-focused-final.trx`：组件/邻接样式及代理修改合计 73 项通过，
  包含新增 12 项数值组件、13 项门禁与 21 项代理修改回归。
- 最初共享模板使用未允许的 DockPanel，被现有门禁拒绝；随后试图用 Setter 修改
  CLR ColumnDefinitions 被 AXAML 编译拒绝。最终改为允许的固定三列 Grid 与附加列位置，
  不添加例外或通过业务代码替换列集合。
- 完整解决方案构建、七项目测试、格式和规模审计结果汇总在 `UI_COMPONENT_ACCEPTANCE.md`。

## 普通权限原生验收

- 生产 App/MainWindow/组件、隔离 SQLite，浅色 1280×820、深色 980×820。
  实际点击增大将端口 7890 改为 7891，重启后恢复；深色减小到 7890 后取消，
  列表和只读数据库仍为 7891。没有用代码直接设状态替代这两次鼠标动作。
- Python 被动主窗口截图，同坐标原始端口裁片没有拉伸：
  `numeric-native-comparison.png` 实测箭头墨迹由 16×8 变为 10×6 像素。
  槽位 10×10 是布局度量，不能与墨迹尺寸混为一谈。
- 原始帧：`proxy-editor-validation-before-light.png`、
  `proxy-editor-validation-after-light.png`、`proxy-editor-restarted-dark.png`。
  当前 Codex 被动参考为 `codex-reference-final.png`；两应用的内容、窗口尺寸和检查器
  状态不同，不能据此计算或声称全页逐像素一致。
- 证据位于 `%TEMP%/MossAgent-ui-fidelity-qa-20261007-02/`。
  Python、临时探针和宿主均不入仓库。多 DPI、长按重复计时、键盘实体输入所有分支
  和所有页面仍未完成原生视觉验收；封装与门禁不等于整个 Codex 还原目标完成。
