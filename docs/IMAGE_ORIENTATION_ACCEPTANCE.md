# JPEG image-preview orientation

## Confirmed defect and scope

The attachment renderer decoded raw pixels and resized them without applying
the codec's EXIF origin. Deterministic asymmetric six-color JPEG fixtures
confirmed the defect before production changes: **4 passed / 28 failed / 32
executed**, no skips. Only normal top-left orientation passed; mirrored cases
had incorrect cell positions, and transposed cases also had incorrect dimensions.
The report remains under `artifacts/image-orientation-20261018/results/before-fix`.

JPEG thumbnail previews now honor all eight EXIF orientations. This applies to
the shared renderer already used by selected images, restored drafts, sent rows
and lazy history previews. Original attachment bytes and encoded dimensions are
not normalized or rewritten. Other formats' orientation metadata, real-camera
corpora, full-size viewing and real-provider interpretation are not established
by these JPEG fixtures.

## Design and safety review

- Infrastructure owns the change behind the existing image-decoder and thumbnail
  ports. No UI, Agent, provider, application contract or database schema changes.
- `SkiaAttachmentImageSource` retains `SKCodec.EncodedOrigin` while keeping its
  original bounded raw bitmap. Format, actual dimension/pixel limits, animation
  rejection, complete decode and cancellation checks remain shared and unchanged.
- `SkiaAttachmentImageOrientation` applies the origin only after the renderer's
  existing resize. Any extra surface is at most 96 by 96 pixels; there is no
  second full-resolution orientation bitmap. Normal top-left uses the existing
  direct path. Rotation, horizontal/vertical mirroring, transpose and transverse
  use explicit small-surface transforms.
- The final static PNG has the oriented dimensions and default top-left origin,
  so a consuming bitmap decoder does not apply the transform twice. Aspect ratio,
  small-image no-upscale and 64 KiB/96-pixel output limits are retained.
- The renderer's instance gate still stays held until its worker exits. The
  helper checks cancellation and disposes its temporary surface. The existing
  bounded-native-work limitation remains: synchronous codec/canvas work cannot
  be forcibly interrupted midway.
- Snapshot hashes, original JPEG/EXIF bytes, encoded width/height, model payloads
  and selected-file authorization stay unchanged. This does not strip metadata
  from provider uploads or claim new model-side image interpretation behavior.
- No new file reads, process, network call, permission, approval bypass, global
  cache, AOT/trimming setting or cross-module dependency was introduced.
- New handwritten C# files each contain one type; focused pixel and persistence
  tests prove success, while the existing complete Infrastructure suite retains
  malformed, animated, oversize, cancellation, hash and path-boundary negatives.

## Validation

Candidate: `artifacts/image-orientation-20261018`.

| Evidence | Terminal result |
| --- | --- |
| Before-fix JPEG fixtures | 4 passed / 28 failed / 32 executed |
| Initial orientation/renderer/decoder focused checks after fix | 63/63 passed |
| Complete Infrastructure suite after persistence tests | 392/392 passed, no skips or abort |
| App image/chip/history/projectless/draft checks | 84/84 passed, no skips or abort |
| Final complete solution build | 0 warnings, 0 errors |
| Changed/untracked handwritten C# shape audit | 178 files, no violations; maximum 499 file lines / 67 method lines |

The 32 pixel cases cover all eight JPEG origins, little-/big-endian TIFF tags,
large inputs and inputs smaller than the thumbnail maximum. Each first checks
that native Skia actually recognizes the requested origin, then checks output
dimensions, six independently specified cell positions/colors, alpha, default
output origin, byte bounds and unchanged original bytes/encoded dimensions.

Sixteen persistence cases use real exact-file/project readers, temporary files,
SQLite and file-backed stores. All eight origins pass both project and
projectless selection, draft reopening, and sent-history snapshot reopening after
deleting the source. Projectless historical tasks are actually persisted with a
null project. Draft and historical previews agree, while original JPEG bytes,
draft text and sent text survive unchanged. These are port/SQLite checks, not
native picker clicks or an EXE restart.

The optional test evidence writer produced eight source JPEG/preview PNG pairs
under this candidate's `images`. The generated `orientation-contact-sheet.png`
was inspected directly: encoded rasters stay unchanged and all normalized
previews have the expected mirrored/rotated cell layout. This is a deterministic
codec comparison, not a desktop screenshot or a Codex pixel comparison.

## Remaining product work

This does not complete image input or the overall Codex workflow-parity goal.
PDF, full-size viewing, child-agent image sharing, other-format orientation
metadata, native picker/restart and real-provider format/interpretation acceptance
remain open in `FUNCTIONAL_PARITY_MATRIX.md`.

At 2026-10-18 14:00 +08 the earlier frozen complete run, session **95889**, was
still live with the same runner/App process identities. All six frozen binary
hashes still matched. Its recorded 1,176-byte sidebar allocation failure is not
erased by newer focused passes. No second complete regression was started and no
running candidate was rebuilt. No native user window, credential, release,
commit, push or memory was changed.
