# 统一 UI 组件

所有业务界面使用 `MossAgent.App.Controls`。组件负责视觉和交互的一致性，
页面只负责数据、命令与结构布局。不要只给原生组件换一个名字后在各页面独立调样式。

## 最短使用规则

- 按钮、图标、文字、输入、列表、卡片和浮层只使用下表的共享组件。
- 同类图文行、导航行、设置行和输入区优先使用组合组件，页面不重复拼装内部结构。
- 页面只绑定数据、命令和状态，通过已有 `Classes` 选择共享语义变体。
- 图标尺寸、图文对齐、字体、点击区域、圆角、阴影及交互外观统一修改共享样式。
- `Grid`、`StackPanel`、`WrapPanel`、`Panel`、`ContentPresenter` 仅作结构布局，
  不能用来定义页面专属颜色、边框、阴影或视觉模板。
- 新外观先补共享变体，新组件放在 `Controls`；业务代码直接使用原生视觉控件、
  派生页面专用视觉组件或覆盖受保护的外观，均由 C#/AXAML 构建门禁拒绝。
- `MOSSUI001` 同样拒绝把原生视觉控件传给带 `new()` 约束的泛型工厂，
  包括工厂实例、方法调用和方法组委托，不能以 `Create<Button>()` 绕过封装。
  共享组件、纯结构布局以及没有构造约束的集合和功能泛型仍可使用。

## 组件目录

