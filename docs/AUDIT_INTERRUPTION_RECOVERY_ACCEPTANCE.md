# Interrupted tool audit recovery

Status: integrated through 30 hash-guarded paths; original Debug/Release solution
builds have zero warnings/errors. Original focused checks pass. Complete Debug
regression 54927 is terminal: 8,153 pass, no failures, skips or aborts, with
unchanged source and runtime identities. This is not full-product completion.

## Reproduction and behavior

The original-assembly Queue/Agent + managed-Chrome probe holds a real navigation
request after explicit AskEveryTime approval. An external controller kills only
the exact owned host after binding its descendants. Eleven descendants exit
automatically and another owned browser instance preserves its clicked page.
The restarted host restores the task as Interrupted, retains its remaining Queue
entry and draft, and sends no automatic model/browser request. Explicit Queue
continuation sends only the saved remaining message with its selected model and
reasoning. The interrupted navigation is not replayed.

The first lifecycle-only run passes. Adding a presentation requirement then
reproduces a product bug: the old incomplete audit remains `pending` / 进行中,
with no explanation, even though its host no longer exists. The raw failure is
`artifacts/queue-browser-crash-20261019/runtime-audit-baseline.log`, case
`2dae5fda242f4badbffca38717b2f459`. Its stored presentation JSON and exception
remain intact; passing lifecycle checks are not a passing presentation check.

Schema 24 adds a separate interruption-observation table. Startup recovery marks
unfinished audits and Running/Waiting tasks in one transaction, preserving the
earliest interruption observation. Original audit IDs, timestamps, approval data,
results and artifacts are retained. It never invents a completion time, approval,
success/failure, output or tool message. Completed operations remain unchanged;
a subsequently received real completion remains authoritative. New executions
start without a historical interruption marker. Recovery invokes no tool, model,
browser or command.

The existing shared audit row now shows 已中断, 执行结果未知 and a localized
instruction to verify actual state before manually continuing. This distinguishes
an unknown effect from live work, cancellation, denial or verified failure. The
central icon style uses the existing warning token; text carries the meaning.

## Verification and evidence

- Independent Debug/Release complete builds: zero warnings/errors.
- Independent Debug/Release Infrastructure: 729 checks each, covering forward
  migration rollback/retry, legacy messages/audits, artifacts, idempotence,
  completed-vs-unfinished rows, recovery rollback, cancellation and new/late work.
- Independent Debug/Release App: 42 audit checks each. Shared Headless rows cover
  220/360 widths in light/dark themes, the actual recovery text, wrapping and state
  classes. These are not native visual/keyboard acceptance.
- Independent Debug/Release Tools: 44 audit/deadline checks each.
- Original Debug/Release complete builds: zero warnings/errors. Each configuration
  passes Windows 148, Infrastructure 729, Tools audit/deadline 44 and App
  subagent/Headless/audit 206 checks. The original verification manifest retains
  each complete TRX hash. Structural review checks 591 C# files, 30 AXAML files
  and 3,236 methods/accessors, with a maximum method length of 78 and no violations.
- Candidate and original real-Chrome process checks pass in
  `runtime-audit-candidate-final.log` and `runtime-audit-original.log`, cases
  `eba5e9336baa49bc92d51cbe31c8648e` and `d610182885004d67815a6f4dff35ff6e`.
  Both retain one uncertain original audit, one navigation request and three
  unique ordered user messages. Model requests are exactly two before manual
  continuation and three afterward; saved model/reasoning and the unsent draft
  remain intact. All eight loaded module hashes are checked against the intended
  runtime and recorded separately for each checkout.
- A copied real schema-23 crash database also migrates through the original
  executable to schema 24 and shows interrupted/unknown without manual
  continuation. Its two messages, queue, draft and audit identity are preserved.
  `legacy-copy-aed34467e6a54bc7b88ea02167415d15` retains the copy manifest and
  `legacy-recovered.json`; the source database, WAL and SHM hashes stay unchanged.
- An earlier candidate probe correctly fails its assembly identity guard because
  SDK CandidateAssemblyFiles selected the old output DLL ahead of HintPath.
  Resolved-reference JSON and the failed case are preserved. The probe project
  now excludes both output directories from default items; its next build and
  runtime identity checks use the intended candidate. No manual DLL substitution
  or skipped identity check was used.

`artifacts/audit-interruption-recovery-20261019` retains design checks, the
original source baseline, the 30-path integration manifest, backups, structural
review, six complete candidate TRX identities and original build/check logs.
Seventeen existing persistence test files only update their current schema-count
expectation; the follow-up migration fixture also removes later version markers
when reconstructing its old checkpoint. No old failure evidence is overwritten.

Complete Debug regression 54927 is under
`artifacts/audit-interruption-integrated-20261019`. Its terminal exit is zero;
the summarizer verifies eight distinct complete TRX reports, 8,153 passed tests
and zero failures, skips or aborts. Before/after path/hash sets match for all
2,421 source/configuration and 2,035 runtime files. Report hashes and counters
are retained in `regression-summary.json`; the original-source/runtime freeze
ended after that review. Earlier failed regression reports remain preserved.

## Limits

`BROWSER_COMMITTED_EFFECT_RECOVERY_ACCEPTANCE.md` subsequently adds one original
Debug/Release case where an approved actual-Chrome click has already committed a
flushed synthetic POST effect before host-only termination. It preserves that
effect exactly once, unknown audit results, another instance, draft and Queue
through restart and explicit continuation. That bounded evidence does not extend
to arbitrary browser side effects or power loss.

This is production Queue/Agent composition in separate CLI executables, a local
simulated Responses service, actual Chrome and temporary SQLite. It does not
establish native GUI, real-provider behavior, every browser mode, every launch or
transport instant, arbitrary interrupted clicks/downloads, OS/power-loss recovery or command
filesystem confinement. An interrupted audit cannot establish whether its remote
side effect occurred. Its approval fields may remain unknown after a crash;
the probe separately observes approval before the actual navigation. No automatic
replay or fabricated success is an acceptable substitute for that uncertainty.
