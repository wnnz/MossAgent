# Queue browser cancellation and reconstruction check

Status: one controlled CLI workflow passes using production App composition,
saved browser/model configuration, Queue dispatch, Agent, the real ToolExecutor,
SQLite and actual managed Chrome. The model service and web page are local,
deterministic fixtures. This is not native GUI or real-provider acceptance.

## Established chain

The independent JIT harness in artifacts/queue-browser-runtime-20261019 references
the existing original App assemblies; it does not rebuild src/tests. It saves a
Chrome/Managed/explicit-direct browser configuration and a Responses model with
medium reasoning, then sends and queues prompts through Workspace commands.
All eight actual HTTP requests use that saved model and reasoning. Both tasks
retain AskEveryTime and receive four explicit harness browser approvals.

The first queued navigation reaches a local page that intentionally withholds its
response. The second task's own browser loads another page and waits for click
approval. Cancelling the first completes its navigation audit as tool_cancelled,
pauses its remaining Queue entry and preserves its draft. The second run and the
exact pending approval identity remain intact. Its real click and snapshot then
complete; the presented Tool.Details contains the heading and button "1".
The first task has one completed cancelled audit; the second has exactly three
completed successful navigate/click/snapshot audits. All retain their own task,
saved AskEveryTime policy and approval result.

After normal shutdown, a new production service composition opens the same
generated SQLite data. The cancelled tool result, successful snapshot and both
unsent drafts remain visible in reconstructed Workspace view models. Reading and
switching those conversations makes no HTTP request. The first Queue remains
paused until Continue is explicitly invoked; that sends exactly the remaining
prompt and does not repeat the cancelled navigation. The held page is requested
once in total. This is service reconstruction within one CLI process, not an EXE
crash or native restart.

The terminal record is runtime-details.log, with original loaded hashes for all
eight participating MossAgent modules. App is
22BD6CDF717B9CE31C83725C0AC5BB49F1F909F1A1A3861BBFDF5F1A03230CE1.
The harness build has zero warnings/errors. runtime.log preserves an earlier
harness timeout: it attempted a second send before the new-conversation command
became available. runtime-gated.log preserves a later assertion error that read
the summary instead of the presented tool details. The final harness waits for
command readiness and inspects the actual Details property; neither issue changed
production code or approval policy.

## Limits

This extends BROWSER_SESSION_OWNERSHIP_RUNTIME_ACCEPTANCE.md with Queue/Agent,
in-flight navigation cancellation, concurrent approval/run/draft preservation and
manual reconstruction. It does not verify browser creation cancellation,
browser-process crash recovery, embedded WebView2, external/daily profiles, CDP
attachment, manual takeover, native GUI, full restart or network confinement.
Only generated data and manager-created sessions are used and disposed. No
existing user browser/window, credentials, paid service, source/Debug runtime
modification, elevation, commit, push or release occurs.

Original complete regression 55845 is terminal: 8,121 pass/one queued audit
assertion failure, 8,122 total, no skips or aborts; all eight reports and all source/
runtime identities were checked before its freeze ended. After the seven-file
browser cancellation/audit assertion integration, queue-runtime-original.log under
artifacts/browser-timeout-runtime-20261019 repeats this workflow against the new
original App module identities. This does not erase the old failed run. The
command filesystem safety blocker remains open; this browser check does not
establish full-product completion.
