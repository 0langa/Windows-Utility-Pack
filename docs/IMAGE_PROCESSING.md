# Image processing dependency

Image Resizer, Image Format Converter, and Screenshot Annotator use
`Magick.NET-Q8-AnyCPU` 15.0.0 through `ImageProcessingService`.
It replaces ImageSharp, ImageSharp.Drawing, and their transitive font library.
Magick.NET uses the [Apache 2.0 license](https://github.com/dlemstra/Magick.NET/blob/15.0.0/License.txt)
and requires no license key, account, subscription, or separately installed
ImageMagick executable. NuGet supplies the native codecs.

The application copies the package's complete native-library and third-party
notices to `ThirdPartyNotices/Magick.NET.txt` in build and publish output. Keep
that file with distributed builds. Native codecs add deployment size; use a
Windows runtime identifier when publishing instead of shipping every platform's
native binaries.

## Behavior

- Input data: PNG, JPEG, WebP, BMP, TIFF, GIF, and HEIC. The decoder is selected
  from the file signature, independent of its extension. Documents such as SVG
  and PDF are not accepted through this service.
- Outputs: JPEG, PNG, WebP, BMP, and TIFF. JPEG flattens transparent pixels onto
  white. PNG, WebP, and TIFF preserve transparency.
- TIFF and WebP exports retain multiple pages/frames. Animated GIF/WebP input
  is coalesced before editing or resizing. Other output formats use the first
  frame. HEIC is an input format only.
- Resizing supports fitting within bounds and stretching to exact dimensions.
  Pixel precision remains eight bits per channel, as in the previous service.
- Annotation supports rectangles, arrows (including explicit endpoints), text,
  opaque black redaction, and local blur. Eight-digit colors use WPF's ARGB
  interpretation. Annotation exports remove metadata/profiles, including
  potentially unredacted embedded previews.
- Conversion, resize, and annotation read and release the source before
  publishing a completed sibling temporary file. Failed conversion or canceled
  processing does not truncate an existing destination. Without overwrite,
  existing filenames receive a numeric suffix; a concurrent collision fails
  instead of replacing another file.
- CPU work runs off the dispatcher. Cancellation is propagated to callers and
  checked before publishing. Native processing progress observes cancellation
  where the codec reports it; cancellation is not a hard execution deadline.
- Inputs are limited to 256 frames, 32768 pixels per side, and 64 million
  decoded pixels across frames. Native memory/disk/thread limits provide an
  additional bound. Limits are checked from headers before decoding and after
  processing.

## Regression coverage and remaining limits

`ImageProcessingServiceTests` covers encoded formats, dimensions, aspect ratio,
alpha, animation timing, HEIC import, same-path writes, filename collisions,
corrupt input, failed publication, cancellation, all annotation types, metadata
removal, and ARGB colors. Windows WIC independently encodes PNG fixtures and
decodes PNG/JPEG/BMP/TIFF exports. The small HEIC fixture is a checked-in CC0
procedural chart; its provenance and checksum are beside it.

Run `pwsh ./scripts/Test-Local.ps1` for the solution and isolated WPF composition
sweep. Use `-DisableIPv6` only if needed for this machine's NuGet connectivity.
The sweep opens views; it does not exercise their controls. For isolated manual
interaction, run the built `WindowsUtilityPack.UiSmoke.exe` with a report path
and `--interactive`; it uses a disposable profile with machine integrations off.

Native conversion, output collisions, resizing, QR decoding and annotation
export/undo are recorded in [native acceptance](NATIVE_ACCEPTANCE.md). Earlier
desktop capture and annotation checks covered 100%/125% displays and a 300-DPI
source image; see [the retest report](REVIVAL_RETEST_2026-10-26.md). Clean Windows
Sandbox startup and installer lifecycle have separate evidence. Color-managed
photos, EXIF orientation, additional monitor arrangements and the remaining
per-tool combinations are listed in [the workflow matrix](REVIVAL_WORKFLOW_MATRIX.md).
