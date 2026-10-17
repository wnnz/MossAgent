# Provider settings-to-runtime UI workflow acceptance

## Scope and design self-check (2026-10-16)

The preceding complete regression/entry audit made verified progress. This
follow-up checks business behavior beyond wiring, without treating the earlier
4,038 button occurrences as successful executions of every command.

1. App.Tests owns this deterministic acceptance coverage. No production module,
   dependency direction, configuration schema or UI appearance is changed.
2. Production MainWindow, composition, configuration/workspace ports, SQLite,
   provider discovery, streaming adapters and existing commands are reused.
   The loopback server supplies protocol responses, not a substitute agent runner.
3. Each case owns synthetic storage, a loopback listener and a 60-second linked
   cancellation deadline. The shared HTTP fixture reader limits headers to 16 KiB
   and bodies to 1 MiB. Request headers/credentials are not recorded; recorded
   paths omit queries. Plain chat grants no project roots or tool capabilities.
4. Four new handwritten C# files each have one type: 104/128/82/188 physical lines,
   maximum method sizes 36/15/28/67 respectively. Existing controls/styles are used
   without adding per-page appearance or new native controls.
5. Six connected cases verify success plus HTTP discovery failure, invalid model
   save, disabled-provider submission, cross-provider isolation and real response
   rendering. No user data, approval preference or existing window is accessed.

## Connected workflow

Each protocol runs once in actual Light at 1280x820 and once in actual Dark at
526x601: Responses, OpenAI-compatible Chat and Gemini GenerateContent. Each case
asserts the window's effective theme, not just its requested theme.

- The visible add-provider action opens the real dialog. Name, loopback URL and
  synthetic key are entered into bound inputs; saving uses Space on the actual
  footer button. Persisted protocol and explicit direct routing are verified.
- Expanding the correct provider reveals its real discovery action. Its first
  HTTP request returns 503 with a synthetic private string. The shared page
  header shows the sanitized error and no model is persisted.
- Space retries the same action. Production protocol discovery imports exactly
  one generative model; Gemini's embedding-only entry is excluded. The page
  leaves its error state, and imported image/file flags match the bundled catalog
  rather than assuming every known model supports every capability.
- The real model-row action opens its editor. Saving an empty ID preserves the
  stored model, keeps the dialog open and shows the editor error. Correcting it
  saves the same identity with 64,000 context, 2,048 maximum output, high reasoning,
  temperature 0.3 and Top-P 0.8. Image/file overrides become false/true.
- Reopening restores the edited name; real Escape dismisses the retained editor.
  Fetching again preserves the complete customized model instead of replacing it
  with catalog defaults or creating a duplicate.
- The real provider editor's enabled checkbox and save action disable the only
  available provider. After returning to chat, typed text cannot enable Send;
  Enter creates neither a task nor a streaming HTTP request.
- Re-enabling the provider restores the saved selected model/high effort. Enter
  sends exactly one plain-chat request through its actual protocol adapter.
  Recorded requests verify output limit, reasoning and sampling parameters;
  Gemini's model identity is checked in the request path.
- The task and exact user message persist as completed with a null project ID.
  The returned `hello` is rendered by MarkdownSelectableTextBlock and hit-tested
  on-screen, not merely found in storage or a hidden visual tree.
- Three discovery paths and one expected streaming path are checked. An unrelated
  disabled provider and its model remain unchanged. No functions are advertised:
  Gemini's empty function-declaration wrapper is checked semantically rather than
  mistaking its nonempty outer array for an available tool.

Text/reasoning entry, checkbox/action activation, Enter and Escape use Headless
keyboard input. Protocol selection and numeric edits use bound control properties;
settings navigation uses existing view-model commands. This is not a claim that
every field or navigation transition receives operating-system keyboard input.

## Validation and evidence

Evidence remains outside the repository at
`%TEMP%/MossAgent-provider-ui-workflow-20261016`.

- `workflow-visible.trx`: six connected cases pass. `related.trx`: all 217 related
  App cases pass, including those six. `architecture.trx`: all seven architecture
  cases pass. The final distinct total is 224, not 230; no failures/skips/aborts.
- `related-filter.txt` records the bounded regression scope. `solution-build.log`
  records zero warnings/errors; `format.log` comes from scoped whitespace
  verification without rewriting source.
- `hashes-before-final-checks.csv` and `hashes-after-final-checks.csv` match all six
  recorded tested/runtime DLL identities. Production and both tested App copies
  match the unchanged App binary from the earlier 6,495-case complete checkpoint.
- `source-size-audit.json` records Roslyn declaration/method-span checks. Its small
  external `source-audit/` project references SDK Roslyn, not production projects.
- Earlier workflow TRX/log files retain test-development errors: an overbroad
  assumption that GPT-5 supports files, nested control namescope lookup, Gemini's
  empty declaration wrapper, and using TextBlock.Text instead of the Markdown
  renderer. They are not confirmed production defects or product red baselines.
  The initial compile log also retains a wrong test-only provider property name.

## Remaining gates

No production change is justified by these results. The full eight-suite run and
pixel suite are not repeated after adding these six tests; the earlier complete
6,495 result is not enlarged arithmetically. Persisting context length does not
independently prove context-budget truncation, and capability overrides do not
exercise actual image/file requests. These cases do not cover every model, proxy,
paid API, concurrent configuration mutation, native DPI or arbitrary user data.

Native screenshots/existing-window operations remain user-postponed. Current
Codex/PI-Desktop visual comparison and whole-product interaction/visual acceptance
remain open. No JIT/JSON/Windows x64 packaging, user settings, staging, commit,
push or release changes are made; the full product goal remains active.
