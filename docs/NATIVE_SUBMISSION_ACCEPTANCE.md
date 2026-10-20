# Native submission and EXE interruption acceptance

## Scope and identity (2026-10-19)

`artifacts/native-submission-20261019` uses the existing production Debug/JIT
`MossAgent.App.exe`, a new synthetic data directory and a local compatible-protocol
HTTP/SSE fixture. Settings are seeded through production persistence ports;
UI actions use the Computer Use skill/Sky. Neither a user database nor a real
provider, credential, project, existing app window or daily browser participates.

The App DLL SHA-256 is
`3E8F2A0F926CAC62C80A334049A4DC0F1C38A9459DC2C1F558C63A19E2E57548`,
identical to the App DLL in the complete 8,021-case frozen Debug regression.
Launch records bind each test PID to its exact executable path and assembly hash.
The fixture uses raw existing assemblies and does not rebuild production code.

## Established path

1. A fresh production EXE opens the no-project composer with the saved default
   fixture model. Sky types `native-first` in the observed focused editor and
   clicks the observed actual send button.
2. The local SSE provider receives exactly one real request. Native accessibility
   shows the submitted message, `native-answer-1`, the first streaming turn and
   the enabled stop button. Production repository reads while the GUI is running
   show one Running task, a null ProjectId, one user message and empty consumed
   source/target drafts. The inspector deliberately does not run database recovery.
3. The actual request uses model native-fixture, medium reasoning, temperature
   0.2, Top-P 0.9 and output limit 1,024, with no tools field. The projectless
   system instruction declares no working directory, authorized roots or tools.
4. Only the recorded test process tree is killed after path verification, while
   the controlled stream is still pending. Starting the same actual EXE with
   the same isolated data restores the task as Interrupted, retains the submitted
   user message once and leaves the consumed drafts empty. The native accessible
   conversation and its directory row remain present. No assistant completion is
   claimed: the deliberately interrupted partial stream was not finalized.
5. Provider request count remains one after restart. There is no automatic model
   call or replay. Both test EXEs and the fixture server subsequently exit; only
   those owned PIDs are terminated. This is forced termination, not graceful
   window-close acceptance.

The persisted state, selected native accessibility snapshots, request summary,
launch identities and assertions are retained in the artifact directory.
`native-evidence-terminal.json` records successful checks and explicit limitations.

## Input and visual limitations

Native screenshot capture returns black frames. A fresh window binding still
provides a readable accessibility tree, but does not fix pixels. Subsequent
composer-focus input fails with `failed to activate captured window`; after one
fresh binding and retry, it fails with `GetCursorPos failed: Access denied
(0x80070005)`. No further clicks or key input are attempted. These errors are
recorded as an automation limitation, not established MossAgent input defects.

This establishes native typing, sending, streaming observation and EXE restart
state for one projectless conversation. It does not establish native screenshots,
typing another draft during streaming, stop-button input, graceful close,
attachments, project variants, submission-time cancellation or a kill during the
SQLite commit. Those branches retain their separate automated/headless/CLI
evidence and unfinished native status. No policy is loosened to make this pass.
