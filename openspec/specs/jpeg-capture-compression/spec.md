# JPEG Capture Compression Spec

## Purpose

Provides configurable JPEG post-capture compression for Hikvision camera captures, reducing file sizes through quality-adjustable re-encoding while preserving the original capture workflow.

## Requirements

### Requirement: JPEG post-capture compression utility

The system SHALL provide a `JpegCompressionUtil` static utility class in `MaterialClient.Common/Utils/` with a `TryCompressJpeg(string filePath, int quality, ILogger? logger)` method that loads a JPEG file, re-encodes it at the specified quality, and overwrites the original file.

#### Scenario: Successful compression
- **WHEN** `TryCompressJpeg` is called with a valid JPEG file path and quality < 100
- **THEN** the system SHALL load the image using `System.Drawing.Bitmap`, re-encode using the JPEG `ImageCodecInfo` encoder with the specified `Encoder.Quality`, save to the original path, and return `true`

#### Scenario: Quality at or above 100 skips compression
- **WHEN** `TryCompressJpeg` is called with quality >= 100
- **THEN** the system SHALL return `true` immediately without any file I/O or image processing

#### Scenario: Compression failure preserves original file
- **WHEN** `TryCompressJpeg` encounters any exception during loading, encoding, or saving
- **THEN** the system SHALL catch the exception, log a warning (if logger is provided), and return `false` without throwing. The original file on disk SHALL remain intact.

### Requirement: Compression integration in sub-stream capture path

The system SHALL apply JPEG compression after successful sub-stream capture in `HikvisionService.CaptureJpegBatchInternalAsync`. After file validation passes (file exists, size > 0), the system SHALL call `TryCompressJpeg` with the configured quality and update `BatchCaptureResult.FileSize` from the recompressed file.

#### Scenario: Sub-stream batch capture with compression
- **WHEN** `CaptureJpegBatchInternalAsync` processes a batch request and the SDK capture succeeds with a valid file
- **THEN** the system SHALL call `TryCompressJpeg(savePath, jpegQuality, logger)` and update `result.FileSize` from `new FileInfo(savePath).Length`

#### Scenario: Sub-stream compression failure does not break capture
- **WHEN** `TryCompressJpeg` returns `false` during sub-stream capture
- **THEN** the system SHALL log a warning and continue with the original file. The capture result SHALL remain successful with the original file size.

### Requirement: Compression integration in main-stream capture path

The system SHALL apply JPEG compression after successful main-stream capture in both `CaptureJpegFromStream` and the main-stream batch block within `CaptureJpegFromStreamBatchAsync`. After file validation passes, the system SHALL call `TryCompressJpeg` and update the file size.

#### Scenario: Main-stream batch capture with compression
- **WHEN** `CaptureJpegFromStreamBatchAsync` processes a main-stream request and `CaptureJpegFromStream` succeeds with a valid file
- **THEN** the system SHALL call `TryCompressJpeg(savePath, jpegQuality, logger)` and update `result.FileSize` from the recompressed file

#### Scenario: Main-stream single capture with compression
- **WHEN** `CaptureJpegFromStream` completes successfully and writes a valid file
- **THEN** the system SHALL call `TryCompressJpeg(savePath, jpegQuality, logger)` after the file write

#### Scenario: Main-stream compression failure does not break capture
- **WHEN** `TryCompressJpeg` returns `false` during main-stream capture
- **THEN** the system SHALL log a warning and continue. The capture result SHALL remain successful with the original file size.

### Requirement: Compression integration in direct CaptureJpeg methods

The system SHALL apply JPEG compression in both `CaptureJpeg` overloads in `HikvisionService`. After `File.WriteAllBytes` succeeds, the system SHALL call `TryCompressJpeg`.

#### Scenario: Direct CaptureJpeg with compression
- **WHEN** either `CaptureJpeg` overload successfully writes the captured buffer to disk
- **THEN** the system SHALL call `TryCompressJpeg(saveFullPath, jpegQuality, logger)` and return `true` regardless of compression result

#### Scenario: Direct CaptureJpeg compression failure
- **WHEN** `TryCompressJpeg` returns `false` after a successful SDK capture
- **THEN** the system SHALL still return `true` from `CaptureJpeg` (capture itself succeeded)

### Requirement: JpegQuality setting read from configuration

`CaptureJpegFromStreamBatchAsync` SHALL read `JpegQuality` from `SystemSettings` via `ISettingsService` and propagate it to all internal capture methods. If the settings service is unavailable, quality SHALL default to 100 (no compression).

#### Scenario: Quality read from settings
- **WHEN** `CaptureJpegFromStreamBatchAsync` is called and settings are available
- **THEN** the system SHALL read `settings.SystemSettings.JpegQuality` and pass it to all capture/compression calls

#### Scenario: Settings unavailable fallback
- **WHEN** `CaptureJpegFromStreamBatchAsync` is called and `ISettingsService` is null
- **THEN** the system SHALL use quality 100 (no compression)