设置标题统一使用 `settings-title`，导航使用 `AppSettingsNavButton`。
图标槽、描边、图文间距、选中行和搜索字段表面由共享设置样式管理，
不要在页面或代码后置覆盖子图标 Margin；`AppIconLabel` 通过 `Spacing` 拥有该间距。
设置导航与侧边栏操作使用共享 14-DIP 字号和 18-DIP 行高，避免默认行高导致图文中心偏移。
分组标题复用 `sidebar-section-heading` 与独立 `SidebarSectionTextBrush`，
不能为一个分组覆盖整个主题的弱文字颜色；实测及限制见 [SHARED_NAVIGATION_TYPOGRAPHY_ACCEPTANCE.md](archive/UI_COMPONENTS.md#shared-navigation-typography-acceptance)。
任务文字使用共享 14-DIP 字号与 18-DIP 行高；`AppText.FadeOverflow` 默认关闭，
任务变体只在文字实际溢出时通过 `:overflowing` 提供渐隐，不淡化完整短标题。
本次文字颜色、原图指标、字体实验及剩余差异见 [SIDEBAR_TASK_TEXT_ACCEPTANCE.md](archive/UI_WORKFLOWS.md#sidebar-task-text-acceptance)。
最新设置页原图测量、明暗和最小窗口交互范围见
[SETTINGS_SHARED_APPEARANCE_ACCEPTANCE.md](archive/UI_WORKFLOWS.md#settings-shared-appearance-acceptance)；历史局部外观核验不能替代当前工作流验收。

| 用途 | 组件 |
| --- | --- |
| 普通按钮 | `AppButton` |
| 纯图标按钮 | `AppIconButton` |
| 图标、文字和可选徽标的操作 | `AppActionButton` |
| 模型快速菜单、搜索列表与思考强度页 | `AppModelPickerMenu` |
| 输入区权限策略与菜单 | `AppApprovalPolicyButton` |
| 空会话项目欢迎标题 | `AppProjectPrompt` |
| 输入区项目、分支与代理上下文条 | `AppComposerContextBar` |
| 对话标题、运行状态与右侧操作 | `AppConversationHeader` |
| 设置导航 | `AppSettingsNavButton` |
| 提供商分组头、模型设置行 | `AppProviderSettingsHeader`、`AppModelSettingsRow` |
| 代理、MCP、自定义代理、子代理列表行 | `AppSettingsItemRow` |
| 窗口编辑菜单 | `AppEditMenu` |
| 工具执行记录行 | `AppToolActivityRow` |
| 任务失败提示 | `AppTaskFailureNotice` |
| 置顶、项目内和最近会话行 | `AppTaskNavigationRow` |
| 侧边栏项目标题、独立展开操作和摘要入口 | `AppProjectNavigationRow` |
| 侧边栏搜索入口、输入、清除及空结果提示 | `AppSidebarSearch` |
| 左侧导航轨、更多菜单和帮助入口 | `AppNavigationRail` |
| 图标与文字组合 | `AppIconLabel` |
| 输入区和消息附件 | `AppAttachmentChip` |
| 图标 | `AppIcon` |
| 文本、可选中文本 | `AppText`、`AppSelectableText` |
| Markdown、代码 | `MarkdownSelectableTextBlock`、`SyntaxHighlightedSelectableTextBlock` |
| 主对话与子代理的结构化回复、表格 | `AppResponseContent`、`AppResponseTable` |
| 输入、数字输入 | `AppInput`、`AppNumericUpDown` |
| 数字输入内部的连按按钮 | `AppRepeatButton` |
| 下拉、列表 | `AppComboBox`、`AppListBox` |
| 复选、切换、滑块 | `AppCheckBox`、`AppToggleButton`、`AppSlider` |
| 卡片、结构表面、浮层 | `AppCard`、`AppBorder`、`AppFlyout` |
| 设置编辑弹窗 | `AppSettingsDialog` |
| 滚动、列表呈现、内容宿主 | `AppScrollViewer`、`AppSelectionScrollViewer`、`AppItemsControl`、`AppContentControl` |
| 缩放、颜色预览 | `AppViewbox`、`AppColorSwatch` |
| 外观设置 | `AppThemePreview`、`AppDiffPreview`、`AppThemePaletteCard`、`AppThemeExtendedFontsCard` |
| 嵌入式浏览器 | `AppEmbeddedBrowserView`、`AppBrowserHost`、`AppBrowserViewport`、`AppBrowserToolbar` |
| 页面与窗口宿主 | `AppUserControl`、`AppWindow` |
| 主页面与设置页切换 | `AppPageSwitcher` |
| 对话与检查器布局、可调整分隔条 | `AppWorkspacePanes`、`AppPaneSplitter` |

模型菜单统一使用 `AppModelPickerMenu`，只继承或绑定现有 `ModelPickerViewModel`；
`ModelPickerFlyoutView` 不再自行拼装菜单。组件保留搜索、提供商分组、选中标记、
模型命令和共享思考滑块的原有绑定及外观，内部命名只用于管理键盘入口。
进入列表优先聚焦搜索，搜索隐藏时聚焦已选模型；快速菜单聚焦模型导航。
自动转移是程序化焦点，不伪造方向键导航而显示粗焦点框；实际 Tab 焦点提示仍保留。
当前 picker 关闭、组件卸载或 DataContext 替换会使迟到调度失效；隐藏、禁用及其
祖先不可用时不能抢回草稿焦点。订阅与组件附着生命周期一致。
`ValidateSharedWorkspacePrompts` 和 `MOSSUI002` 拒绝替换 Content、DataTemplates、
Padding、尺寸和内容对齐，包含原生属性所有者限定、属性元素及隐式子内容。
实窗检索、选择、Escape 与非黑屏原图的范围见 [MODEL_SELECTION_ACCEPTANCE.md](archive/PROVIDERS.md#model-selection-acceptance)。

主回复和子代理回复统一使用 `AppResponseContent`，只绑定解析后的 `Blocks` 和已有
`CopyCodeCommand`；子代理选择共享 `compact` 变体。标题、列表、引用、代码和表格
使用同一套内部模板，不在页面另建回复模板。代码复制传递完整原始代码；缺失命令时
禁用按钮，命令的 CanExecute 仍生效，保留键盘操作及本地化读屏名称。
`AppResponseTable` 继承表格数据上下文，列宽和单元格间距只在共享样式维护。
长代码和宽表格在自身有限宽度内滚动，代码滚动区最多高 360 DIP，不撑宽工具详情。
工具输出、任务和状态内容仍为原始文本，不把 Markdown 标记误解释为模型回复。
派生的展示分块不写入持久化载荷；流式更新、完成和重开恢复均用同一组件。
`ValidateSharedResponseContent` 与 `MOSSUI002` 拒绝替换 Content、DataTemplates、
内边距、尺寸及内容对齐，包含原生属性所有者限定、属性元素和隐式子内容写法。
实际模型链路、复制与截图范围见 [SUBAGENT_EXECUTION_ACCEPTANCE.md](archive/AGENTS_AUTOMATIONS.md#subagent-execution-acceptance) 的 2026-10-07 记录。

普通正文和列表统一为 14-DIP 字号、23-DIP 行高；段落间距只在共享文本块根节点设置，
列表不重复加段落间距，列表缩进和前缀基线由 `ResponseContentVariants.axaml` 维护。
行内代码使用可选择的原生 Run，代码字体、字号和主题背景只由共享样式设置。
背景按原生 TextLayout 的 UTF-16 范围逐行绘制圆角，先于原生选区和文字；
不再给 `Run.Background` 赋值，以免灰底覆盖蓝色选区。垂直内边距为 2 DIP、圆角为 3 DIP，
不改变文字测量或复制内容。所有 `AppSelectableText` 派生控件显式复用原生选择主题，
不改变 StyleKey，也不复制主题或硬编码系统选区颜色。
`CodeFontFamily`、`CodeFontSize`、`CodeBackground`、`CodeBackgroundPadding`、
`CodeCornerRadius`、`SelectionBrush`、`SelectionForegroundBrush` 均受 AXAML 和 `MOSSUI002` 门禁保护，
页面不能直接赋值、绑定或重置这些外观属性。样式变化不重新构造整段文本或丢失选择。
正文原始像素对照、窗口重排和剩余差异见 [RESPONSE_CONTENT_FIDELITY_ACCEPTANCE.md](archive/UI_WORKFLOWS.md#response-content-fidelity-acceptance)。

数字输入统一通过 `AppNumericUpDown`，不要在页面拼装文本框和加减按钮。
`NumericFieldVariants.axaml` 拥有内部箭头、22-DIP 连按按钮、32-DIP 字段、共享状态
颜色与数值动作读屏名称；框架数值解析和范围处理保持不变。`AppRepeatButton` 是其
内部共享按钮，不通过页面独立设置宽高、Padding、对齐或 Theme。
`ValidateSharedRepeatButtons` 与 `MOSSUI002` 同时阻止 AXAML/C# 外观覆盖，
包括 `Button.Width` 等原生所有者属性元素；不添加原生控件例外。
原始截图墨迹测量、主题/边界回归与原生加减/取消证据见
[NUMERIC_FIELD_COMPONENT_ACCEPTANCE.md](archive/UI_COMPONENTS.md#numeric-field-component-acceptance)；组件封装通过后仍需验证实际绑定和交互。

侧边栏项目标题统一使用 `AppProjectNavigationRow`。页面只传 `Title`、选择和展开状态、
`ActionTarget`、现有选择/展开命令、`SummaryContent` 与双向 `IsSummaryOpen`，不重新
拼装文件夹图标和展开按钮。共享组件拥有有限宽度标签、完整工具提示、独立命令目标、
展开/收起文件夹状态及摘要浮层；浮层明确继承业务 DataContext，换目标、上下文、
命令或内容、行或祖先不可用、命令拒绝及卸载时关闭，并释放拖动所有权。
祖先和按钮可用性订阅在卸载时全部解除，重新附着时建立当前树的订阅。
缺失命令或目标时操作不可用，命令自己的 CanExecute 仍然生效。
新对话和展开箭头使用 12-DIP 图标槽、1-DIP 描边。项目文件夹采用专用几何资源，
闭合/展开槽分别为 11/13 DIP，图文间距分别为 13/11 DIP，合计固定 24 DIP，
保持文字起点不变。共享样式拥有光学偏移，不改全局文件夹资源、不在页面移动图标。
96 DPI 原图中闭合墨迹为 12×12、展开为 14×12；槽尺寸不等同于实际墨迹尺寸。
`ValidateSharedProjectNavigationRows` 与 `MOSSUI002` 禁止覆盖 Content、Padding、尺寸
及内容对齐，普通属性、属性元素和原生属性所有者限定写法同样受约束。
明暗主题、真实页面命令及原始尺寸截图证据见 [PROJECT_ROW_COMPONENT_ACCEPTANCE.md](archive/UI_COMPONENTS.md#project-row-component-acceptance)。

项目拖动只绑定当前侧边栏的 `DragScope`、既有 `ReorderCommand` 和 `IsReorderEnabled`。
共享行拥有鼠标阈值、原生拖放与上下插入线，导航滚动区通过 `NavigationDragScope`
复用同一作用域；页面不得自行处理拖放或构造第二份项目顺序。隐藏、禁用或不可用祖先
拒绝迟到拖放并停止边缘滚动；换作用域、卸载或载荷失效同样释放订阅与计时器。
身份与原位置检查在构造顺序之前完成，自身与未改变位置的拖放不写入、不改变行状态。
原生拖动、保存、重启恢复及边界回归见 [SIDEBAR_PROJECT_DRAG_ACCEPTANCE.md](archive/UI_WORKFLOWS.md#sidebar-project-drag-acceptance)。

侧边栏置顶、项目和最近会话共用 `AppScrollViewer` 的 `sidebar-navigation-scroll`
变体。`NavigationActionVariants.axaml` 拥有 6-DIP、3-DIP 圆角的可见滑块及
12-DIP 命中区，并消除 Fluent 对父 Thumb 的静止缩放；不能只检查内部 Border.Width。
视口向侧边栏边缘延伸 9 DIP，同量右侧 Padding 保持行宽和文字位置不变。
轨道保持透明、隐藏行箭头，但保留分页点击、拖动和滚轮；悬停及按下只改变专用主题颜色。
这些规则仅作用于侧边栏变体，不改变其它滚动区或全局分隔线。
原始尺寸像素、明暗主题与禁用/内容适配边界见 [SIDEBAR_FIDELITY_ACCEPTANCE.md](archive/UI_WORKFLOWS.md#sidebar-fidelity-acceptance)。

侧边栏头部布局使用共享 `sidebar-actions-header` 变体，`has-pinned-tasks` 只绑定
现有置顶列表的 `HasItems`。有置顶时下间距 17 DIP，无置顶时 21 DIP；置顶标题
下间距 10 DIP，使标题上移但任务和项目位置不变。不能用文字 RenderTransform
越过滚动视口裁剪边界，也不能在页面自行设置另一套间距或缓存重复状态。
完整窗口坐标、真实置顶命令、原尺寸截图及限制见 [SIDEBAR_HEADING_ACCEPTANCE.md](archive/UI_WORKFLOWS.md#sidebar-heading-acceptance)。

项目编辑器仍复用共享输入、按钮、列表和 `status-error` 文字变体。`IsBusy` 表达
保存或目录选择进行中，命令拒绝重复执行，保存入口显示本地化的进行中状态。
`HasStatusError` 只选择已有错误外观，不在页面定义颜色。保存与目录选择使用
编辑器生命周期取消；迟到的保存结果不能清空新草稿，迟到的目录选择不能修改
另一个项目。错误只展示允许列表中的验证提示或安全的存储失败提示，不展示原始异常。
原生摘要、项目重命名、错误与重试保存验收见 [PROJECT_MANAGEMENT_ACCEPTANCE.md](archive/UI_WORKFLOWS.md#project-management-acceptance)。

输入区上下文条统一使用 `AppComposerContextBar`。页面只绑定项目、分支、代理名称、
代理集合与已有选择命令，并提供项目选择器的业务内容，不重新拼装三列操作区。
组件拥有浮层、图标文字组合和响应式列宽：有代理且实际宽度小于 440 DIP 时，
三列共享可用空间，长名称省略并保留完整工具提示，不能挤没分支或把操作推出卡片。
调整列宽保留原有 ColumnDefinition 实例，避免异步加载代理后仍使用旧测量结果。
`CanChooseAgent` 绑定工作区目录的可用状态，加载或失败时关闭菜单并禁用入口。
可用项不作数量截断，菜单由共享 `composer-agent-options` 滚动变体限制为最多 240 DIP。
失效选择仍保留入口以便明确改用默认代理；读取失败和失效提示复用共享文字与按钮。
项目缺失、选择器缺失或命令缺失时入口禁用；命令的 CanExecute 仍然生效。
换项目、换命令、代理不可用或卸载时关闭相关浮层，项目选择器继承业务 DataContext。
`ValidateSharedWorkspacePrompts` 和 `MOSSUI002` 拒绝页面替换内容、内边距和尺寸。
运行截图、迟到数据回归与实际菜单输入验收见 [COMPOSER_CONTEXT_COMPONENT_ACCEPTANCE.md](archive/UI_COMPONENTS.md#composer-context-component-acceptance)。

对话标题栏统一使用 `AppConversationHeader`。页面传标题与菜单的业务内容、会话目标、
状态和已有命令，不重新拼装右上角按钮或运行状态布局。共享变体统一 52-DIP 标题栏、
20-DIP 状态行、190-DIP 菜单，以及图标和动作对齐。长启动提示和运行状态在有限列中
省略，并保留完整工具提示，不挤掉标题和操作。标题与菜单宿主明确继承业务 DataContext。
检查器展开时隐藏对话区的重复展开入口和分隔线；收起操作保留在检查器顶栏。
缺失会话、菜单或命令时对应操作不可用，命令自己的 CanExecute 继续生效。
换会话、换业务上下文、换菜单/命令或卸载时关闭浮层；草稿和任务仍归原 ViewModel。
`ValidateSharedConversationHeaders` 和 `MOSSUI002` 拒绝页面替换 Content、Padding、
尺寸及内容对齐，普通属性、属性元素和原生属性所有者限定同样受检查。
运行截图、菜单实际宽度回归和验收限制见 [CONVERSATION_HEADER_COMPONENT_ACCEPTANCE.md](archive/UI_COMPONENTS.md#conversation-header-component-acceptance)。

主工作区外框复用 `AppBorder` 的 `window-content-frame` 共享变体：统一 12-DIP
圆角、右侧及底部 4-DIP 留白、内容裁剪和四周浅色阴影。页面不单独设置这些属性。
侧边栏、对话及检查器放在同一外框内，标题栏、导航轨和原生窗口缩放命中区保持在外。
侧边栏有独立目录 DataContext，展开状态必须绑定到窗口 ViewModel，不能从目录对象取值。
布局和原生截图验收见 [WINDOW_FRAME_ACCEPTANCE.md](archive/UI_WORKFLOWS.md#window-frame-acceptance)。

对话和检查器的排列统一使用 `AppWorkspacePanes`，页面只传 `PrimaryPane`、
`InspectorPane` 和 `IsInspectorOpen`，不重新计算列宽或在 ViewModel 中维护外边距。
检查器展开后默认占工作区 47%，对话和检查器的最小宽度分别为 360、280 DIP；
4-DIP 分隔条支持鼠标拖动和焦点状态下方向键调整，每次按键 10 DIP。
收起后上下文恢复 300-DIP 浮动卡片，顶部 58、右侧 16 DIP。
这些值只在 `WorkspacePaneVariants.axaml` 和组件中维护，不能在业务实例覆盖。
用户调整过的宽度在缩小窗口、收起重开及重新附着后保留；临时空间不足只作限幅。
最终排列还会使用实际可用宽度重新配置列；最大化时早期测量宽度可能仍是普通窗口约束，
不能据此把实际宽窗口的检查器永久限幅为 296 DIP。默认比例与用户偏好仍使用同一规则。
列配置后按最终尺寸重新测量共享模板，再排列子内容；欢迎标题等自适应组件不能继续
沿用旧测量宽度的分行模式。该修正在面板内完成，不给业务页面增加宽度或主题专用补丁。
组件不重建页面、任务或浏览器会话，卸载时解除列宽订阅。
`ValidateSharedWorkspacePanes` 与 `MOSSUI002` 拒绝 Content、Padding、尺寸、内容对齐、
默认比例、最小宽度、浮动位置及分隔条配置覆盖，普通属性和属性元素均受检查。
功能数据和可见状态仍可绑定；验收见 [WORKSPACE_PANE_COMPONENT_ACCEPTANCE.md](archive/UI_COMPONENTS.md#workspace-pane-component-acceptance)。

输入、数字输入和下拉选择的默认最小高度统一为 32 DIP，内容垂直居中，
在 `Styles/FieldVariants.axaml` 维护。多行配置统一使用 `field-multiline`
（最小 72 DIP、顶部对齐），系统提示词使用 `field-prompt`（最小 150 DIP）；
旧的 `json-editor` 复用多行配置变体。输入区仍使用 `composer-input`，最大高度
180 DIP 由共享样式维护，浏览器字段继续使用自己的 28-DIP 紧凑变体。
页面不得覆盖字段 Height、MinHeight、MaxHeight 或内容对齐；AXAML 的
`ValidateSharedFieldSizing` 和 C# 的 `MOSSUI002` 在构建时拒绝这些写法，
包括属性元素、原生属性所有者限定、属性赋值和重置。字段宽度仍由结构布局决定；
文字、选项、值、命令、只读和禁用状态等功能属性不受此约束。

空会话项目标题使用 `AppProjectPrompt`，页面只传前后文、项目名称、切换命令与
现有项目选择浮层。共享组件负责窄宽度下的分行、长名称省略、宽窗口内连续居中的
标题和项目操作可用状态，不在每页重新拼装三个文字节点和按钮。
输入区权限策略使用 `AppApprovalPolicyButton`，页面只绑定 Policy 和 SelectPolicyCommand。
单个 12-DIP 图标与标签由已有 `AppIconLabel` 居中，菜单的文本来自本地化资源；
当前项高亮，缺失或拒绝执行的命令不可用。组件只展示外部策略状态并转发现有命令，
不会自己修改策略或批准工具。控件脱离页面时关闭自己的浮层。
`ValidateSharedWorkspacePrompts` 与 `MOSSUI002` 禁止替换这两个组件的内容、内边距、
尺寸和内容对齐，也禁止覆盖权限按钮内部 Flyout、Command 和 CommandParameter。
普通属性、属性元素和原生属性所有者限定写法执行同一检查；功能数据绑定仍允许。
验收证据与限制见 [WORKSPACE_PROMPT_COMPONENT_ACCEPTANCE.md](archive/UI_COMPONENTS.md#workspace-prompt-component-acceptance)。

任务失败提示统一使用 `AppTaskFailureNotice`，页面只绑定 `Hint` 和可见状态。
组件拥有警告图标、有限宽度中的自动换行及共享卡片；图标采用共享 12-DIP
`icon-small`，位置和外边距由 `SurfaceVariants.axaml` 维护，不在页面拼装。
`ValidateSharedTaskFailureNotices` 和 `MOSSUI002` 拒绝替换内容、内边距、尺寸
及内容对齐。提示只能来自本地化、允许列表中的失败类别，不回显原始异常；
恢复后的提示、数据库迁移和明暗主题验收见 [TASK_FAILURE_ACCEPTANCE.md](archive/CONVERSATIONS.md#task-failure-acceptance)。

提供商和模型列表的标题与操作区使用 `AppProviderSettingsHeader` 和
`AppModelSettingsRow`，页面只传 Provider/Model、展开状态、数量和命令。
组件在有限宽度列内省略长名称和模型 ID，保留完整工具提示与可访问操作名称；
徽标、模型数量、编辑和删除操作不被长文字挤占。操作按钮由
`Styles/ProviderSettingsRowVariants.axaml` 统一 24-DIP 高度并垂直居中。
目标数据或命令缺失时操作不可用，命令自身的 CanExecute 继续生效。
`ValidateSharedProviderSettingsRows` 与 `MOSSUI002` 拒绝页面替换内容、内边距、
尺寸和内容对齐。截图、真实页面命令及拒绝验收见 [PROVIDER_ROW_COMPONENT_ACCEPTANCE.md](archive/PROVIDERS.md#provider-row-component-acceptance)。

提供商与模型页面共用 `ProviderModelCatalogViewModel` 的完整配置快照，
加载、读取失败和成功空结果必须分开呈现，不能用列表数量判断读取是否成功。
提示继续使用共享 `AppCard`、`AppText` 变体，重试使用已有刷新命令；
读取期间或失败后保留旧列表和弹窗草稿，但禁用修改操作。刷新只同步保存身份，
不重新填充打开的编辑器；原目标删除后保留草稿并要求重新选择，不能自动换到其他提供商。
获取/添加模型也使用共享 24-DIP 操作变体；提供商、模型及其他设置行的禁用操作
统一半透明、箭头光标，不能仍像可点击操作。恢复后由共享样式恢复正常状态。
详见 [PROVIDER_CATALOG_ACCEPTANCE.md](archive/PROVIDERS.md#provider-catalog-acceptance)。

提供商/模型保存、删除与发现后的模型导入共用目录的写入执行器。
等待写入时，相关行操作使用同一禁用样式；写入失败显示本地化安全提示，
保留打开的草稿并允许重试。写入过程中重新打开编辑器或修改字段时，
旧操作完成不能关闭或清空新草稿；成功删除其他模型也不能重置当前编辑器。
读写状态和编辑版本由 ViewModel 管理，页面不另建样式或异步处理。
设计、失败回归和实际鼠标/键盘验收见 [CONFIGURATION_MUTATION_ACCEPTANCE.md](archive/ENGINEERING.md#configuration-mutation-acceptance)。

网络代理、MCP、自定义代理与子代理列表复用 `AppSettingsItemRow`，不再在页面中
拼装标题、协议、状态徽标和操作。组件将长名称与说明限制在可省略的有限列中，
保留完整工具提示；元数据和徽标同样有上限，不能遮住右侧操作。操作统一 24 DIP，
垂直居中，与提供商和模型共用 `Styles/ProviderSettingsRowVariants.axaml` 中的尺寸规则。
其余行外观由 `Styles/SettingsItemRowVariants.axaml` 维护。`compact` 用于单行代理，
`multiline` 用于两行触发条件，`endpoint` 使用共享代码字体。
页面仅传 Title、Subtitle、Metadata、BadgeText、状态和命令；ActionTarget 始终绑定
当前行数据。启用/停用通过两种语义标签与 IsPrimaryAlternate 切换，不创建两个叠放按钮。
未提供动作标签时不显示操作，缺少命令或目标时禁用，命令自己的 CanExecute 仍生效。
组件保留 `app-settings-item-row` 样式身份；封装尺寸、Content、Padding 和内容对齐
受现有 C#/AXAML 构建门禁保护。协议与地址只显示必要信息，MCP 继续使用已有安全摘要，
不展示 URL 凭据、路径、查询、参数、环境变量和请求头。详见 [SETTINGS_ITEM_COMPONENT_ACCEPTANCE.md](archive/UI_COMPONENTS.md#settings-item-component-acceptance)。

`Grid`、`StackPanel`、`WrapPanel`、`Panel`、`ContentPresenter` 可以作为结构元素。
共享 `AppActionButton` 的图标、标签和徽标组合在内部统一按同一垂直中心排列，
避免奇数行高时分别取整造成 1-DIP 错位；外部按钮尺寸和点击区域保持不变。
可访问名称自动值只作为回退，不覆盖页面显式指定或绑定的 `AutomationProperties.Name`。
这些布局元素不能在页面中设置背景、边框、圆角、阴影等视觉属性；表面使用 `AppBorder`。
模板、资源、绑定、画刷和几何数据不是业务视觉组件，可继续使用 Avalonia 框架类型。
框架模板自动生成的内部元素不需要再实现一套替代品，其外观由共享样式统一控制。
业务 C# 也不能通过 `Activator.CreateInstance` 反射创建已知的原生视觉控件；
泛型调用、直接 `typeof` 和只读局部类型别名都按相同组件门禁检查。
组件构造时添加的 `app-*` 样式标识属于共享实现，业务代码不能清空、索引替换、
批量替换或重排 `Classes`，也不能增删或切换保留的 `app-*` 标识。
AXAML 业务页不能用 `Classes.app-*` 状态绑定或显式标识冒充另一种组件。
正常状态仍可使用 `Classes.selected` 等绑定；C# 通过明确的常量变体名调用
`Classes.Add`、`Remove` 或 `Set`，例如 `Classes.Set("selected", isSelected)`。
不要传动态类名或整体重建类集合；这会破坏组件身份。只读检查和组件内部实现不受限制。
这些约束由 `MOSSUI002` 与 `ValidateSharedStyleClassIdentity` 在编译时执行，
与既有原生组件和实例外观门禁共同生效，不是仅靠代码约定。
业务 C# 的解构赋值也执行相同门禁，例如 `(button.Width, button.Height) = (22, 22)`
不能绕过共享尺寸。嵌套元组、属性索引器、基类转换和外观集合项逐个检查；
功能状态或纯结构布局的元组赋值仍允许，共享组件实现仍能维护自己的外观。
原生浏览器通过 `AppEmbeddedBrowserView` 封装，窗口根布局以共享 `AppBorder.Child`
常驻承载 `AppBrowserHost`。各任务和标签页视图始终附加并获得非零布局尺寸；
检查器通过 `AppBrowserViewport` 提供当前显示位置，其余视图在窗口内容范围外停放，
不能接收鼠标命中或键盘焦点。不要让隐藏的检查器承担浏览器初始化，
也不要用内容模板、切换任务时重新创建视图或合成离屏帧替代真实页面承载。
工具审批等待期间通过 `AppBrowserHost.IsPresentationBlocked` 停放原生视图，
避免原生子窗口覆盖共享审批弹窗；这只改变呈现，不释放会话或改变审批策略。
`AppBrowserViewport` 附加时订阅祖先的位置、可见性和变换属性，脱离时全部解除。
浮动卡片与停靠面板切换时，即使视口自身的 Bounds 不变，也必须更新原生位置。
页面不添加临时偏移或重建浏览器会话来绕过布局；拖动、收起重开和审批取消的
原生截图与回归验收见 [BROWSER_EXECUTION_ACCEPTANCE.md](archive/BROWSER_MCP.md#browser-execution-acceptance)。
主窗口协调器订阅审批状态并将最终状态更新投递到 UI 线程，关闭时解除订阅，
不能在容器释放的后台线程销毁 Avalonia 属性绑定或修改原生视图。
Windows 应用清单声明兼容系统和 DPI，
继续以普通权限运行；这不是允许业务页面直接使用第三方视觉控件的例外。
弹出层的 `FlyoutPresenterTheme` 和 `FlyoutPresenterClasses` 也由共享组件维护，
业务页面不能替换主题或增删呈现器类名；内容、位置和关闭行为仍可按业务需要设置。

组件实例的本地 `Resources` 也不能用来覆盖外观。业务 C# 不替换或修改控件资源字典，
包括字典别名、索引器、合并字典和明暗主题字典；只读查询仍允许。
业务 AXAML 可声明转换器和使用共享组件的 `DataTemplate`，但不能声明本地画刷、字体、
阴影或图标几何，也不能导入资源文件或绑定替换整张资源字典。外观资源统一放在
`App.axaml` 或 `Styles/`，全局字体与主题服务仍可更新应用级共享资源。

## 页面用法

```xml
<ui:AppActionButton Classes="ghost"
                    IconData="{StaticResource UiIconPlus}"
                    Label="{Binding AddLabel}"
                    Command="{Binding AddCommand}" />
<ui:AppIconButton IconData="{StaticResource UiIconClose}"
                  Label="{Binding CloseLabel}"
                  Command="{Binding CloseCommand}" />
<ui:AppCard Classes="card-inset-roomy">
  <StackPanel Spacing="12">
    <ui:AppText Classes="settings-section-title" Text="{Binding SectionTitle}" />
    <ui:AppInput Text="{Binding Name}" />
  </StackPanel>
</ui:AppCard>
```

图标按钮自己创建图标、维护尺寸并提供可访问名称和提示，不要重新指定 `Content`。
`AppIcon` 统一按几何边界等比缩放并在方形槽内居中，非方形图标和水平、垂直线
不再依赖原生 Path 的顶部排列。描边粗细保持为共享样式定义的显示单位，
不随几何缩放而改变；页面不指定 `Stretch` 或用局部位移纠正图标。
描边端点、连接、虚线及渲染位移、缩放同样属于共享外观；业务 AXAML 和 C#
不能覆盖，也不能通过修改虚线集合绕过约束，需要时添加共享变体。
图标文字按钮自己处理水平布局和垂直对齐，不要在页面中手动拼装同类按钮。
输入区的模型入口、模型菜单行和思考强度入口也复用 `AppActionButton`：
模型名称使用 `Label`，思考强度使用 `BadgeText`，箭头和选中标记使用尾图标。
字号、徽标间距和图标尺寸在 `Styles/ActionButtonVariants.axaml` 定义，
不要在页面中重新拼装图标、文字与选中勾。加载、读取失败和选中模型不可用
通过数据状态呈现；刷新期间禁用模型选择，失败时保留上次列表但不能发送。
`AppIconLabel` 的子控件与行列定义由组件内部维护，业务代码不能追加、移除、清空
或通过索引替换这些集合，集合别名与转换成原生 Grid 也不能绕过检查。
页面只读检查与真正的 Grid、StackPanel 等结构布局集合仍可正常使用。
`AppActionButton` 的标签使用有限宽度列和省略号；徽标与可选尾图标拥有独立自动宽度列。
用 `TrailingIconData` 表达下拉箭头或选中标记，用 `IsTrailingIconVisible` 表达选中状态。
未提供前图标时不保留空图标槽；共享变体通过 `LabelMaxWidth` 限制项目和代理选择器标签。
`AppIconLabel` 和 `AppActionButton` 的 `LabelMaxWidth` 只由组件实现和共享样式设置，
业务 AXAML 或 C# 不能单独覆盖；分支、项目摘要等用途通过语义类选择统一的截断上限。
侧边栏项目、项目菜单以及输入区项目和代理选择器复用这一组合，不再各自拼装图标与文字。
保留绑定、命令、可用状态、可见状态与合理的结构布局属性。
工具执行记录统一使用 `AppToolActivityRow`，通过摘要、状态、是否可展开和命令表达行为。
多行工具摘要在行内只显示首行，完整元数据留在展开正文；没有真实详情时显示静态行，
不再伪装为禁用按钮。组件统一维护图标、文字和状态的垂直对齐与可访问名称，
禁止替换其内容或在实例上覆盖尺寸。
用户可配置的颜色通过 `AppColorSwatch.ColorHex` 等语义属性传递，
而不是让页面直接改变组件的 `Background`。

附件统一使用 `AppAttachmentChip`，通过 `DisplayPath`、`SizeLabel` 和 `RemoveCommand`
传递数据。输入区传移除命令，消息中的附件不传命令，组件自动隐藏移除按钮；
命令不可执行时保留禁用状态，不把它当作可用操作。图标、双行文字、截断宽度、
圆角、内边距、四周浅色阴影和移除点击区域统一在 `AttachmentChipVariants.axaml`
维护。业务页不能替换 Content、内边距或尺寸，也不要手动拼装另一套附件卡片。
完整路径通过提示和可访问名称保留；长标签不能挤掉文件图标或移除操作。

侧边栏的置顶、项目和最近会话列表统一使用 `AppTaskNavigationRow`，
通过 `Title`、`IsSelected`、`IsProjectTask`、命令及 `CommandParameter` 传递状态。
组件维护同一套文字层级、选中背景、长标题省略和悬停、键盘焦点时显示的选项按钮。
选中背景仅应用于当前会话，不同时给其项目标题增加一条选中背景。
各菜单操作与选择按钮都接收该行的任务参数；`PinLabel` 来自该任务自己的置顶状态，
不能借用当前打开会话的状态或无参数菜单命令。重命名和归档检查目标任务的运行状态，
操作其他会话不切换当前对话；最近列表也提供相同菜单，不再单独拼装简化行。
组件拥有内容、内边距和尺寸，AXAML 构建门禁和 `MOSSUI002` 禁止业务代码覆盖这些属性。

会话菜单的置顶、重命名、归档和恢复共用已有写入执行器，写入中禁用互相冲突的动作，
直接执行命令也不能绕过。持久化失败只显示本地化安全提示，通过现有 `status-error`
变体呈现，不在侧边栏添加错误颜色。晚完成的重命名保留新草稿；同一会话的置顶或
元数据刷新不重置标题编辑器。取消和关闭向仓库传递所有者令牌，不发布过期结果。
普通权限菜单操作、重启后恢复及测试边界见 [TASK_MUTATION_ACCEPTANCE.md](archive/CONVERSATIONS.md#task-mutation-acceptance)。
标题通过仓库端口的专用事务更新，不再回写旧任务全量快照；冲突保留草稿，
成功发布存储返回的完整元数据。重命名和归档期间只阻止同一目标任务开始运行，
其他任务和停止操作不被全局禁用；发布完成后再解除阻止。
并发状态、外部标题、归档、取消与回滚回归见 [TASK_TITLE_PERSISTENCE_ACCEPTANCE.md](archive/CONVERSATIONS.md#task-title-persistence-acceptance)。

导航操作的显示状态统一在 `NavigationActionVariants.axaml` 和 `ConversationTabVariants.axaml`
维护：隐藏时同时关闭鼠标命中，行悬停或键盘焦点进入时显示，焦点离开且没有悬停时隐藏。
选中会话标签保留选项按钮；菜单打开期间，即使焦点进入弹出层，触发按钮也不能消失。
显示状态不改变命令可用性，禁用操作不会因为悬停而启用。不要在页面绑定透明度补丁。
详见 [NAVIGATION_ACTION_ACCEPTANCE.md](archive/UI_WORKFLOWS.md#navigation-action-acceptance)。

浏览器操作区统一使用 `AppBrowserToolbar`：`Address` 默认双向绑定，
`NavigateCommand` 同时用于地址栏 Enter 和导航按钮；返回、前进、刷新和开发工具
分别使用对应命令。组件将地址与操作分为两行，按钮复用 28×28 的 `icon-compact`，
地址字段通过 `browser-address` 变体定义，不在业务页指定尺寸或替换组件内容。
浏览器标签选择器复用 `compact-selector browser-tabs`，与工具栏保持相同高度。

水平标签列表使用 `AppSelectionScrollViewer.SelectedItem` 绑定当前项。
选中项、可用宽度或内容变化后，组件在布局完成时执行有界的最小滚动；
不会每次布局都抢回用户手动滚动的位置。卸载时解除布局订阅，不保留过期回调。

设置编辑弹窗统一使用 `AppSettingsDialog`：`Title`、`IsOpen` 和 `CloseCommand` 表达状态，
`Content` 放表单，`Footer` 放操作和结果。不要在设置页面重新拼装遮罩、卡片、标题和滚动区域。
普通和 `wide` 变体统一提供宽度、外边距、共享卡片阴影、滚动正文和固定底部操作；
高度受实际可用空间约束。打开后聚焦首个字段，Tab 在弹窗内循环，Escape 执行可用的关闭命令，
关闭后恢复之前的焦点。下拉框先处理自己的 Escape，不应越过控件关闭整张表单。
退出收尾同样复用该弹窗，通过 `IsCloseButtonVisible="False"` 隐藏关闭按钮；
等待期间没有关闭命令或强制退出入口，失败后才显示明确的退出操作。

对话中的工具详情使用 `AppScrollViewer` 的 `tool-details-scroll` 共享变体，
限制高度并保留纵向滚动，横向滚动禁用，让长路径、工具输出和子代理回复获得
有限宽度并真正换行。详情表面的缩进和内边距由 `tool-detail-surface` 维护。
代码块仍保留独立的横向滚动；不要为工具正文的长行关闭整个对话区的换行。

全屏页面切换使用 `AppPageSwitcher`：`PrimaryPage`、`SecondaryPage` 放页面，
`IsSecondaryActive` 表达当前页面。组件保留两页实例、草稿和编辑状态，
仅显示当前页，隐藏页不能继续接收 Tab 焦点或出现在正常可访问树中。
进入设置页优先聚焦可用字段，返回时恢复仍然有效的原焦点；设置页内 Tab 循环。
不要用叠在工作区上的可见遮罩替代页面切换，也不要覆盖组件拥有的 `Content`。

## 共享样式位置

标题栏编辑菜单统一使用 `AppEditMenu`，页面不再拼装菜单行或用空格对齐快捷键。
菜单复用 `AppActionButton` 的文字与快捷键两列，尺寸、字体、分隔线和禁用透明度
在 `ActionButtonVariants.axaml` 定义，浮层复用共享圆角与四周浅阴影。
普通操作徽标保持 10-DIP 字体，编辑菜单快捷键使用 12-DIP 字体。
组件跟踪同一窗口的有效编辑目标，并在菜单转移焦点时保留原选区；
不会改变输入框原本的失焦清理策略，也不会把旧选区恢复到已替换的正文上。
撤销、重做、剪切、复制、粘贴和全选按目标及状态启用；
密码字段不能剪切或复制，只读字段不能修改，隐藏、禁用、移出或关闭的目标不能执行。
卸载菜单解除焦点与属性订阅，关闭浮层并废弃旧命令；普通输入及剪贴板操作仍由 Avalonia 执行。
`MOSSUI002` 与 `ValidateSharedEditMenus` 拒绝业务代码替换菜单内容、浮层、命令或尺寸。

- `App.axaml`：明暗主题语义画刷、共享几何及基础样式。
- `Styles/ComponentVariants.axaml`：按钮与导航等控件主题。
- `Styles/SurfaceVariants.axaml`：表面、字段与浮层变体。
- `Styles/FieldVariants.axaml`：输入、数字与下拉字段的统一高度、内容对齐和多行变体。
- `Styles/NavigationActionVariants.axaml`：导航操作的悬停、焦点、命中和菜单打开状态。
- `Styles/TypographyVariants.axaml`：字体层级、字重与行高。
- `Styles/CardInsetVariants.axaml`：卡片内边距。
- `Styles/ComboBoxVariants.axaml`：下拉框与框架生成的菜单、选项状态。
- `Styles/CheckBoxVariants.axaml`：复选框尺寸、明暗主题选中配色、悬停、焦点、禁用和三态。
- `Styles/ThemePreviewVariants.axaml`：外观预览及随预览一起缩放的选中描边。
- `Styles/SettingsDialogVariants.axaml`：设置弹窗结构与普通、宽版尺寸。
- `Styles/WindowVariants.axaml`：窗口背景、标题栏图标、缩放热区尺寸和缩放光标。
- `Styles/WorkspacePaneVariants.axaml`：工作区最小宽度、检查器比例、浮动位置和分隔条状态。
- `Styles/ChatHeaderVariants.axaml`：对话标题栏的紧凑图标、上下文选中背景和侧栏分隔线。
- `Styles/BrowserToolbarVariants.axaml`：浏览器地址栏与标签选择器的紧凑尺寸。

标题栏窗口操作、历史导航和侧边栏切换图标采用不同语义变体，
绘制槽尺寸与截图中含描边的实际图标边界并不相同；窗口按钮仍保留 46×36 点击区域。
需要对齐设备像素的标题栏偏移只定义在共享变体，不在页面实例上追加补丁。

对话标题栏的三项操作共用 `chat-header-action`：保留 32×32 点击区域，
图标使用 10-DIP 槽和 1-DIP 描边；上下文背景为居中的 28×28 圆形。
侧栏入口在收起时显示圆角方框加号，展开时由共享图标按钮切换为面板图案；
标签、提示和可访问名称同步表达“展开”或“收起”。两种状态都复用既有面板命令，
关闭检查器恢复用户此前的上下文卡片显示偏好，不强制打开用户已经隐藏的卡片。

调整图标尺寸、按钮点击区域、字体、圆角或阴影时，修改共享样式或添加有明确用途的变体。
会话仅有一个选中标签时使用 `AppIconLabel.conversation-heading`，长标题在有限宽度内
省略，不再显示标签卡片和额外的新建按钮；多个标签和未选中任务的草稿继续显示可滚动标签条。
关闭单标签仍通过右侧任务菜单执行，不归档或删除会话，新对话入口仍在侧边栏和任务菜单。
标题的 12-DIP 图标、16-DIP 图文间距及 14-DIP 字体由 `ConversationTabVariants.axaml`
维护，标题栏缩进由共享表面变体维护。`AppWindow` 统一设置灰度文本抗锯齿，
该 Avalonia 渲染选项没有可用于 Style Setter 的属性字段，因此只在共享窗口初始化一次。
卡片、设置弹窗、操作浮层和下拉列表的阴影统一引用 `SharedSurfaceShadow`，
保持零水平、垂直偏移的四周浅阴影，不在每种组件中重新定义数值。
共享样式的 `BoxShadow` Setter 只能引用该资源或显式选择 `None`；
`ValidateSharedSurfaceShadows` 在构建时拦截独立数值和属性元素写法。
禁止页面局部 `Style`、`ControlTheme`、字面量颜色以及实例级视觉覆盖；
同样不能用 AXAML 属性元素或 C# 赋值绕过此规则。

## 新增组件

1. 先检查目录中是否已有能表达需求的组件和共享变体。
2. 新视觉组件放在 `Controls`；页面宿主放在 `Views` 并继承共享宿主。
3. 所有手写 C# 文件只声明一个类型，遵守 `AGENTS.md` 的体积限制。
4. 在共享样式中实现普通、悬停、焦点、禁用和所需选中状态。
5. 保留键盘交互、可访问名称和正确的命令行为。
6. 添加正常用法及违规用法测试，在明暗主题、不同窗口尺寸下截图验收。

组件归属不仅靠目录约定：`MOSSUI001` 同时检查继承声明。
业务代码不能通过派生 `AppButton`、`AppSelectableText` 等共享基类建立页面专用控件，
也不能将已有页面继续当作可复用控件基类；正常类声明与主构造函数执行相同检查。
页面宿主必须位于 `MossAgent.App.Views`，直接继承 `AppUserControl` 或 `AppWindow`。
共享组件内部仍可继承原生控件或其他共享组件，纯结构布局及组件实例使用不受影响。
新增语义变体应放在共享样式中，新增可复用视觉实现应放在 `MossAgent.App.Controls`。

### 编译规则约束自检

- 组件实现与样式由 `MossAgent.App` 所有；编译守卫由 `MossAgent.App.Analyzers`
  和 App 构建规则所有，不向领域层或工具层引入 UI 依赖。
- 守卫只检查源码和标记，不需要新增运行时端口或工具契约。
- 不运行用户任务、不修改持久化或授权策略，不新增审批、超时或输出通道。
- 守卫与回归测试各自保持单类型、小文件，不通过大型页面或嵌套类型实现。
- 回归覆盖合法共享控件、结构布局和功能绑定，以及原生控件、独立外观、
  页面专用视觉继承、错误宿主归属、规则禁用和降级的拒绝路径。
  编译约束修改不代表新的截图视觉验收。

## 强制检查

- `MOSSUI001`、`MOSSUI002` 是不可配置的构建错误，不允许通过 `#pragma warning disable`、
  项目诊断配置或编辑器规则关闭检查或降级为警告。需要新样式时添加共享变体，
  需要新视觉组件时在 `Controls` 封装，不在业务页绕过规则。
- `ValidateSharedComponentResources` 拒绝业务 AXAML 的本地视觉资源、资源导入和
  `Resources` 属性替换；`MOSSUI002` 同时拒绝业务 C# 对控件资源字典的写入、清空、
  替换以及合并、主题字典修改，防止通过 Fluent 资源键绕过共享样式。
- App 构建的 `ValidateShared*` 规则拒绝 AXAML 原生视觉组件、未知视觉命名空间、
  页面局部样式、结构元素视觉属性和共享组件实例外观覆盖。
  外观检查覆盖 App 下所有业务 AXAML，包括以后新增的目录；仅共享 `App.axaml`、
  `Styles/` 和构建输出排除在实例外观检查之外。新增组件也不能直接指定颜色、字体、
  阴影、圆角或光标，使用原生属性所有者限定写法也不能绕过检查。
- `ValidateSharedSettingsDialogs` 拒绝设置页自建遮罩弹窗，以及弹窗实例尺寸和外观覆盖。
- `ValidateSharedToolActivityRows` 拒绝工具行内容和尺寸覆盖，包括原生属性所有者限定写法；
  C# 赋值也受 `MOSSUI002` 同一约束。
- `ValidateSharedTaskFailureNotices` 拒绝失败提示内容和尺寸覆盖，包括属性元素与
  原生属性所有者限定写法；数据提示和可见状态仍允许，C# 同样受 `MOSSUI002` 约束。
- `ValidateSharedProviderSettingsRows` 拒绝提供商头和模型行的内部组成、内边距、
  尺寸和内容对齐覆盖；功能数据和命令绑定仍允许，C# 同样受 `MOSSUI002` 约束。
- `ValidateSharedLabelSizing` 拒绝业务 AXAML 的标签宽度覆盖，包括属性元素和属性所有者限定写法；
  `MOSSUI002` 同时拦截 C# 赋值、绑定、索引器和属性别名，结构布局宽度仍可正常设置。
- `MOSSUI001` 拒绝共享组件实现之外直接构造或继承原生、第三方视觉控件。
  别名、目标类型 `new()`、主构造函数继承和受原生控件约束的泛型工厂也受检查。
  泛型工厂需要约束为共享组件；结构布局元素仍可直接使用，包括 C# 的
  `Avalonia.Controls.Presenters.ContentPresenter`，其内容可承载共享组件。
- `ValidateSharedComponentAppearance` 和 `ValidateSharedComposedControls` 对普通属性、
  所有者限定属性及属性元素执行相同检查，不能通过 `Button.Width`、`Border.Padding`、
  `Grid.ColumnSpacing` 或 `Button.Content` 绕过共享尺寸、内边距和组件组成。
  复选框、切换按钮也使用共享按钮尺寸；图标和颜色预览的最小、最大尺寸同样受约束。
  普通按钮内容、输入数据、命令、弹出菜单和真正的结构布局仍由页面负责。
- `ValidateSharedVisualTemplates` 禁止业务 AXAML 直接替换 `Theme`、`Template`、
  `FocusAdorner` 或导入页面局部样式，包括属性元素写法和新建的页面目录。
  页面通过 `Classes` 选择共享变体，窗口控制和历史导航按钮的模板也由共享变体提供。
  弹出层呈现器主题与类名覆盖同样被拒绝，包括原生属性所有者限定写法。
- `MOSSUI002` 拒绝共享组件实现之外的 C# 实例外观赋值、直接 `SetValue`、
  `SetCurrentValue`、`Bind`、`ClearValue` 外观属性，以及局部样式、控件主题构造。
  页面不能通过清空属性移除组件自带内容、图标尺寸或浮层主题；属性别名、
  原生基类转换和泛型约束同样受检查。普通按钮内容、输入数据、可用状态和
  真正的结构布局尺寸仍可清空，组件实现内部的外观重置不受此限制。
  复合赋值、自增自减和 Avalonia 属性索引器（包括绑定索引器）也受同一规则约束；
  功能属性和共享组件实现内部的赋值不受影响。
  泛型及传递约束、直接类型转换、命名参数换序、静态扩展绑定调用同样受检查。
  页面直接追加、插入、移除、清空或通过索引替换控件的 `Styles` 也会报错，
  包括直接转换成集合接口后修改；使用 `Classes` 选择共享变体，
  应用级共享样式装配和组件内部样式装配不受此限制。
  声明初始化后未再赋值的局部变量别名同样受检查：不能先保存外观属性、
  绑定索引器、控件或 `Styles` 集合，再通过该变量覆盖共享样式。
  多级别名、集合接口和局部 `AvaloniaObject` 类型转换也不能绕过这一检查。
  只读检查和功能属性绑定仍然允许；重新赋值或通过 `ref`、`out` 传递的变量，
  不会被错误地当作其初始值。
  对弹出层的 `FlyoutPresenterTheme` 赋值或绑定、`FlyoutPresenterClasses` 集合修改
  同样会报错，包括集合别名、接口转换、索引替换及 `Set` 移除默认类名。
- 组件用法和视觉规则测试还检查图标按钮组成、共享文字层级、卡片阴影及模板状态。

```powershell
dotnet test tests/MossAgent.App.Tests/MossAgent.App.Tests.csproj --no-restore --disable-build-servers -m:1
dotnet build MossAgent.slnx --no-restore --disable-build-servers -m:1
```

编译和规则测试通过不等于工作流已验收。还需运行界面，在主要窗口尺寸、明暗主题和
键盘操作下检查遮挡、裁切、可读性、焦点和真实交互；不以与 Codex 像素一致作为完成条件。

侧边栏搜索只绑定 `Query`（双向）、`HasMatches` 和已有 `ClearCommand`，
不能重新拼装浮层或替换内部内容。`SidebarSearchVariants.axaml` 拥有 270-DIP 浮层、
无边框输入和 12-DIP 清除图标；快捷键、输入焦点和浮层生命周期由组件拥有。
`ValidateSharedSidebarSearch` 和 `MOSSUI002` 拒绝内容、尺寸及 Padding 覆盖，
原生交互及历史外观核验范围见 [SIDEBAR_SEARCH_COMPONENT_ACCEPTANCE.md](archive/UI_COMPONENTS.md#sidebar-search-component-acceptance)。

需要验证实际绑定和键盘命令的组件测试使用 `Avalonia.Headless` 的独立会话，
在它的 UI 调度线程内构造、编辑和断言，并异步释放会话。
Avalonia 12.1 的首个调度线程和普通 xUnit 工作线程不一定相同；
只禁止测试并行不能保证绑定已同步更新，不要以此删减命令或双向绑定断言。
共享样式的文字装饰应创建自己的 `TextDecorationCollection`/`TextDecoration`，不要
使用字符串 `Underline` 隐式复用 Avalonia 的静态装饰实例；否则第二个 Headless 会话
绘制时可能访问第一个会话的 UI 线程对象。文字装饰是非视觉元数据，不是原生控件例外。
无应用宿主的工作流测试仍验证原有无消息循环行为；Headless 不是原生 WebView2 验收。
需要真实共享字体和样式的布局测试使用 `SharedVisualTestApplication`：只加载 App
的视觉资源，并注册 Inter 字体及 Headless 平台，不调用生产应用的服务装配和数据库启动。

左侧导航轨只绑定已有的新会话、审计、检查器、设置命令和 `HelpContent`，
不在页面重新拼装图标或菜单。`NavigationRailVariants.axaml` 拥有轨宽、按钮、
图标光学偏移、选中色和底部间距；`ValidateSharedNavigationRail` 与 `MOSSUI002`
拒绝内部内容、Padding 和尺寸覆盖。帮助内容继承业务 DataContext；缺失功能禁用，
命令 CanExecute 保留，所有权替换、隐藏、禁用或分离时关闭浮层并解除祖先订阅。
定时任务已有运行链路；完整状态与剩余范围见[功能清单](FEATURE_STATUS_AND_IMPLEMENTATION_PLAN.md)。
组件只承载真实可用的命令，未实现动作应说明限制。
任务旋转动画只在专用图标主题内安装，不能在全局条件样式中污染其他图标的变换。
`TranslateTransform` 与 `RotateTransform` 均为非视觉值对象；不放行原生视觉控件，
业务页面仍不得覆盖 RenderTransform。原生像素与交互范围见
[NAVIGATION_RAIL_COMPONENT_ACCEPTANCE.md](archive/UI_COMPONENTS.md#navigation-rail-component-acceptance)。
