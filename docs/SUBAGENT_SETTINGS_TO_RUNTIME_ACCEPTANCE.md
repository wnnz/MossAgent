# Subagent settings-to-runtime acceptance

## Design and scope

1. App owns the real settings editor, production composition and conversation
   presentation; Agent owns configured delegation through existing ports. This
   continuation adds repository-external acceptance assets, not a second executor.
2. Reuse the real configuration/workspace repositories, provider adapters and
   tool pipeline. Verify saved identities, trigger/prompt, selected model/effort,
   context sharing, actual child tools/results and restore without replay.
3. Use a new synthetic task created with FullAccess from the outset and a bounded
   loopback fixture. Do not alter the older AskEveryTime probe, its pending/cancelled
   history, any user data or any approval/privacy setting through UI automation.
   The older manual-approval branch remains separate evidence, not upgraded to a
   successful approved run. Only the new authorized synthetic evidence file is read.
4. Every external handwritten C# file has one type; production dependency, size,
   shared-control, JIT/JSON and Windows x64 packaging constraints remain unchanged.
5. Exercise actual settings edits/saves, native live/completed dialogue, sharing-off
   and sharing-on requests, persistence/audits, normal close and no-replay restore.
   Compare bounded request flags and hashes, never credentials or raw requests.
   Existing deterministic negative authorization, approval, configuration, lifetime
   and cancellation tests remain required alongside this successful native branch.

