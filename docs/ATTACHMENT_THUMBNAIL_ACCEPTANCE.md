# Image attachment thumbnail workflow

## Delivered scope

The composer now displays a bounded image thumbnail in the existing shared
attachment chip for selected static PNG/JPEG/GIF/WebP inputs. Restored project
and projectless image drafts use the same row factory and renderer. Text and
unavailable attachments retain their ordinary removable file chips.

Sent messages now retain their selected immutable image source. Restored history
uses the same chip with a lazy, validated task/message snapshot source, including
projectless conversations. No history image bytes are read just to map its row.

JPEG EXIF preview rotation/mirroring is subsequently verified in
`IMAGE_ORIENTATION_ACCEPTANCE.md`, without changing original attachment bytes.
This is not complete image-input acceptance: PDF, full-size image viewing,
child-agent image sharing, other-format/native-image orientation acceptance,
native picker/EXE restart and paid-provider interpretation remain open.
No existing user window was operated or captured.

## Design and safety review

- Application owns `IAttachmentThumbnailRenderer`; Infrastructure implements it
  with Skia. App only invokes the port and decodes its small PNG result into the
  Avalonia bitmap. Dependency direction remains unchanged.
- Rendering uses the already-selected immutable `AttachmentImage` bytes. It
  never opens the original path, grants a directory, invokes a model/tool or
  changes authorization/approval settings. Projectless input remains plain chat.
- The shared `SkiaAttachmentImageSource` retains source format, animation,
  dimensions, pixel-count, cancellation and full-decoding checks for both the
  original image decoder and the thumbnail renderer. The renderer also rejects
  metadata inconsistent with actual decoded dimensions/media type.
- A renderer-instance semaphore admits one source decode at a time. Work runs
  off the UI thread. The gate is retained until the worker actually exits, even
  if its caller stops awaiting it after cancellation.
- Thumbnail output is a static PNG, at most 96 pixels per edge and 64 KiB.
  Aspect ratio is preserved and small inputs are not upscaled by the renderer.
  Original bytes/metadata, provider payloads and database schema remain unchanged.
- The shared control loads only while attached, has a ten-second caller deadline,
  cancels on replacement/detach and rejects late results. It posts only bounded
  final presentation to the UI dispatcher; it never waits for a stopped UI loop.
- PNG signature/header dimensions must match bounded result metadata before the
  UI bitmap decoder runs. Bitmap ownership is local to the attached control;
  clearing, replacement and detach clear its source and dispose the old bitmap.
- There is no global image cache. A failed thumbnail falls back to the existing
  icon/filename/size/removal controls without changing attachment availability.
  No raw image content is logged or newly persisted in SQLite.

The existing application port contracts are reused for selection/persistence;
only image-to-thumbnail rendering needs a new port. New code remains one type
per handwritten C# file, below the file/method hard limits.

## Interaction and rendering

AppAttachmentChip remains the single visual owner for composer attachments.
Its new image slot, 28-DIP sizing and stretch live in shared styles. The slot
reserves its layout while loading; image completion does not move labels or
removal buttons. Existing text-chip geometry is unchanged. The thumbnail is
noninteractive/decorative in accessibility; the chip retains its accessible
filename/size and the removal button retains its own name/action.

Four isolated Headless PNGs under
`artifacts/attachment-thumbnails-20261018/screenshots` cover light/dark and
removable/read-only chips. Both removable theme images were inspected directly:
thumbnail, readable labels and removal action fit without overlap. Tests verify
unchanged pre/post-load bounds and the shared 28-DIP image size. These are Skia
Headless renders, not screenshots of native desktop windows or a Codex comparison.

## Validation

Candidate: `artifacts/attachment-thumbnails-20261018`.

| Evidence | Terminal result |
| --- | --- |
| Thumbnail renderer plus existing image-decoder checks | 31/31 passed |
| Full Infrastructure suite, including SQLite/image/draft boundaries | 344/344 passed |
| Final expanded App attachment/chip/projectless/draft checks | 82/82 passed |
| Complete solution build | 0 warnings, 0 errors |
| Changed/untracked handwritten C# shape audit | 168 files, no violations; maximum 499 file lines / 67 method lines |

