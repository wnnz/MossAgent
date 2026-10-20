# Image attachment workflow

This checkpoint predates projectless attachment selection. The subsequent
implementation, version-17 migration and current evidence are recorded in
`PROJECTLESS_ATTACHMENTS_ACCEPTANCE.md`; the results below retain their original
candidate and scope.

The full functional-parity goal remains active. This extends the transport stage
in `IMAGE_INPUT_ACCEPTANCE.md`; neither document defines a smaller product goal.

## Plan and required self-check

1. Domain owns an immutable image snapshot. Application owns decoder contracts
   and attachment/request limits; Infrastructure implements bounded Skia decoding,
   authorized file reads and snapshots. App only coordinates ports and displays
   localized feedback; it does not access image files or native codecs directly.
2. Reuse the existing attachment reader/store, draft CAS repository, composer and
   history builder. Keep text snapshots and their old paths valid; images become
   native ModelMessage image blocks, never Base64 inside untrusted text context.
3. Retain project-authorized paths and per-selection identity, cancellation,
   thirty-second storage/read limits and task-owned execution. Enforce real media
   type, static frames, dimensions/pixels, encoded bytes, aggregate image bytes,
   count and selected-model capability. Invalid or missing restored snapshots
   remain removable unavailable chips and cannot be sent. No tool permission is
   granted by selecting an image. Source files are not read again after selection.
4. Keep one handwritten type per file, files <=500 lines and methods <=80 lines.
   Add a forward metadata-only SQLite migration; never place pixels/Base64 in SQL.
5. Test actual codecs/files/SQLite and independent production composition reopen,
   original-source deletion, changed metadata/hash, bad/animated/oversized images,
   unauthorized roots/links, cancellation, text regression, incompatible models,
   draft switching/promotion, native wire blocks and no binary text context.

SkiaSharp is pinned to the version already used by Avalonia (3.119.4), with the
Windows native-assets package. This is a platform-neutral codec API, not a new
Windows API in Application/Domain. No System.Drawing or new OS dependency is put
in those modules. Packaging and native/library resolution still require checks.

## Verification

Current candidate: `artifacts/image-workflow-20261018`, Debug/JIT, 2026-10-18.

| Gate | Current authoritative evidence |
| --- | --- |
| Real codecs | `SkiaAttachmentImageDecoderTests`: four static formats; real animated GIF/APNG/WebP rejected; signatures/truncation, dimensions/pixels, encoded bytes, cancellation and immutable/non-diagnostic snapshots |
| Authorized selection | `WorkspaceImageAttachmentReaderTests`: actual magic rather than filename, multiple authorized roots, sibling traversal and fake image rejection, exact 5 MiB/10 MiB boundaries, missing decoder and cancellation; existing text/link/selection-isolation cases remain in regression |
| Submitted snapshots | `ImageAttachmentPersistenceTests`: four formats survive original-file deletion and SQLite metadata reload; real binary bytes/hash; missing, length, hash, MIME and decoded-content damage reject; forged envelopes are actually decoded before publication |
| Draft snapshots | `ImageDraftAttachmentTests`: four formats reopen with no original source; stale CAS and same-ID replacement cannot overwrite saved bytes; missing/corrupt/retyped images retain unavailable removable image metadata, not empty text |
| Forward migration | `ImageAttachmentMigrationTests`: actual v15-shaped tables with sent/draft text snapshots survive v16 upgrade; synthetic final-version failure rolls back both tables, retry/idempotency succeeds, cancellation preserves v15, MIME/size constraints and cascades remain enforced |
| Production workflow | `WorkspaceImageAttachmentWorkflowTests`: real model-editor capability save, production reader/Skia/store/SQLite, project/projectless navigation isolation, independent composition reopen of unsent/submitted images after source deletion, empty-body image send and later native Responses blocks over real loopback HTTP; unsupported model selection blocks sending without losing the draft and switching back recovers; unavailable images block requests until explicit removal |
| History budget | `WorkspaceImageAttachmentHistoryTests`: real snapshot reads, whole older-payload count/context omission with markers, latest image retained even when the model budget is too small; text-only history keeps its original default `Images` representation |

The first full Infrastructure run exposed a new test comparing a record's list
reference rather than its attachment values. The final test separately compares
message fields and ordered attachment metadata; no production assertion was
weakened. Existing cross-task SQLite coverage now explicitly names insert columns
and checks the foreign-key extended error (787), so the added MIME column cannot
make that negative test pass for a wrong column-count error. Older migration
fixtures remove all later version markers, not just version 15.

Final current-candidate results (no failures, skips or aborts):

