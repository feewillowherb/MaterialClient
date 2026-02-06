---
name: Photo grid A+C optimization
overview: "Implement Option A (thumbnail decode at 480px) and Option C (async load with placeholder) from the evaluation doc: add a thumbnail converter for Bitmap? display, extend PhotoGridViewModel with async thumbnail loading and placeholder, and wire grid views to bind to thumbnails while keeping path for ImageViewer."
todos: []
isProject: false
---

# Photo Grid Options A + C Implementation Plan

## Goal

- **Option A**: Decode grid images at 480px (e.g. `Bitmap.DecodeToWidth(stream, 480)`) instead of full resolution.
- **Option C**: Load thumbnails on a background thread and show a placeholder until ready; update UI on completion via dispatcher.

Result: grid shows placeholder immediately, then thumbnails appear without blocking the UI; ImageViewer/PrintPreview continue to load full-resolution from path.

## Architecture (A + C combined)

```mermaid
sequenceDiagram
  participant VM as PhotoGridViewModel
  participant BG as Background thread
  participant UI as UI thread
  VM->>VM: SetEntryPhoto(index, path)
  VM->>VM: EntryPhoto1Thumbnail = null (placeholder)
  VM->>BG: Task.Run DecodeToWidth(path, 480)
  BG->>BG: File.OpenRead + Bitmap.DecodeToWidth(stream, 480)
  BG->>UI: Dispatcher.UIThread.Post(set thumbnail)
  UI->>VM: EntryPhoto1Thumbnail = bitmap
  Note over VM,UI: Grid binds to EntryPhoto1Thumbnail; converter shows default if null
```



- **Path properties** (`EntryPhoto1`–`ExitPhoto4`) stay as `string?` and are used for: (1) `OpenImageViewerCommand` parameter (full image), (2) triggering async thumbnail load.
- **New thumbnail properties** (`EntryPhoto1Thumbnail`–`ExitPhoto4Thumbnail`) are `Bitmap?`; grid binds `Image.Source` to these. Null = show default (placeholder).
- **Decode logic**: Use `Bitmap.DecodeToWidth(stream, 480)` in the ViewModel’s async load (Option A). No separate sync thumbnail converter in XAML for the grid once C is in place.

## 1. Converter for placeholder + thumbnail display (Option C)

- **New converter**: `NullableBitmapToImageConverter` (or reuse/extend name).
  - **Input**: `object?` (expected `Bitmap?` for grid).
  - **Logic**: If value is `Bitmap b`, return `b`; else return the same default car image used in [CarNullOrEmptyImageConverter](MaterialClient/Converters/CarNullOrEmptyImageConverter.cs) (lazy-loaded from `avares://MaterialClient/Assets/Car_Default.png`).
  - **Output**: `IImage` for `Image.Source`.
- **Register** in [App.axaml](MaterialClient/App.axaml) (e.g. `NullableBitmapToImageConverter` or `CarThumbnailPlaceholderConverter`).

This converter is used only in the photo grid views; ImageViewer/PrintPreview keep using `CarNullOrEmptyImageConverter` (path → full-size bitmap).

## 2. PhotoGridViewModel: thumbnail properties and async load

**File**: [MaterialClient/ViewModels/PhotoGridViewModel.cs](MaterialClient/ViewModels/PhotoGridViewModel.cs)

- **Add 8 reactive properties**: `EntryPhoto1Thumbnail` … `EntryPhoto4Thumbnail`, `ExitPhoto1Thumbnail` … `ExitPhoto4Thumbnail` (`Bitmap?`). Keep existing `EntryPhoto1`…`ExitPhoto4` (path) unchanged.
- **Clear()**: For each thumbnail property: dispose existing `Bitmap` if non-null, then set to `null`. Clear path properties as today. Optionally increment a `_loadGeneration` and have async completion check it to avoid applying stale thumbnails.
- **SetEntryPhoto(index, path)** / **SetExitPhoto(index, path)**:
  1. Set the path property as today (e.g. `EntryPhoto1 = path`).
  2. Dispose and clear the corresponding thumbnail (e.g. `EntryPhoto1Thumbnail = null`).
  3. If `string.IsNullOrEmpty(path)`, return.
  4. Capture a “generation” or the path for this slot. Start a fire-and-forget async load:
    - `Task.Run(() => LoadThumbnailBitmap(path))` where `LoadThumbnailBitmap` uses `PathManager.ToAbsolutePath(path)`, then `using var stream = File.OpenRead(absolutePath); return Bitmap.DecodeToWidth(stream, 480);` (handle missing file / exception → return null).
    - In the continuation (or after await), use `Dispatcher.UIThread.Post` to run on UI thread: if the slot’s path still equals the loaded path (or generation matches), set the corresponding thumbnail property and dispose the previous bitmap for that slot; then assign the new bitmap. If path changed or generation changed, dispose the loaded bitmap and do not assign.
