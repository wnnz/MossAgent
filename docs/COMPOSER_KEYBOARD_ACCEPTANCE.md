# Composer keyboard submission acceptance

## Ownership and behavior

The App UI owns this change. `AppComposerInput` extends the shared `AppInput`
wrapper and preserves TextBox templates, editing, wrapping, scrollbars and the
existing IME client. It adds no provider, tool, repository or operating-system
access and introduces no new dependency direction or approval path.

Only the focused, visible, enabled, attached and writable composer can submit.
Enter and Ctrl+Enter invoke the existing primary command only when submission
is enabled, the draft is nonblank, the command can execute and no preedit text
is active. `WorkspaceComposerView` disables submission throughout preparation
and execution, including the interval before an active run exists. The explicit
send/stop button retains its original behavior and approval policy.

Shift+Enter retains native newline editing. Other modified Enter gestures do
not submit. A key-down latch prevents repeated submission while Enter remains
held, including when eligibility becomes available or modifiers change during
that hold. Release, lost focus and visual detach reset the latch. There are no
external subscriptions or mutable global state.

The old chat-wide Ctrl+Enter send/stop binding is removed deliberately: the
shortcut is now scoped to the composer and never stops an active task. Focus
elsewhere in the chat or settings cannot submit its draft. Localized tooltip
and shortcut-help labels describe sending and newline separately.

## Regression evidence

The unchanged baseline fails four of seven rendered integration cases: ordinary
Enter fails to submit in both themes/sizes, plain Enter changes the running
draft, and Ctrl+Enter cancels the active run. Ctrl+Enter idle submission and
Shift+Enter newline are the three already-passing baseline cases.

The final focused run passes 175 cases, including all 32 new cases. New tests
cover real MainWindow input, loopback Responses requests and persisted messages;
both themes and wide/compact windows; pending-save and active-run draft retention;
focus routing; repeat/modifier transitions; blank, readonly, disabled, hidden,
unfocused, detached and unavailable states; missing/disabled commands; and the
default TextBox IME client's preedit/commit/held-key sequence. Existing field,
wrapping, send-failure, editing and settings-shortcut cases remain enabled.

The IME case routes its active-preedit key event directly, then uses normal
headless text/key input after commit. Rendering Avalonia's static preedit
underline across independent headless UI threads initially raises a framework
thread-ownership error; the test avoids that unrelated renderer path rather
than changing production IME behavior. This checks routing, not native Windows
IME timing, candidate selection or preedit painting.

Two old test assumptions are corrected without weakening geometry: the source
guard now recognizes `AppComposerInput`, and the outside-composer routing case
focuses the chat header directly instead of looking for a deliberately
non-focusable header button. The initial focused 173/175 run is retained
separately; it is not reported as all-green.

## Native production composition

External evidence and Python helpers, kept outside the repository, are under
`%TEMP%\MossAgent-composer-20261015`. The native candidate App SHA256 is
`64E32C47659F391815866F2775C46EAC16F4F609E8A6F81F2762782677911D5A`.
It matches the production App used by both UI test assemblies.

A fresh synthetic database contains one authorized, empty project and one
loopback-only provider/model. Its observer returns only text events, never tool
calls. No actual account, external API, browser, command or security setting is
used. The ordinary-privilege native App is the normal Debug/JIT build.

Eleven accepted Python PrintWindow captures cover Light/96-DPI maximized
1920x1032, restored 1280x820 and minimum 526x601 windows. Real native keyboard
and pointer input verifies:

- Shift+Enter leaves a two-line unsent draft with zero requests.
- Enter sends exactly one multiline request and displays the running state.
- Enter and Ctrl+Enter during that run preserve the next draft and connection.
- The first reply completes normally and leaves the next draft intact.
- Ctrl+Enter then sends that draft as the second request.
- Clicking Stop disconnects only that second request and preserves a new draft.
- The minimum-width shortcut flyout displays both send and newline instructions.

Independent `verify-native.py` checks all eleven nonblack, correctly sized,
identity-bound captures, both normal zero exits, clean stderr, exactly two
whitelisted requests and exact persisted message contents. Only tasks, messages
and conversation model preferences change; the other sixteen tables and exact
UI-preference bytes remain unchanged. There are zero tools/artifacts and no
project-file changes. The final task is cancelled, the completed first reply is
retained, and the unsent final draft is absent from stored messages.

The first capture attempt expected restored geometry but found a maximized
window. It fails explicitly and yields no accepted image; the subsequent capture
uses the observed actual bounds. Native automation also exposes lagging UIA
values during layout changes; passive pixels, observer signals and persisted
data establish results rather than stale accessibility text.

## Complete validation

The complete solution run passes 6,175/6,175 cases across eight assemblies: App
4,530, visual 102, Agent 119, Browser 76, Infrastructure 199, Windows 142,
Providers 516 and Tools 491. There are no failures, skipped or aborted cases.
The App assembly finishes normally after 12m30s; its quiet interval is not
treated as a reason to interrupt/restart it. The 175-case focused subset is
already included in the total, not added to it.

The solution build has zero warnings/errors. Scoped whitespace verification,
one-type/file/line-limit checks, AXAML/resource parsing and scoped diff checks
pass. `verify-complete.py` independently checks all eight TRX files, unique
test identities, counters and the native/source audit records. Native captures
and both UI test assemblies share the recorded production App identity.

After that full run, a whitespace-only line split in the preparation test is
rebuilt and its two cases pass again. The full-run test assembly hash begins
`4F99205E1498`; the rebuilt test assembly begins `A51BF2871349`. The full-run
record preserves the former identity rather than attributing its results to the
later binary. The production App hash is unchanged, and these two rerun cases
are not added to the 6,175-case total.

## Remaining scope

The fresh passive Codex reference is still a 481x601 conversation beside a
preview, not an equivalent maximized conversation. It does not certify current
whole-window pixel parity. Native Dark/high-DPI/real IME interaction and the
remaining sidebar, inspector and appearance fidelity gates remain separate work.
No AOT, JSON implementation, packaging, real user data or approval configuration
changes are made. Nothing is staged, committed, pushed, released or installed;
the overall product objective stays active.
