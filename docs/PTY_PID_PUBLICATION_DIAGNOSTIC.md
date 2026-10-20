# ConPTY test PID publication diagnostic

Status: candidate test-fixture repair verified independently, not yet applied to
the repository fixture or accepted as a production terminal change.

The complete `conversation-followup-storage-20261019` run reports one failure in
`StopOrDisposalReapsRealShellAndItsBackgroundChild(dispose: true)`: `BindAsync`
cannot read `child.pid` because another process has it open. This occurs before
the process-tree-reaping assertions. The fixture already writes a temporary PID
file and renames it, but its reader waits only for destination existence. That is
not an explicit completion handshake with the publishing call. The original
failure does not by itself identify which process held the file or establish a
production process-tree-reaping defect.

## Controlled reproduction and candidate

`artifacts/pty-pid-handshake-20261019/PidHandshakeProbe.csproj` links the current
fixture, cleanup helper and original lifecycle test source. Raw references use
the already-built production assemblies; the diagnostic does not rebuild or
modify production source/test binaries. It creates only owned temporary roots
and ConPTY shells through the existing full-access tool execution path.

`PidPublicationTests` holds an owned PID file with FileShare.None, containing the
identity of its own shell. Before the publisher finishes, the current fixture
immediately attempts to read it and throws the same IOException. The expected
failure is in `results/original-locked-publisher.trx` (0 passed, 1 failed); it is
retained as a reproduction, not counted as a successful regression.

An artifact-only copy of the fixture makes two changes: publish `name.ready`
after WriteAllText/Move return, and wait for that marker before opening the PID
file. The new test verifies binding remains pending while the publisher owns the
file, then binds the same shell after release and verifies real process exit.
All four original lifecycle cases retain their process ownership, child cleanup,
natural-exit/restart and independent-task assertions. The candidate passes all
five checks in `results/candidate-native-lifecycle.trx`. There is no exception
retry, arbitrary additional sleep or relaxed assertion. Hashes of original and
candidate fixture are recorded in `candidate-identity.json`.

This demonstrates and repairs the reader/publisher protocol gap under a controlled
lock. It does not prove that every historical sharing violation has that cause.
The repository fixture remains unchanged while the original full run finishes;
afterward apply the bounded change and new test, run the real Platform.Windows
project and rebuild. Preserve the original full-run failure and distinguish the
subsequent fixed candidate. This diagnostic is real ConPTY/process automation,
not GUI/screenshot or strict filesystem-sandbox acceptance.

## Applied after the frozen full run completed

The original frozen run finished with 8,053 passed and one failed out of 8,054.
App contributed 5,391 passing checks. Its 2,379 source/configuration files and
2,035 Debug runtime files had no before/after hash differences. The failed TRX
is retained; this result is not represented as an all-pass run.

The repository fixture hash was checked against `candidate-identity.json` before
copying the two-line readiness handshake. `PtyPidPublicationTests` now includes
the independently reproduced locked-publisher scenario in the actual Windows
test project. The complete project then passed 147/147 in Debug, including real
ConPTY, shell/child reaping, task isolation and the publication test; see
`artifacts/pty-pid-handshake-20261019/repository-results`. No production terminal
code or approval policy changed. Solution build and later product changes have
their own candidate identities and must be reported separately.
