# Conversation preference and selection cleanup acceptance

## Scope and ownership

- `WorkspaceConversationPreferences` coordinates per-conversation selection through
  an Application repository port. SQLite lives in Infrastructure, not the UI.
- `WorkspaceAgentPickerViewModel` shares `SettingsOperationLifetime` with model and
  preference reads rather than duplicating linked cancellation and deadline logic.
- Preference write completion shares one current-context guard and notice projection.
  Background-run startup is extracted from the near-limit workspace view model.
- Preferred Agent identity is independent of the currently available profile snapshot.
  Missing or disabled selections never turn silently into the default Agent.
- Agent-default reasoning is persisted separately from explicit user reasoning.
  Configuration updates affect subsequent turns, never an already captured request.
- Windows x64, ordinary JIT debugging and single-file publishing remain unchanged.
  No AOT or JSON source-generation changes are introduced.

## Storage and lifetime boundaries

The transactional version-eleven migration adds `conversation_model_preferences`.
It preserves existing task metadata and message history. Preferences contain only
provider/model/Agent identities, canonical reasoning values and the default-source
flag. Agent defaults require an Agent identity and no explicit reasoning override;
both repository validation and SQLite constraints reject contradictory values.
Deleting provider, model or Agent configurations does not delete preferred identity.
Archived and missing tasks reject new preference writes.

Conversation reads have the existing 30-second deadline, cancellation and generation
checks. Late results cannot overwrite another conversation or an explicit selection.
Loading and failed reads block sending without clearing drafts; an explicit valid
choice can recover. Saves are serialized and coalesced per task. Normal shutdown
drains owned writes; noncooperative writes retain ownership until storage returns.
Failures show localized guidance without exception text. Explicit refresh retries
failed writes without silently replacing the current choice.

## Regression coverage

The first complete regression run exposed eight failures. They covered disabled
Agent identity, next-turn Agent defaults, unavailable composer presentation, pending
task mutation readiness and disabled-model fallback. All eight required correction;
they were not treated as unrelated failures or weakened by removing assertions.
Task-mutation tests now await conversation preference readiness before asserting
their original target-specific send guard.

Focused checks cover:

- Independent conversation choices, stale reads, explicit-choice precedence,
  unavailable identity retention, safe failures and persistence retry.
- Read deadlines, caller cancellation, disposal and noncooperative write ownership.
- Serial/coalesced writes and preservation of existing task metadata and history.
- Agent-default and explicit-override round trips and contradictory-source rejection.
- Missing-Agent recovery, configuration refresh, task switching and actual
  settings-to-loopback request routing without paid provider calls.
- Fresh service-container restart after choosing a model/effort without sending
  another message, including Agent defaults and explicit Agent overrides.

The combined focused App run passed 82 tests before adding the additional
implicit-choice/disabled-model switching regression. The final full-solution run
passed all 5,058 tests across eight assemblies: App 3,576, Infrastructure 196,
Providers 499, Agent 83, Browser 76, Tools 483, Visual 8 and Windows 137. No tests
failed or were skipped. The final additional regression is included in that run.

Whitespace verification passed for the 23 affected C# files. Roslyn checks found
exactly one type per file, no syntax errors, no file over 500 lines and no method
over 80 lines. The largest file remains the workspace view model at 483 lines;
the preference coordinator is 221 lines. Existing unrelated changes are preserved
and no files are staged, committed or pushed.

Evidence is under the temporary root listed below: `solution-final-tests.log`,
the eight separately named files in `full-suite-final`, `source-final-audit.log`
and `format-final.log`. An attempted concurrent build encountered the running
test host's DLL lock; it is not a source compilation failure. Final build evidence
is recorded separately after the test host exits: `solution-final-build-clean.log`
confirms a successful complete solution build with zero warnings and zero errors.

## Native restart check

On 2026-10-11 the external native QA host ran with ordinary permissions
(`QA_ELEVATED=False`), an explicitly isolated temporary data directory and synthetic
configuration. The existing saved maximized window state produced a 1920x1032
light-theme window at render scaling 1.

Using the real model menu and reasoning slider, select the Responses
`fixture-model` and high reasoning, then close normally without sending a message.
Restart the native host and select the same stored conversation. The restored model
profile identity, `selectedReasoning=高`, `effectiveReasoning=high` and
`canSendWithSelectedModel=true` matched the pre-close state. The original 12 messages
and Completed task status were unchanged. Both host processes exited normally
(`QA_UI_EXIT=0`).

Passive Python captures and whitelisted observations are outside the repository:

```text
C:\Users\wzheng\AppData\Local\Temp\MossAgent-conversation-preferences-20261011
  native-preference-before-restart.png
  native-preference-after-restart.png
  native-preference-before-restart.json
  native-preference-after-restart.json
  native-preference-first.log
  native-preference-restarted.log
```

This native check verifies model/effort persistence, not custom-Agent native menus,
all DPI/theme combinations, real provider accounts or full Codex pixel parity.
Custom-Agent default and override restart behavior has deterministic integration
coverage. The overall product goal remains active.
