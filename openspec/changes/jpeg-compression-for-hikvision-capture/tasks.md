## 1. Compression Utility

- [ ] 1.1 Create `MaterialClient.Common/Utils/JpegCompressionUtil.cs` with static `TryCompressJpeg(string filePath, int quality, ILogger? logger)` method using `System.Drawing.Bitmap` + JPEG `ImageCodecInfo` encoder
- [ ] 1.2 Implement quality >= 100 early return (zero overhead skip)
- [ ] 1.3 Wrap entire method body in try-catch, log warning on failure, return `false` — never throw

## 2. Configuration

- [ ] 2.1 Add `public int JpegQuality { get; set; } = 75;` property to `SystemSettings` in `MaterialClient.Common/Configuration/SystemSettings.cs`

## 3. HikvisionService Integration

- [ ] 3.1 In `CaptureJpegFromStreamBatchAsync`: read `JpegQuality` from `SystemSettings` (fallback to 100 if settings unavailable) and pass to all downstream methods
- [ ] 3.2 Add `int jpegQuality` parameter to `CaptureJpegBatchInternalAsync`; after file validation succeeds, call `TryCompressJpeg` and update `result.FileSize`
- [ ] 3.3 In the main-stream batch block (lines 204-256 of `CaptureJpegFromStreamBatchAsync`): after file validation succeeds, call `TryCompressJpeg` and update `result.FileSize`
- [ ] 3.4 Add `int jpegQuality = 100` parameter to `CaptureJpegFromStream(HikvisionDeviceConfig, int, string, out int)`; after decoder capture succeeds and file is written, call `TryCompressJpeg`
- [ ] 3.5 In both `CaptureJpeg` overloads: after `File.WriteAllBytes` succeeds, call `TryCompressJpeg`; always return original capture result regardless of compression outcome

## 4. Settings UI

- [ ] 4.1 Add `[Reactive] private int _jpegQuality = 75;` to `SettingsWindowViewModel`
- [ ] 4.2 In `SaveAsync`: add `systemSettings.JpegQuality = JpegQuality;`
- [ ] 4.3 In `LoadSettingsAsync`: add `JpegQuality = settings.SystemSettings.JpegQuality;`
- [ ] 4.4 In `SettingsWindow.axaml`: add Slider (Minimum=1, Maximum=100, TickFrequency=5, IsSnapToTickEnabled=True) + TextBlock displaying current value, placed below the stream type selector grid
