# QR Code Generator - Implementation Notes

Date: 2026-04-08

## Overview

A new production-grade tool, **QR Code Generator**, was added under **Developer & Productivity**.
The feature includes URL validation/normalization, live preview generation, style customization,
scannability warnings, multi-format export, clipboard helpers, logo overlay support, and recent history persistence.

## Navigation and UI Integration

- Registered as tool key: `qr-code-generator`
- Category: `Developer & Productivity`
- ViewModel: `QrCodeGeneratorViewModel`
- View: `QrCodeGeneratorView`
- DataTemplate registered in `App.xaml`
- Home/category navigation is automatically populated through `ToolRegistry`

## New Services and Models

Namespace: `WindowsUtilityPack.Services.QrCode`

- `IQrCodeService` / `QrCodeService`
  - URL normalization and validation
  - Preview rendering (raster + SVG)
  - Export (PNG/JPEG/BMP/SVG/PDF)
  - Scannability analysis
- `IQrCodeFileDialogService` / `QrCodeFileDialogService`
  - Logo file picker
  - Export save dialog and format inference
- Style and export models:
  - `QrCodeStyleOptions`
  - `QrCodePreviewResult`
  - `QrCodeExportRequest`
  - `QrCodeExportResult`
  - `QrScannabilityReport`
- Enums:
  - `QrCodeExportFormat`
  - `QrCodeErrorCorrectionLevel`
  - `QrCodeModuleShape`
  - `QrCodeStylePreset`
- `QrCodePresetCatalog` for built-in presets

## Core Behavior

- Accepts URL input with normalization (adds `https://` when appropriate)
- Rejects malformed/unsupported URLs with clear feedback
- Live preview updates on setting changes (debounced)
- Manual generation command also supported
- Styling controls include:
  - size, quiet zone, foreground/background colors
  - transparent background
  - error correction level
  - module shape (square/rounded)
  - frame and caption options
  - center logo overlay with size/padding constraints
  - preset switching and reset to defaults
- Utilities:
  - copy QR image to clipboard
  - copy normalized URL
  - open URL in browser
  - clear session
- Export options:
  - PNG, JPEG, BMP, SVG, PDF
  - export size and DPI controls
  - timestamp toggle in suggested filename
  - last export directory persistence

## Persistence

`AppSettings` now stores QR-specific preferences:

- `QrCodeRecentUrls`
- `QrCodeLastExportDirectory`
- `QrCodeIncludeTimestampInFileName`

## Dependency Added

- `QRCoder` (v1.6.0)
  - added to app project and root shim project

## Architecture Notes

- Rendering is implemented in service layer using QRCoder payload generation plus custom WPF raster/SVG rendering.
- ViewModel remains MVVM-safe and uses services for dialogs, generation/export, settings, and clipboard.
- Clipboard abstraction was extended with image support via `IClipboardService.TrySetImage(BitmapSource)`.

## Future Extension Points

- Batch generation can be layered on top of current request/result models.
- Additional presets or branding packs can be added in `QrCodePresetCatalog`.
- Additional content types (Wi-Fi, vCard, calendar payloads) can be added by extending normalization and payload construction in `QrCodeService`.
