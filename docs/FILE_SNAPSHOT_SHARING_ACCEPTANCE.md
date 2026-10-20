# Windows snapshot-reader and atomic-edit coexistence

## Confirmed defect

`FileSnapshotReader.Open` was opening internal read handles with `FileShare.Read`
only. On Windows, a still-open internal snapshot/preview handle therefore blocked
`File.Replace` in the atomic writer. The standard tool pipeline returned
`tool_failed` even though the task was authorized and the file version matched.

Two deterministic write/replace cases hold the production snapshot reader open
while invoking the real tools through `FileEditingFixture.ExecuteAsync`. Both
fail with the original production reader. The original baseline is retained at
`artifacts/snapshot-sharing-20261018/results/baseline` and its accompanying log.

That baseline also contains an initial external-save control using
`File.Move(overwrite: true)`. On this Windows host it fails even after adding
delete sharing, so it is not evidence about `File.Replace`. The final external-
save control explicitly uses `File.Replace`, the primitive this change supports.
The intermediate failed test runs are preserved, not relabeled as passes.

This independently proves a real internal reader/atomic-writer conflict. It does
**not** prove that it caused the two older full-App authorization failures:
those reports have no captured exception stack identifying their direct cause.

## Ownership and safety review

Tools.BuiltIn owns the change. The existing reader, writer and shared execution
pipeline suffice; no application port, UI access, dependency or platform API is
added. The read handle now uses `FileShare.Read | FileShare.Delete`, not
`FileShare.Write`. Atomic replacement can publish a new version while the held
handle continues reading the original bytes. In-place mutation remains blocked
on Windows, and hash validation still observes the current path before editing.

Authorized-path validation, risk/approval checks, task-owned version caches,
timeouts, cancellation, staging, history bounds and audit/output handling are
unchanged. External handles explicitly denying delete sharing still reject the
commit; no retries or forced unlocks bypass that restriction. Existing
`AtomicTextFileWriterTests.TargetLockRejectsCommitWithoutTruncatingOriginal`
continues to cover it.

Two history-cleanup tests previously used the internal reader to manufacture
an external commit lock. They now create an explicit `FileStream` with
`FileShare.Read`. Their exception, original-byte, retained-history, uncommitted-
snapshot cleanup and temporary-file assertions remain unchanged. The first
full-tools run exposed these outdated fixture assumptions (497 passes / 2
failures); its report is retained under `results/all-tools`.

New tests stay in one type/file and cover:

- Both write and text replacement with an open snapshot, exact old/new bytes,
  bounded history enabled and advancement of the task's observed version.
- Windows refusal of in-place write access while the snapshot is open.
- An external atomic replacement followed by rejection of a stale task write;
  the original handle still returns the original bytes.
- A task with no authorized roots being denied despite an already-open handle.

## Validation and remaining scope

The isolated final candidate is `artifacts/snapshot-sharing-fixed-20261018`.

- `results/focused-tools-final`: **56/56 passed**, no skips.
- `results/all-tools-final`: **499/499 passed**, no skips, including existing
  traversal, links, authorization, approvals, cancellation, concurrent edits,
  external locks, history cleanup, preview and schema coverage.
- `results/expanded-app`: **113/113 passed**, no skips; includes all eight
  production-composition authorization cases, model initialization/reloads,
  independent draft/background persistence and isolated allocation controls.
- Solution build completed with **0 warnings and 0 errors**; log is the sibling
  `artifacts/snapshot-sharing-fixed-build-final-20261018.log`.
- `source-shape.json`: **157** changed/untracked handwritten C# files, one type
  each, maximum 499 file lines / 67 method lines, no syntax/shape violations.

These expanded App checks are not a complete solution regression. The older
frozen full run was still executing at 13:19 +08:00, with its original reported
failures; this latest candidate has not had a complete regression yet. This is
deterministic Windows filesystem/pipeline
evidence, not a native preview-click or real-provider test. No normal user window,
credentials, approval settings, AOT/trimming, release, commit or push is changed.

The old frozen full run has since ended: all eight authorization cases pass,
before this reader change, while model initialization and allocation are its
only two App failures. Therefore this fix remains an independently reproduced
reader/edit defect, not a claimed explanation of the older authorization reports.
The later thumbnail candidate includes it in ordinary full session 95889; that
run's final result must be checked independently.
