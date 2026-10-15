# SQLite 测试连接池隔离验收

日期：2026-10-09。本记录是设置页预览回归中发现的测试隔离问题的后续，
不是生产 SQLite 实现变更，也不代表 Codex 界面或完整产品目标已完成。

## 问题与范围

前次默认并发全量测试中，既有
`SqliteProviderFailureMigrationTests.VersionSevenUpgrade_PreservesHistoryAndAllowsNewFailureCategories`
的 category 11 在归档任务时抛出 `ObjectDisposedException: SQLitePCL.sqlite3`。
失败栈经过 `SafeHandle.DangerousAddRef`、`SqliteDataReader.NextResult` 和
`SqliteWorkspaceRepository.SetTaskArchivedAsync`。

仓库的生产连接工厂默认启用 pooling；测试各自使用独立临时数据库，
但多处 teardown 调用 `SqliteConnection.ClearAllPools()`，会清理同一进程中
其他测试拥有的池。单独重跑通过或串行全量通过，不能证明这个并发边界安全。

本次只修改 `MossAgent.Infrastructure.Tests` 的清理范围：11 个既有测试/夹具
文件中的 17 处全局清理替换为 `SqliteTestPool.Clear(root)`，并新增两个辅助/测试文件。
生产连接工厂、repository、迁移、schema、调度和测试并发设置均未改变。

## 设计自检

1. 行为归 Infrastructure.Tests 的测试支持，不增加生产依赖或 App 的文件/数据库访问。
   辅助类复用现有 `SqliteConnectionFactory` 的连接字符串，定向退休当前数据库的池。
2. 使用现有 `SqliteConnection.ClearPool(connection)`，不关闭 pooling，
   不增加异常重试、全局锁或测试串行化来掩盖跨库干扰。
3. root 为 null、空或空白时立即拒绝，不能回退到用户默认数据目录。
   没有工具授权、审批、执行超时、取消或输出限制变化；探针只使用合成临时数据库。
4. Roslyn 审计的 13 个 C# 文件均只有一个类型；最大文件 221 行，
   最大方法或 lambda 43 行。开发和测试保持普通 JIT，不改变发布或 JSON 方案。
5. 测试覆盖 owned/foreign、idle/active handle、缺失 root、真实夹具清理及源码门禁。
   非当前数据库连接保持可用；当前数据库的活动连接在 Close 前仍可用、Close 后退休。

## 确定性红绿灯

新增 `SqliteTestPoolTests` 共八个 cases：

- 清理 owned idle handle，不退休另一个数据库的 idle handle，后者可复用。
- 重复清理 owned pool，不影响另一个数据库的活动连接及 SQL 查询。
- 分别拒绝 null、空、空白 root，不触碰已存在的其他 pool。
- owned active handle 在 Close 前仍可查询，Close 后退休，重开取得新 handle。
- 源码门禁拒绝 Infrastructure.Tests 中再次调用全局 `ClearAllPools()`。
- 真实 failure fixture 的 disposal 不退休另一个 fixture 的 idle handle；
  另一个 fixture 可继续归档任务并读取保留的历史。

初版辅助类暂用旧全局清理跑红灯：六项中两项失败、四项通过。
跨库 idle handle 断言和源码门禁均失败。改为定向清理后，新增八项及
既有四项迁移测试一起通过；没有降低 handle 身份或历史断言。

## 独立并发探针

外部探针使用本地 Microsoft.Data.Sqlite 10.0.12 和生产 Infrastructure，
八个独立 worker 数据库各做 250 轮归档/取消归档与计数查询，第九个数据库负责清理。
global/scoped 在独立进程执行，清理节奏相同；没有用户数据库、GUI、提供商或付费请求。

| 清理方式 | 成功操作 | 失败 | deadline | 时长 |
| --- | --- | --- | --- | --- |
| global | 3,998 | 1，`ObjectDisposedException: SQLitePCL.sqlite3` | 否 | 23.97 秒 |
| scoped | 4,000 | 0 | 否 | 5.36 秒 |