- **LoadThumbnailBitmap**: Static or instance helper that returns `Bitmap?`; catch exceptions and return null so UI just keeps showing placeholder.
- **Disposal**: When setting a new thumbnail, dispose the old one. When clearing, dispose all. Use `IDisposable` / `Bitmap.Dispose()` (Avalonia Bitmap is IDisposable).

## 3. Grid views: bind Image.Source to thumbnail + converter

- **[AttendedWeighingMainView.axaml](MaterialClient/Views/AttendedWeighing/AttendedWeighingMainView.axaml)**  
  - Change all 8 `Image` bindings from  
  `Source="{Binding PhotoGridViewModel.EntryPhoto1, Converter={StaticResource CarNullOrEmptyImageConverter}}"`  
  to  
  `Source="{Binding PhotoGridViewModel.EntryPhoto1Thumbnail, Converter={StaticResource NullableBitmapToImageConverter}}"`  
  - Same for EntryPhoto2–4 and ExitPhoto1–4.  
  - Keep `CommandParameter="{Binding PhotoGridViewModel.EntryPhoto1}"` (and same for others) so ImageViewer still receives the **path** for full-resolution load.
- **[PhotoGridView.axaml](MaterialClient/Views/PhotoGridView.axaml)**  
  - Same change: bind `Source` to `EntryPhoto1Thumbnail` … `ExitPhoto4Thumbnail` with `NullableBitmapToImageConverter`.  
  - Keep `CommandParameter` bound to path (`EntryPhoto1` … `ExitPhoto4`).

## 4. Option A: where it is applied

- Option A is implemented **inside the ViewModel’s async load**: the background task uses `Bitmap.DecodeToWidth(stream, 480)` instead of `new Bitmap(absolutePath)`. No separate sync “thumbnail converter” that takes path in XAML is required for the grid once C is in place.
- ImageViewer and PrintPreview continue to use [CarNullOrEmptyImageConverter](MaterialClient/Converters/CarNullOrEmptyImageConverter.cs) with **path** and full decode; no change there.

## 5. Files to add or modify


| File                                                                                                                                         | Action                                                                                                                                                                                                                      |
| -------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| New: `MaterialClient/Converters/NullableBitmapToImageConverter.cs`                                                                           | Add converter: `Bitmap?` → default car image or bitmap.                                                                                                                                                                     |
| [MaterialClient/App.axaml](MaterialClient/App.axaml)                                                                                         | Register the new converter resource.                                                                                                                                                                                        |
| [MaterialClient/ViewModels/PhotoGridViewModel.cs](MaterialClient/ViewModels/PhotoGridViewModel.cs)                                           | Add 8 thumbnail properties; async load with DecodeToWidth(480); Clear() dispose and null; SetEntryPhoto/SetExitPhoto trigger load and use Dispatcher to set thumbnail; path comparison or generation to ignore stale loads. |
| [MaterialClient/Views/AttendedWeighing/AttendedWeighingMainView.axaml](MaterialClient/Views/AttendedWeighing/AttendedWeighingMainView.axaml) | Bind 8 `Image.Source` to `PhotoGridViewModel.*Thumbnail` + new converter; keep CommandParameter as path.                                                                                                                    |
| [MaterialClient/Views/PhotoGridView.axaml](MaterialClient/Views/PhotoGridView.axaml)                                                         | Bind 8 `Image.Source` to `*Thumbnail` + new converter; keep CommandParameter as path.                                                                                                                                       |


## 6. Edge cases and notes

- **Stale load**: When path changes or `Clear()` is called before a load completes, the completion must not overwrite the slot. Use a per-slot “current path” check when posting to UI (e.g. only set thumbnail if `EntryPhoto1 == pathWeLoaded`).
- **Missing file**: If the file does not exist or decode fails, leave thumbnail null so the converter shows the default image.
- **Thread safety**: Only assign thumbnail properties on the UI thread (via `Dispatcher.UIThread.Post`). Read path on UI thread in the completion.
- **Memory**: Dispose every replaced or cleared bitmap to avoid leaks.
- **Avalonia API**: `Bitmap.DecodeToWidth(Stream stream, int width, BitmapInterpolationMode interpolationMode = HighQuality)`. Caller must dispose the stream (e.g. `using var stream = File.OpenRead(...)`).

## 7. Out of scope (unchanged)

- [ImageViewerWindow.axaml](MaterialClient/Views/ImageViewerWindow.axaml), [PrintPreviewWindow.axaml](MaterialClient/Views/PrintPreviewWindow.axaml): keep `CarNullOrEmptyImageConverter` and path binding.
- ManualMatchWindow / ManualMatchEditWindow: continue using `CarNullOrEmptyImageConverter` with path; no thumbnail in this change.
- Option B (in-memory thumbnail cache): not in this plan; can be added later.

