# Queue recovery after process termination

Status: four controlled CLI cases pass against both the independent completion
candidate and the merged original checkout. This is OS process termination and production App DI, not Avalonia
window interaction, the production GUI executable, graceful close or termination
inside a database transaction.

## Harness and identity

The separate JIT console harness under
`artifacts/queue-runtime-workspace-20261019/artifacts/queue-crash-probe` references
existing compiled assemblies, without rebuilding or editing src/tests. It uses
generated data roots, synthetic provider credentials and a local HTTP/SSE server.
Each child is created hidden, readiness is tied to a committed boundary, and
its executable identity is checked before terminating only that owned process
tree. Parent and worker verify the actual loaded App/Providers hashes against
the supplied assembly directory. No user data, existing window or browser is used.

The final identity-checked, explicitly gated run is probe-gated.log. App hash:
F80E50D84A8CD890154AF3D40FBEE63730EB7187CD22E38F564942296543D7B0
Providers hash:
19469DA4B91227863F975A237BE299C215F912D4ACC29E495ADB00AABDAA31E5

## Established paths

Both project and projectless variants cover two boundaries:

| Termination boundary | Recovered state | Explicit recovery |
| --- | --- | --- |
| Entry committed while the first response is held | Interrupted task, one user message, one queue entry and the next draft retained; request count stays one | Queue Continue sends the entry once; task completes and the next draft remains |
| Queue dispatch committed and its real HTTP request is held | Interrupted task, two user messages, empty queue and the next draft retained; request count stays two | Queue Continue is unavailable; a manually entered new turn sends one new request without restoring or duplicating committed input |

Every final history contains crash-queued exactly once. The initial and restored
workspaces use production composition, repositories, Queue coordinator, agent and
provider adapter. Startup initialization marks interrupted execution without
replay. The first case's pending response is released only after the old process
exits, so cleanup cannot complete the old run instead of testing recovery.

The earlier two four-case runs also passed. The final run adds an explicit
enqueue acknowledgement before releasing the first response in dispatch cases,
so reaching the boundary does not depend on a fast enqueue winning a network
race. It retains loaded-assembly identity checks. Its generated SQLite/artifact directories
remain available for inspection. These four cases overlap workflow intent with
the xUnit reconstruction tests and are not added to their test count.

After integration, the harness was force rebuilt with ProbeApp pointing at the
original checkout's Debug App directory. All four cases pass again; the exact
record is artifacts/provider-stream-boundaries-20261019/queue-crash-probe.log.
Its parent and workers validated these loaded assembly hashes:

- App: 9FB7D34F02E3BE8756F0155EA82B1A87B10110694898F49AABA961F166C57D35
- Providers: BB27103F906F743C8D16BA7345BFE806E08FAF6DDB37C49E362E1466BA393051

The earlier independent hashes above are historical evidence, not the original
checkout's assembly identities. No src/tests source or original runtime output
was rebuilt by the harness build itself.

After the later dispatch-authorization integration, the original-assembly harness
was force rebuilt again and passes all four cases. Its current record is
artifacts/followup-dispatch-integration-20261019/crash-probe.log. Verified hashes:

- App: 22BD6CDF717B9CE31C83725C0AC5BB49F1F909F1A1A3861BBFDF5F1A03230CE1
- Providers: 69548380962D9C1E9A7DF998E16D51BB4F0AE07B859A69E9E8BC823EE2CAEEF3

Earlier hashes/results remain historical checkpoints; the validation records do
not establish native GUI restart or termination inside a transaction.

## Remaining limits

No native picker, keyboard/window, screenshot, real provider, browser/process tool
or external side effect participates. No mid-commit atomicity or crash-before-
draft-flush guarantee is established. Native application restart and other
Queue dispatch races remain open; command filesystem isolation is unchanged.