Evidence root:
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-child-settings-success-20261014`.
Current work is not whole-product, all-provider, DPI or Codex pixel-parity sign-off.

## Candidate and fresh checks

The unchanged production source is built into the separate
`.scratch/child-settings-success-20261014` artifact directory. The native App DLL
SHA-256 is
`705ADF5880D564496E0C7B66FFB2E6FEABB65F4B08FF01239735530F6B781CB9`.
This is not silently identified as the earlier Appearance-selector binary.
`native-assembly-identities.json` records the production dependency identities;
`final-verification.json` checks them again and records current test DLL identities
and matching App/Agent dependencies.

- The solution build succeeds with zero warnings and zero errors.
- The fresh App subset passes 152 cases, selected by
  `FullyQualifiedName~Subagent|FullyQualifiedName~ModelReasoning|FullyQualifiedName~ConfigurationMutation`.
- The fresh complete Agent suite passes 119 cases. Both TRX files have zero failed
  or unexecuted tests: 271 fresh cases, not another complete product-suite run.
- The preceding Appearance-selector checkpoint's 6,050 complete-suite cases
  remain historical evidence; they are not counted as rerun in this continuation.
- No production implementation or repository test source is changed in this
  acceptance slice. It adds this record and updates the product matrix; seed,
  fixture, capture, launch, snapshot and verification scripts remain outside Git.

## Real settings to production execution

The isolated synthetic task is created with FullAccess before App launch; its
policy remains unchanged throughout. The production App, configuration stores,
Responses parent adapter, Gemini child adapter and execution pipeline are used.
The server is a bounded local deterministic protocol fixture, not a paid Gemini
or OpenAI service and not a second agent executor.

Actual editor interaction changes and saves the child name to `设置贯通审查`,
trigger to a condition containing `SETTINGS_TRIGGER_V2`, system prompt to
`SETTINGS_CHILD_SYSTEM_V2`, and model to `gemini-3-pro-preview`. Selecting that
model exposes the unsupported previous medium effort; explicitly selecting high
and saving produces the expected supported configuration. The enabled child
identity and selected provider/model identities persist rather than being
replaced with another seeded profile.

Two actual composer sends run in the same native App:

1. With sharing disabled, parent requests publish the new name/trigger. Both child
   requests use the newly saved prompt, selected model and Gemini
   `thinkingLevel: HIGH`, without the parent-only context marker or old prompt.
2. The real sharing checkbox is enabled and saved. A second actual send produces
   another delegation/read/result cycle. Both subsequent child requests carry
   the parent context marker, with the same model, HIGH effort and new prompt.

Each child performs a new authorized `filesystem.read_file` through the production
pipeline, displays its tool result and live partial response, and completes with
Markdown containing `SETTINGS-CHILD-ANSWER-OK` and the file marker. The parent
receives both the child answer and file result, then returns its final answer.
The sharing-on request can also contain earlier file/child markers from history;
the second successful file audit, not marker presence alone, proves the new read.

The allowlisted request log contains exactly four parent and four child requests.
Final persistence contains two successful `subagent.run` audits, two successful
file-read audits, two complete child dialogue payloads with actual tool content
and final answers, and two parent answers. No partial dialogue entries remain.
The task is Completed, all four audits have completed timestamps and no error
code, and there are zero artifact records. Fourteen of nineteen seed table hashes
remain unchanged; only child settings, messages, task state, execution audits and
conversation model preferences change. Both synthetic files retain their original
bytes, and the foreign-file marker never appears in the bounded request flags.

## Native captures and no-replay restore

Fifteen passive Python PrintWindow captures bind the exact expected App executable,
PID and HWND, reject black images, and record 96 DPI and ordinary permissions:

- `00`–`02`: initial task, old child editor and saved sharing-off settings.
- `03`–`05`: live sharing-off dialogue, completed parent, and scrolled final child.
- `06`–`09`: saved sharing-on settings, live dialogue, completed parent, and final
  child answer/tool content in the expanded timeline.
- `10`–`11`: restarted completed history and expanded/scrolled persisted child.
- `12`–`14`: restored 1280x820 and 980x640 windows, including compact historical
  child Markdown reached by actual scrolling. Other captures are 1920x1032.

The first App (PID 9008) exits normally with code zero. The same recorded candidate
restarts as PID 6768; the completed conversation is loaded without a send.
Expanding and scrolling the persisted timeline reveals the saved child answer.
All nineteen table hashes, request records, audits, settings and dialogue summaries
remain identical to the closed-before-restore snapshot, including after normal
close of the restarted App. Both native windows/processes disappear and both
stderr logs are empty. The fixture also exits zero with parent/child counts 4/4,
and its port is no longer listening. There is no model-call or tool replay.

`verify-final.py` independently checks the request parameters/sharing flags,
saved identities, completed audits/payloads, two normal exits, stable restore
snapshots, files, zero artifacts, non-black capture metadata, DLL hashes, fresh
TRX counters and released endpoint. `final-verification.json` reports success.

## Preserved limitations and diagnostic evidence

- The older AskEveryTime pending/cancelled probe is untouched. FullAccess's
  successful audit approval flag is not evidence of a user approving a prompt;
  the manual-approval success branch remains separate work.
- The fixture uses the same recorded tool-call IDs across two sequential runs;
  it proves those two completed cycles, not unique IDs or concurrent delegation.
- One Sky accessibility `set_value` attempt on the second composer reports a
  timeout while the draft changes; subsequent clicks do not send. Reobserving,
  focusing and using ordinary typed input produces exactly one second send.
  This is not diagnosed as a product bug or reported as a sending fix. Enter
  inserts a newline in this multiline composer and is not treated as send.
- Early snapshots immediately after releasing a held stream or before a send
  starts remain non-final evidence. `running-on-verified` and the completed
  snapshots, not their earlier similarly named probes, establish those states.
- The early `completed-off` verifier incorrectly expects a nonexistent dialogue
  ToolName field. Its corrected `completed-off-verified` snapshot checks Role and
  Content and confirms the stored file result; this was not data loss. The early
  settings-off JSON has a legacy Windows encoding, retained without rewriting
  and explicitly decoded by the final verifier.
- No real provider credentials, paid service, all-protocol native sweep,
  high-DPI/dark-theme matrix or same-state whole-window Codex parity is established
  by this bounded branch. The prior negative tests do not become fresh native
  rejection evidence. Whole-product acceptance remains active.
- Default launch output, user data, approval/privacy settings, packaging,
  JIT/JSON and unrelated dirty files are unchanged. Nothing is staged, committed,
  pushed, released or installed.
