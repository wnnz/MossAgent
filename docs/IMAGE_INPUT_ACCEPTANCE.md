# Image input: transport foundation

## Scope and sequence

The product target remains Codex Desktop functional/workflow parity. This is the
first image-input stage, not completion of multimodal attachments or the product.

1. Add immutable, bounded image envelopes to the Application model contract.
2. Encode native image blocks in all four existing provider protocols, preserving
   text, tools, Gemini continuation and advanced-parameter ownership.
3. Include image envelopes in context budgeting and verify production transports.
4. Continue with actual file decoding, dimensions/animation limits, authorized
   selection, file-backed history/drafts, migrations and native interaction.

## Required design self-check

1. Application owns the in-memory request envelope and request validation;
   Providers owns wire JSON; Agent owns context selection. No new dependencies,
   filesystem access, UI ownership changes or Windows APIs are introduced.
2. Extend the existing ModelMessage/IModelProvider path. Do not create a parallel
   provider path or encode image bytes into ordinary message text.
3. Only explicit user messages may contain images. The selected model must declare
   image support; file support is a separate capability. Limits are 5 MiB per
   image, ten images/10 MiB of binary image data per request, and 20 MiB of final
   UTF-8 JSON for image requests. No URL fetch, upload, tools or permission grant
   is implied. Existing provider cancellation/timeout/HTTP behavior remains owned
   by its current pipeline. Local validation errors must not echo credentials,
   image bytes or caller-supplied media-type values.
4. Keep each handwritten C# file to one type, under 500 lines, with methods under
   80 lines. Split image JSON construction from existing request builders.
5. Verify immutable ownership, empty/oversized/unsupported envelopes, capability,
   roles, count/aggregate/body limits, all four wire schemas, text-only behavior,
   image-only messages, context eviction and intact latest images. Exercise real
   provider streaming entry points using synthetic credentials and local fixtures.

## Deliberate boundaries, not completion claims

- An image envelope checks its declared media type and bounded byte ownership;
  it is not a codec and does not prove that bytes decode to a valid static image.
  The UI must not accept binary files until a decoder enforces real type,
  dimensions, decoded-pixel and animation policies. PNG, JPEG, GIF and WebP are
  transport media types, not a claim of complete file-format acceptance.
- Base64 is immutable, excluded from ordinary JSON persistence/diagnostics, and
  included only by explicit provider content-block builders. SQLite still stores
  text attachment metadata only; this stage adds no schema or binary persistence.
- Until codec dimensions and model-specific image accounting are available,
  context selection conservatively charges the encoded character count plus
  envelope overhead, using Agent's existing character-budget units. It never
  slices Base64 to fit. Large images may therefore be rejected conservatively.
- Shared subagent context remains textual and explicitly marks images as omitted;
  it must not silently imply that a child model has received the pixels.
- Authorized image selection, safe decoding, sent/unsent snapshot recovery, PDF,
  model-service acceptance, native EXE interaction and full regression remain
  incomplete. Do not enable image picking based on transport tests alone.

## Protocol references

Read on 2026-10-18; no model/default changes are implied by documentation examples.

- OpenAI: https://developers.openai.com/api/docs/guides/images-vision
  Responses input_image/image_url data URLs; Chat Completions image_url blocks.
- Anthropic: https://platform.claude.com/docs/en/build-with-claude/vision
  image/source with type=base64, media_type and data.
- Gemini: https://ai.google.dev/api/generate-content
  contents/parts with inlineData, mimeType and Base64 data. This application uses
  generateContent, not the separate Interactions API shown by newer guides.

The OpenAI vision guide was retrieved and confirms the Responses example. The
separate Chat endpoint reference returned a generic landing page/404 in this run;
its compatible content-block schema has local fixture/loopback coverage, not
fresh endpoint-reference or paid-service acceptance evidence.

## Verification (2026-10-18)

Candidate root: `artifacts/image-transport-20261018`. All commands use this
independent artifacts path; no existing user window, credentials or paid API was
used. Final sessions completed successfully; this is not full-solution regression
or native EXE validation.

| Check | Current result |
| --- | --- |
| Full solution Debug build | Passed: 0 warnings, 0 errors |
| Providers complete test project | 600/600 passed; `results/providers-all-fixed/wzheng_WZ-MINIPC_2026-10-18_09_39_42_net10.0.trx` |
| Agent complete test project | 132/132 passed; `results/agent-all/wzheng_WZ-MINIPC_2026-10-18_09_39_54_net10.0.trx` |
| App attachment/composer/history/projectless/subagent regression subset | 115/115 passed; `results/app-regression/wzheng_WZ-MINIPC_2026-10-18_09_40_00_net10.0.trx` |
| Roslyn syntax/type/size check of all modified/untracked handwritten C# | 105 files passed; one type each, maximum file 494 lines, maximum method 58 lines |
| Tracked diff and untracked C#/Markdown whitespace | Passed |

The new tests include actual loopback HTTP through all four production streaming
providers with deterministic SSE replies and real one-pixel PNG bytes; this proves
request encoding and response handling, not remote image interpretation. Agent
tests retain the image identity over tool rounds and deny unsupported images
before contacting even a custom/unvalidated provider. Projectless image envelopes
do not enable tools or grant authorized roots. Ordinary serialization excludes
pixels; the envelope factory snapshots caller bytes and is not a JSON persistence
constructor. Shared-image concurrent builds own independent JSON trees.

Recorded failures are retained, not overwritten or reclassified as passes:

- The first focused run passed 56/57: System.Text.Json inspected the public Span
  constructor even during serialization. Replaced it with an explicit byte factory
  and a private constructor; serialization/privacy checks now pass.
- The initial complete Providers run passed 593/599: image validation plus presence
  detection added two history enumerations. Fused request-local validation into
  existing provider message passes, passing state by reference; the original
  Gemini enumeration assertions remain unchanged and now pass. A new image case
  also requires the original two-pass no-tool history behavior.
- An initial solution build caught xUnit2031 in a new test. The assertion now uses
  the predicate overload, without disabling analyzers.

Reproduction commands (use the same candidate artifacts path):

```powershell
dotnet build MossAgent.slnx --artifacts-path artifacts/image-transport-20261018
dotnet test tests/MossAgent.Providers.Tests/MossAgent.Providers.Tests.csproj --artifacts-path artifacts/image-transport-20261018 --no-build
dotnet test tests/MossAgent.Agent.Tests/MossAgent.Agent.Tests.csproj --artifacts-path artifacts/image-transport-20261018 --no-build
dotnet test tests/MossAgent.App.Tests/MossAgent.App.Tests.csproj --artifacts-path artifacts/image-transport-20261018 --no-build --filter 'FullyQualifiedName~WorkspaceAttachment|FullyQualifiedName~WorkspaceComposer|FullyQualifiedName~WorkspaceHistoryContext|FullyQualifiedName~Projectless|FullyQualifiedName~WorkspaceSendDraft|FullyQualifiedName~WorkspaceSubagent'
```

The older composer full-suite handle remains a separate, live candidate with
previously reported allocation failures. Its dependent attachment-history run is
still waiting. Neither is evidence for this image candidate, and neither was
restarted, terminated or overwritten due to silent polling. Full regression,
native interaction and the remaining product requirements stay open.