这是同类句柄异常的复现及范围修正证据，不是所有 SQLite 并发行为的证明。
首次更激进的探针到达 90 秒 deadline，保留诊断日志，但不计作成功或异常复现。

## 验证证据

证据目录：`%TEMP%\MossAgent-sqlite-pool-20261009`。
Python/探针/审计脚本、合成数据和日志全部在仓库外，不是发布文件。

- `focused/focused.trx`：新测试与既有迁移专项 12/12 通过。
- `parallel/parallel-1.trx` 至 `parallel-3.trx`：Infrastructure.Tests 默认并发完整跑
  三轮，每轮 144/144 通过，失败和未执行均为 0。
- `global-probe-v2.log`、`scoped-probe-v1.log`：上述两个有效探针结果。
- `source-audit.log`：单类型和文件/方法规模审计通过。
- `format.log`：13 个相关 C# 的 scoped whitespace 验证通过。
- `solution-build.log`：完整解决方案构建 0 警告、0 错误。
- `full/` 的七份独立 TRX：默认项目/测试并发完整解决方案 3,370/3,370 通过，
  失败和未执行均为 0；App 2,559、Infrastructure 144、Providers 283、Tools 188、
  Browser 76、Agent 63、Platform.Windows 57。没有使用 `-m:1`。

```powershell
dotnet test tests/MossAgent.Infrastructure.Tests/MossAgent.Infrastructure.Tests.csproj --no-build --no-restore --filter "FullyQualifiedName~SqliteTestPoolTests|FullyQualifiedName~SqliteProviderFailureMigrationTests"
dotnet test tests/MossAgent.Infrastructure.Tests/MossAgent.Infrastructure.Tests.csproj --no-build --no-restore
dotnet build MossAgent.slnx --no-restore -v:minimal
dotnet test MossAgent.slnx --no-build --no-restore --logger trx -v:minimal
```

## 剩余边界

- 其他测试项目仍有全局清理调用，本次没有扩大修改到那些夹具。
  门禁也只约束 Infrastructure.Tests，不声称所有测试进程的连接池清理已经隔离。
- 没有修改或原生复测生产 SQLite 使用策略。探针和确定性测试不等于
  Microsoft.Data.Sqlite 所有并发问题已修复。
- 没有 UI 修改，不需要为测试支持改动重新截图；设置页的原生对照与仍有的
  像素差异保留在 `SETTINGS_PREVIEW_FIDELITY_ACCEPTANCE.md`。
- 未暂存、提交、推送或发布；完整产品目标保持进行中。

## 2026-10-10 App 测试隔离设计复核

1. 本轮解决 App 完整并行回归中的测试清理边界，不修改生产 SQLite。当前 App 的
   43 个测试/夹具文件共 78 处全局清理会影响同进程的其他数据库；最近一次全量
   在 `WorkspaceAgentCatalogFlowTests` 初始化独立数据库时出现句柄已释放异常。
2. 将既有定向清理实现移至共享测试源文件，由 App.Tests 和 Infrastructure.Tests
   链接编译；两者没有运行时项目依赖。所有调用必须传入实际数据目录，区分夹具
   外层项目目录与内层 `data`，复用生产连接工厂生成的连接字符串。
3. 保持 pooling、测试并行、既有关闭顺序、取消和期限，不增加异常重试或全局锁。
   缺失 root 立即拒绝；只触碰合成测试数据库，不访问真实用户数据或工具审批。
4. 辅助类与新增测试各自单类型，按原有文件进行局部替换，不合并生产职责或更改
   普通 JIT 调试、Windows x64 单文件发布和 JSON 方案。
5. 先通过真实命令、历史夹具的释放验证其他数据库的 idle/active handle 和 SQL
   查询不受影响，并新增 App/共享源码门禁；再运行既有基础设施边界测试、受影响
   App 回归、完整默认并行测试、格式/规模检查和完整构建。

### 实现与确定性验证

