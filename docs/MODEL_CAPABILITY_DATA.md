# Model capability defaults and editable reasoning

## Behavior

- Model, Agent and Subagent settings share `AppEditableChoice`: type a value,
  choose from its menu, or cycle with Up/Down. F4 and Alt+Down open the menu.
  Refreshing choices preserves the draft; closing the view closes its popup.
- OpenAI Responses and compatible Chat accept custom effort tokens of 1–64 ASCII
  letters, digits, underscores and hyphens, starting with a letter. Save trims
  and lowercases the value. Recommendations are not a whitelist for gateways;
  the upstream server may reject an unsupported custom value.
- Native Gemini keeps its level/budget translation and validation. Native
  Anthropic keeps named effort validation and the existing manual-budget presets
  for older models. Typing does not bypass protocol limits.
- Fetching models still uses the configured provider, proxy, timeout and
  cancellation. Only newly imported rows receive catalog defaults. Existing
  user values, names, enable/default flags and advanced JSON are untouched.
- Exact model IDs and explicit `provider/model-id` names match, ignoring case
  and surrounding whitespace. Native Gemini also accepts `models/` prefixes
  for capability lookup. Arbitrary suffixes, fuzzy matches and gateway aliases
  such as `claude-opus-4-6-thinking` are deliberately not inferred.
- Unknown models keep the existing conservative defaults: 128,000 context,
  8,192 output, image/file flags off, and the protocol's default effort.
- Output defaults cannot exceed the editor's 1,000,000-token cap or consume the
  entire context window (at least 256 tokens remain). The source limits remain
  unchanged in the data file. Defaults remain editable.

## Data and refresh

`src/MossAgent.Application/Data/model-capabilities.json` is a versioned snapshot
of [models.dev](https://models.dev/api.json), a community-maintained catalog,
retrieved on 2026-10-15. It includes 168 tool-capable text-output models from
OpenAI, Anthropic, Google, DeepSeek, Moonshot, Z.ai, xAI, Alibaba and MiniMax.
The file records its schema version, retrieval timestamp and source SHA-256.
These are offline recommendations, not a guarantee of gateway/account support.

- `supportsImages`: the source explicitly lists `image` input.
- `supportsFiles`: the source explicitly lists `pdf` input. This deliberately
  does not equate the broad `attachment` flag with arbitrary file support;
  a false value means PDF input was not declared in this source. For example,
  models.dev does not list PDF input for `gpt-5`, even though the
  [official OpenAI model page](https://developers.openai.com/api/docs/models/gpt-5)
  advertises file uploads. This conservative flag can be corrected manually.
- `reasoningEfforts`: explicit `reasoning_options` entries of type `effort`.
  Budget-only or toggle-only models have no inferred named efforts. Native
  adapters may expose existing translated budget presets separately.
- Realtime/live, dedicated computer-use and deep-research models are excluded.
  Capabilities describe upstream models; enabling a flag does not implement a
  new attachment transport in MossAgent.

To refresh the snapshot during development (PowerShell 7):

```powershell
pwsh -NoProfile -File scripts/Update-ModelCapabilityData.ps1
```

Review the data diff and run the focused tests before building/releasing.
`-OutputPath` supports inspecting a candidate outside the checkout. Download
failure leaves the previous file intact. The script has a 30-second request
timeout and rejects oversized/incomplete responses. It never uses API keys.
The JSON is embedded in the Application assembly, adding no loose release files
or startup network requests and leaving JIT/single-file publishing unchanged.

## Engineering self-check

1. Application owns pure model defaults and immutable lookups; App owns editor
   interaction and imports through the existing repository/discovery ports.
   No UI filesystem/HTTP access or new dependency direction is introduced.
2. No runtime port is needed for an embedded immutable resource. Remote refresh
   is an explicit maintenance script, not a background service or agent tool.
3. Existing discovery authorization, timeout, cancellation and bounded response
   handling remain unchanged. No credentials are sent to the metadata source.
4. New handwritten C# files each declare one type; existing file limits remain.
5. Tests cover typed/selected values, reopening, draft preservation, popup
   lifetime, protocol limits, exact matching, provenance, conservative unknown
   fallback, preserving existing models, all imported limits and request JSON.

Validation uses deterministic local fixtures and Avalonia Headless. It does not
establish native-window appearance or real upstream acceptance of custom values.