- Infrastructure complete project: **316/316**, `results/infrastructure-final.trx`.
- Providers complete project: **600/600**, `results/providers.trx`.
- Agent complete project: **132/132**, `results/agent.trx`.
- App focused regression: **150/150**, `results/app-attachments.trx`.
- Total in this regression scope: **1198**, not the complete solution test suite.
- Full solution build succeeds with **0 warnings / 0 errors**.
- Roslyn syntax/one-type/size check: **123** changed/untracked C# files,
  largest file **497** lines, largest method/local function **59** lines.

Reproduction (all paths below are relative to the repository):

```powershell
dotnet build MossAgent.slnx --artifacts-path artifacts/image-workflow-20261018 --nologo -v minimal
dotnet test tests/MossAgent.Infrastructure.Tests/MossAgent.Infrastructure.Tests.csproj --artifacts-path artifacts/image-workflow-20261018 --logger 'trx;LogFileName=infrastructure-final.trx' --results-directory artifacts/image-workflow-20261018/results --nologo -v minimal
dotnet test tests/MossAgent.Providers.Tests/MossAgent.Providers.Tests.csproj --artifacts-path artifacts/image-workflow-20261018 --no-build --logger 'trx;LogFileName=providers.trx' --results-directory artifacts/image-workflow-20261018/results --nologo -v minimal
dotnet test tests/MossAgent.Agent.Tests/MossAgent.Agent.Tests.csproj --artifacts-path artifacts/image-workflow-20261018 --no-build --logger 'trx;LogFileName=agent.trx' --results-directory artifacts/image-workflow-20261018/results --nologo -v minimal
dotnet test tests/MossAgent.App.Tests/MossAgent.App.Tests.csproj --artifacts-path artifacts/image-workflow-20261018 --filter 'FullyQualifiedName~Attachment|FullyQualifiedName~ComposerDraft|FullyQualifiedName~ComposerSend|FullyQualifiedName~WorkspaceSendDraft|FullyQualifiedName~ProjectlessChat' --logger 'trx;LogFileName=app-attachments.trx' --results-directory artifacts/image-workflow-20261018/results --nologo -v minimal
```

## Remaining scope and limitations

The list below records this earlier candidate's original boundaries, not the
latest overall status. Projectless explicit selection was subsequently added
in `PROJECTLESS_ATTACHMENTS_ACCEPTANCE.md`; selected/composer, restored-draft
and sent/history thumbnails are now covered by
`ATTACHMENT_THUMBNAIL_ACCEPTANCE.md`. Native picker/restart, full-size viewing,
other-format/native-image orientation acceptance, PDF and live-provider
acceptance remain open. JPEG EXIF thumbnail normalization is covered separately
by `IMAGE_ORIENTATION_ACCEPTANCE.md`, without rewriting original attachments.

- This does not complete the overall Codex functionality/workflow objective.
  `FUNCTIONAL_PARITY_MATRIX.md` retains the complete gap inventory.
- Picker selection is scripted; actual Windows codec execution, real files,
  SQLite and loopback HTTP are verified, not native picker clicks, EXE restart,
  rendered image thumbnails or paid-provider image interpretation. No existing
  user window or real credential was accessed.
- Native file pick/restart, thumbnails/previews, projectless image selection,
  PDF/native documents and image sharing with child agents remain open.
- Provider-specific accepted formats, real reasoning and useful image-cost
  estimation need further verification. The conservative Base64-character budget
  can reject an otherwise size-valid image for a small-context model; the latest
  image is not silently stripped to force a successful call.
- Synchronous native decoding is bounded by encoded bytes, dimensions and pixels
  but cannot be forcibly interrupted midway. Caller cancellation stops waiting;
  native resources are released when the bounded background decode completes.
- Paths retain existing link checks, not a claim of eliminating every filesystem
  TOCTOU race. File writes and metadata transactions are not distributed commits;
  unreferenced snapshots/abrupt-submit draft duplication remain possible and
  restored drafts are never sent automatically.
- No publish was requested or performed. Native codec resolution in these tests
  does not establish Release single-file extraction or dependency-count behavior.
- Earlier sessions `21912` and `11377` were re-polled and remained live with no new
  output. Process inventory confirms the earlier dotnet/vstest/App processes
  still exist. They were not restarted, killed or overwritten and cannot validate
  this candidate. The earlier two allocation failures remain unresolved; this is
  not a successful current complete App/all-suite checkpoint.

The later ordinary frozen workspace run has since completed with App 5,021
passes / two failures, no skips or abort; its seven other test projects pass.
Current full session 95889 tests the newer thumbnail candidate and has already
reported a different raw-counter allocation assertion. See
`DRAFT_BACKGROUND_PERSISTENCE_ACCEPTANCE.md` for exact terminal/run boundaries;
no current complete regression is claimed green here.
