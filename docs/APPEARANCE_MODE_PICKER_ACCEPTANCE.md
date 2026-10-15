# Appearance mode picker acceptance

## Design self-check

1. App owns this rendered layout. One shared `AppThemeModePicker` replaces the
   appearance view's duplicated choice markup; no dependency direction changes.
2. Reuse the real `SelectThemeCommand`, selected-state properties, existing scene
   renderer and appearance persistence. No second theme service or state cache.
3. Only synthetic preferences change during native replay. Tool authorization,
   approval, cancellation, timeouts, outputs and JSON/JIT publishing stay intact.
4. New handwritten types each occupy their own small file; shared styles own
   button dimensions and selection appearance, not per-view visual overrides.
5. Actual rendered bounds cover light/dark and two widths; keyboard tests use the
   production command. Existing shell, focus, appearance and pixel gates remain.

## Reference and correction

Fresh Python passive capture finally obtains the real Codex Appearance page,
1920x1032 at 96 DPI, with System selected and the effective light palette. This
is a settings reference, unlike the earlier conversation captures. Codex receives
no automated input. Both production MossAgent captures use the same viewport,
effective theme and System selection; they are not entire-page equality claims.

- The old three-column previews have 174-DIP hit-area height and captions below.
  Four new geometry cases fail before the product edit, in both themes/widths.
- The shared picker places three equal 80x60 buttons in one compact Mode row,
  separated by 16 DIP. Selection uses the blue outline; existing native button
  focus, commands, names and tooltips are retained. New labels use resources.
- The existing 248x174 scene is scaled inside each button; it is not deleted or
  recreated as a screenshot asset. Unused large-choice styles and view markup
  are removed. This intentionally does not yet redraw the thumbnail contents.
- Native blue-pixel extents independently measure 80x60 for Codex and MossAgent.
  Their page positions differ: reference (1186,242), candidate (1208,221). The
  full-page width, title, palette organization and advanced disclosure still
  differ. They remain work, not claimed fixed by matching button dimensions.

## Verification record

The first candidate's invalid `BoxShadow="None"` is caught by loading shared
styles (28 failures / 19 passes); the invalid override is removed, retaining the
existing shared surface shadow. The next 480-case group has six failures because
the old focus scenario assumes the first accent selector remains offscreen after
compact resize. With the shorter picker it is correctly visible. The scenario now
targets a UI-font selector that is verified visible before resize and offscreen
after it, preserving all dismissal and no-scroll assertions. No focus policy is
weakened or changed. The final related run passes 480 cases with no skips/failures.
The first full App run also encounters the old literal section-heading guard;
it is updated to check the new localized Visual Style resource binding while
retaining its shared typography/margin assertions. An attempted rebuild before
that test process exits fails with MSB3027/MSB3021 on the test apphost. Its log
is retained separately; it is not reported as a clean build or a product defect.
That complete run finishes with 4,394 passes and the one heading-guard failure,
not an all-green checkpoint. The repaired guard is followed by a separate serial
complete run; historical related-group identities are retained independently.

The production candidate is built separately under `.scratch/appearance-mode-20261014`;
it does not replace the default launch output. App SHA-256 is
`F83A6B6B10AE0B76373A93DC7DAC5F92FE974E7342B411BBF27AC35469D04FC4`.
Ordinary-permission native replay verifies System/light and Dark at 1920x1032,
then Dark and Light at the actual 526x601 minimum through a native grip resize.
All choices fit and the selected outline follows the clicked button. Synthetic
preferences return to Light; model requests and saved database rows do not change.

External evidence, scripts and synthetic data stay outside Git:
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-child-settings-runtime-20261014`.
`appearance-comparison.png` contains unresized source crops, with reference,
before and after clearly labeled. `appearance-measurements.json` records the
native selection extents. These crops do not establish full-page pixel parity.

## Separate child-settings probe

Before this UI change, the preceding production App identity `A3B7F0C...` edits
one synthetic child through the real settings page: name, trigger, prompt, Gemini
model and high effort. The invalid empty-name save preserves all nineteen table
hashes; explicit repair succeeds. Sharing remains off and the seeded task retains
AskEveryTime throughout. The subsequent real parent request contains the saved
name and trigger. Its child tool waits for manual approval and sends no child
request. No approval is automated or permission policy raised. Without a user
approval reply, normal Stop cancels the request: `tool_cancelled`, no granted
approval, task Cancelled, exactly one parent request and zero child requests.

This verifies settings persistence, catalog publication, approval gating and Stop,
not a successful current-version child run. The newer picker App reopens that
history without a new model request. A fresh successful settings-to-child run
still requires manual approval. The fresh earlier focused App and Agent probes
pass 164 and 119 cases respectively; they are not promoted to native success.

## Final checkpoint

The repaired complete serial App run passes all 4,395 cases with zero failures,
skips or aborts. The complete pixel suite passes 84 and the fresh candidate Agent
suite passes 119: 4,479 UI cases and 4,598 across these three assemblies. The
480-case related run is a subset, not additional coverage in that total; its
pre-heading-guard assembly identity remains recorded separately. Five other
non-UI suites are not rerun for this App-only change, and no historical complete
solution test count is promoted to this checkpoint.

The final solution build succeeds with zero warnings/errors after the locked
driver exits. Scoped formatting and all eighteen explicit source/document paths'
whitespace checks pass, including untracked files; the index remains empty.
The native App exits normally with code zero. Independent Python and PowerShell
checks agree on final TRX counts and the production App identity. The external
`verified-checkpoint.json` also verifies native capture metadata, restored-Light
preferences, all nineteen unchanged table hashes, no artifacts, one parent/zero
child requests, the cancelled unapproved tool audit, and released processes/port.

The default launch output, user data, JIT/JSON behavior and packaging are not
changed. Nothing is staged, committed, pushed, released or installed. Full-page
Appearance fidelity, a fresh manually approved settings-to-child success and
the remaining product gates stay open; the overall goal remains active.