- `tests/Shared/SqliteTestPool.cs` 保留既有 `ClearPool(connection)` 和缺失 root 拒绝
  行为，移除 Infrastructure.Tests 内的重复源文件；两个测试项目链接同一文件并
  使用 `MossAgent.Testing` 命名空间，不引入新的测试或生产项目依赖。
- App 的 78 处调用替换为定向清理，并移除 42 个不再使用的 SQLite using。
  六个工作区夹具传入自己的内层 `data`；原子持久化夹具传入 `_root`，模型校验
  测试传入 `fixture.Root`，其余传入原有直接数据目录。原有关闭/等待/删除顺序不变。
- `SqliteFixtureIsolationTests` 的四项实际夹具用例，在关闭命令/历史夹具后验证
  另一个数据库的 idle handle 或 active connection 关闭后的 handle 没有退休，
  重开仍复用同一句柄并可查询。第五项门禁拒绝 App 与共享测试源中的全局清理。
  新增五项修复前全部因断言失败，修复后全部通过，不是编译失败或增大期限的结果。
- App 定向回归 9/9，包括此前失败的四项代理目录流；既有基础设施连接池边界
  8/8 通过，覆盖 owned/foreign、idle/active、缺失 root 与历史保留。
  跨库测试不执行命令、不请求付费模型，也不读取真实用户数据库。
- 与本轮开始前的快照逐文件比较，43 个既有 App 文件仅包含清理调用与无用 using
  的变更；范围内 45 个 C# 单类型、文件和方法规模审计零违规，空白格式检查通过。
  首次局部替换遗漏模型校验测试的 tuple 字段名，编译报错后修正为 `fixture.Root`；
  不把这个改动期间的编译错误当作原故障复现证据。

### 最终默认并行回归

- 完整解决方案构建零警告、零错误；默认项目和测试并行下，七个项目共
  3,920/3,920 通过，失败和未执行均为零。App 2,972、Infrastructure 144、
  Providers 317、Tools 219、Browser 76、Agent 63、Platform.Windows 129。
- 此前全量失败的代理目录实际请求用例包含在这次 App 全量中。新增隔离测试
  在完整默认并行运行时也通过；未使用 `-m:1`、禁止并行、关闭 pooling、测试重试
  或扩大已有期限。这个结果证明当前回归通过，不声称所有 SQLite 并发故障均已修复。
- `full/` 保存七份独立 TRX，`full.log` 保存这一次完整执行，
  `solution-build.log`、`format.log`、`source-audit.log`、`diff-check.log` 分别保存
  构建、45 个 C# 的格式/规模审计和与开始前快照的局部变更核查。

```powershell
dotnet test tests/MossAgent.App.Tests/MossAgent.App.Tests.csproj --no-restore --filter "FullyQualifiedName~SqliteFixtureIsolationTests|FullyQualifiedName~WorkspaceAgentCatalogFlowTests"
dotnet test tests/MossAgent.Infrastructure.Tests/MossAgent.Infrastructure.Tests.csproj --no-restore --filter "FullyQualifiedName~SqliteTestPoolTests"
dotnet build MossAgent.slnx --no-restore -v:minimal
dotnet test MossAgent.slnx --no-build --no-restore --logger trx -v:minimal
```

### 本轮范围限制

- 本轮只改变测试支持与测试项目链接，不修改生产 SQLite、迁移、服务生命周期、
  UI、提供商、审批或授权行为；没有为测试支持改动新增原生截图验收。
- 其他测试项目的池清理未在本轮扩大修改，不能宣称全仓所有 SQLite 清理或压力
  并发问题均已修复。一次并行全量通过也不能替代确定性的隔离断言。
- 仓库外证据目录：
  `C:/Users/wzheng/AppData/Local/Temp/MossAgent-app-sqlite-isolation-20261010/`。
  保存本轮前快照、`red/before.trx`、`focused/` 两份 TRX、构建、格式、规模和差异
  检查日志；脚本、日志与合成数据库不加入仓库。未暂存、提交、推送或发布。