Renderer tests cover all four source formats, aspect ratios, small/extreme-aspect
images, malformed and oversized actual headers, animated formats, inconsistent
envelopes, cancellation and recovery after a failed request. App tests cover
both themes, stable layout, fallback/removal, no reads while unattached,
replacement/detach cancellation, late completion, detach/reattach, invalid
renderer output, actual production DI and project/projectless draft restoration
without an original file. Draft-coordinator fixtures use a test repository;
the renderer is real, while controlled renderers isolate lifecycle races.

The first expanded App attempt stopped at compilation because its optional PNG
evidence writer used an obsolete save overload. It was corrected to the current
`PngBitmapEncoderOptions` API; no suppression or relaxed assertion was added.
Only `results/expanded-app-final` is the final expanded result.

After the older full run ended, the latest candidate was rebuilt and frozen at
`full-app-binary-identity.json`. Its only complete regression is session **95889**,
started without diagnostic hooks, with output in `full-regression.log` and
`results/full`. It was still executing when recorded; focused passes do not
establish its terminal result. Do not rebuild over these running binaries.

This full run has already reported another unchanged raw-counter case:
`SidebarFilterAllocationTests` for recent/no-matches measured **1,176 bytes**
against zero. That family does not yet use the isolated measurement helper.
Its exact cause is not established merely by the earlier standalone runtime
probe. Keep the failure, audit the remaining raw-counter boundaries and retain
their functional/zero-byte assertions; do not call this full candidate green.

No native interaction, release, commit, push, AOT, trimming or memory update was
performed. The overall Codex workflow-parity objective remains incomplete.

## Sent-message and history extension

Candidate: `artifacts/history-thumbnails-20261018`. This is separate from the
still-running frozen full candidate above; those binaries are not rebuilt.

- `WorkspaceViewModel` transfers the selected thumbnail source into its sent
  attachment row, without transferring a UI bitmap or rereading the source file.
- Production DI supplies `ConversationAttachmentThumbnailFactory` to the catalog.
  Only user-image rows receive a source; text and non-user attachments do not.
- Mapping does not hydrate image bytes. An attached chip invokes the source,
  validates task/message ownership and metadata, and reads exactly its immutable
  snapshot through `IConversationAttachmentStore`. The real store retains its
  internal path/link, byte-length, hash, format and image-decoding checks.
- An instance-local gate bounds active snapshot/read-render operations. Waiting
  requests honor cancellation. An ignoring port keeps the gate until it returns;
  the existing native decoder limitation still applies: bounded synchronous
  decoding may finish after the store's cancellation wrapper returns. This does
  not claim instantaneous native interruption or a process-wide decode gate.
- The same ten-second control deadline, stale-result rejection, small PNG bounds,
  fallback icon, read-only filename/size and bitmap disposal apply to history.
  Missing/corrupt snapshot previews do not discard message text or metadata and
  do not weaken the separate model-history validation before a provider call.
- There is no new model call, original-path read, tool permission, database
  migration, image cache, native-window operation or dependency reversal.

Validation so far:

| Evidence | Result |
| --- | --- |
| Final focused boundary/workflow/component plus allocation controls | 31/31 passed, no skips |
| Expanded App attachment/composer/history/navigation/model checks | 409/409 passed, no skips or abort |
| Complete solution build | 0 warnings, 0 errors |
| Changed/untracked handwritten C# shape audit | 174 files, no violations; maximum 499 file lines / 67 method lines |

The initial focused attempt has its own retained report: 26 passed / 3 failed.
Those three new test expectations mistakenly compared two separately created
image objects by identity. They now assert the exact renderer-returned instance;
no production or zero-byte assertion was relaxed. The final run also adds two
missing-snapshot component cases. Only `results/focused-final` is the 31-case
passing result; `results/focused` remains the initial failure evidence.

The two new production-composition workflow cases select through a controlled
picker and real readers, send to a local loopback provider, reopen the same
SQLite/artifact data in a fresh production DI container, and load the historical
thumbnail after deleting the selected source. Both project and projectless
conversations are covered; this is not a native EXE restart or paid-model test.

Six Headless PNGs under this candidate's `screenshots` cover both themes and
valid/corrupt/missing snapshots in `WorkspaceMessageView`. Light/dark valid images
and the light corrupt fallback were inspected directly. The shared chip binds
the thumbnail, keeps its filename/size and has no removal action in history.
Broader attachment/composer/navigation/model regression ended successfully under
`results/expanded`: 409/409 passed, no skips or abort. This remains a filtered App
run, not a complete regression of the new candidate. The old frozen full run is
still separate and already has a recorded allocation failure.
