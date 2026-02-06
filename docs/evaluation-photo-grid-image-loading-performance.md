# Evaluation: Photo Grid Image Loading Performance

## 1. Scope

- **Views**: `AttendedWeighingMainView.axaml`, `PhotoGridView.axaml`
- **Issue**: When the page renders, images are too large (~7–8 MB each), causing slow load and poor responsiveness.
- **Context**: Both views display up to 8 photos (4 entry + 4 exit) in small grid cells. Images are bound via `CarNullOrEmptyImageConverter` to file paths from `PhotoGridViewModel` (e.g. `EntryPhoto1`–`EntryPhoto4`, `ExitPhoto1`–`ExitPhoto4`).

---

## 2. Root Cause Analysis

### 2.1 Full-file load and full decode

- **Converter**: `CarNullOrEmptyImageConverter.Convert()` uses `new Bitmap(absolutePath)` for local files.
- **Effect**: The entire file (7–8 MB) is read from disk and decoded to full resolution in memory. Decoded pixel buffer is typically 2–4× the file size (e.g. 15–30 MB per image).
- **Impact**: 8 thumbnails ⇒ up to ~56–64 MB read + 8 full decodes. This happens on the UI thread when bindings are evaluated, so the UI blocks.

### 2.2 No decode-to-size (thumbnail) path

- The grid only needs a small display size (e.g. a few hundred pixels), but the code always decodes at **full resolution** and relies on `Stretch="UniformToFill"` for display.
- Avalonia’s `Bitmap` supports **DecodeToWidth** / **DecodeToHeight**, which decode at a target size and are more efficient than load-then-resize. This path is currently unused.

### 2.3 No caching

- The converter is stateless: every binding evaluation can create a **new** `Bitmap` from the same path.
- Repeated binding (e.g. tab switch, list selection change) can reload and redecode the same files, multiplying cost.

### 2.4 Synchronous loading on UI thread

- All file I/O and decode run synchronously inside the converter during binding. No async load or background decode.
- Result: first paint is delayed until the heaviest images finish loading; UI can freeze.

### 2.5 Summary

| Factor              | Current behavior              | Impact                          |
|---------------------|-------------------------------|---------------------------------|
| Decode size         | Full resolution               | High memory, slow decode        |
| I/O                 | Full file read per image      | Slow disk + network if remote  |
| Thread              | Synchronous on UI             | Blocking, jank                 |
| Cache               | None (new Bitmap per eval)    | Redundant work on re-bind      |
| Display vs source   | Same full-res source in grid  | Unnecessary for thumbnail use  |

---

## 3. Optimization Options

### 3.1 Option A: Thumbnail decode in converter (recommended baseline)

**Idea**: For grid display, decode at a fixed max width/height (e.g. 400–600 px) using `Bitmap.DecodeToWidth()` (or `DecodeToHeight`) instead of `new Bitmap(path)`.

**Pros**

- Single change in one place (converter or a dedicated thumbnail converter).
- Large reduction in decode time and memory (e.g. 400px-wide decode vs 4000px).
- Avalonia API is built for this; no new dependencies.

**Cons**

- Need a way to distinguish “thumbnail” vs “full size” (e.g. ImageViewer must still show full res). Options:
  - **A1**: New converter (e.g. `CarThumbnailImageConverter`) used only in grid views; keep `CarNullOrEmptyImageConverter` for ImageViewer/PrintPreview.
  - **A2**: Converter parameter (e.g. `maxWidth`) passed from XAML; when absent, use full decode.

**Implementation outline**

1. Add a thumbnail converter (or extend the existing one with a parameter).
2. For local file path: open `File.OpenRead(absolutePath)` and call `Bitmap.DecodeToWidth(stream, 480)` (or similar), then dispose stream. Keep null/empty and default-image behavior.
3. Use the thumbnail converter in `AttendedWeighingMainView.axaml` and `PhotoGridView.axaml` for all 8 `Image` bindings.
4. Keep `CarNullOrEmptyImageConverter` (full decode) in `ImageViewerWindow.axaml` and `PrintPreviewWindow.axaml` so “view full image” still loads full resolution.

**Suggested max size**: 480–600 px on the longest side; tune by testing on 7–8 MB files.

---

### 3.2 Option B: In-memory thumbnail cache

**Idea**: Cache decoded thumbnails by file path (and optionally file write time) so repeated binding or tab switch does not redecode.

**Pros**

- Avoids repeated decode when the same photo is shown again (e.g. switching list selection or tabs).
- Can be combined with Option A (cache the result of DecodeToWidth).

**Cons**

- Cache size and eviction policy needed (e.g. LRU, max count or max MB).
- Must invalidate when file is replaced (e.g. by path + last write time or similar).

**Implementation outline**

1. Introduce a small `ThumbnailCache` (e.g. path → `Bitmap`, with optional path+last-write-time key).
2. In the thumbnail converter (from Option A), check cache before decode; store result after decode.
3. Set a cap (e.g. 20 entries or 50 MB) and evict oldest or least recently used when over limit.

---

### 3.3 Option C: Async loading and placeholder

**Idea**: Load/decode thumbnails on a background thread and assign the `Bitmap` to the ViewModel (or an intermediate property) when ready; show a placeholder (or default image) until then.

**Pros**

- UI stays responsive; no blocking on first paint.
- User sees grid immediately with placeholders, then images appear as they complete.

**Cons**

- Requires a different binding model: grid binds to an “image source” (e.g. `Bitmap?`) that is updated when load completes, rather than binding directly to path and converting in sync.
- More code: async load service or helper, cancellation when path changes, and proper disposal of old bitmaps.

**Implementation outline**

1. ViewModel exposes for each slot either the path (for placeholder) or a `Bitmap?` thumbnail (for display). Or use a wrapper type (e.g. `PhotoSlot { Path, Thumbnail }`).
2. When path is set (e.g. in `SetEntryPhoto`/`SetExitPhoto`), start an async load (e.g. `Task.Run` + `Bitmap.DecodeToWidth`) and on completion set the thumbnail property on the main thread.
3. Grid binds `Image.Source` to the thumbnail (or to a converter that takes path + thumbnail and returns thumbnail if not null, else default).
4. Cancel or ignore in-flight load when path changes; dispose previous bitmap when replacing.

---

### 3.4 Option D: Pre-generated thumbnail files (server or local)

**Idea**: Store or generate a separate small file per photo (e.g. `photo_xxx_thumb.jpg`) and use that for the grid; full-size file only for ImageViewer/print.

**Pros**

- Grid only ever loads small files; no decode of 7–8 MB in the client.
- Can be done at capture/upload time (server or client) so no runtime cost.

**Cons**

- Requires pipeline changes (where and when thumbnails are created and stored).
- More storage and logic (path resolution: full vs thumb).

**Implementation outline**

1. Define a convention (e.g. `{originalPath}.thumb.jpg` or under a `Thumbs` folder).
2. At capture/upload or in a background job, generate thumbnails (e.g. 480px longest side, JPEG quality 85).
3. PhotoGridViewModel (or attachment model) exposes a “thumbnail path” for grid and “full path” for viewer; grid binds to thumbnail path.
4. Converter (or existing) loads from thumbnail path when present; fallback to full path with DecodeToWidth if no thumb (optional, for backward compatibility).

---

## 4. Recommendation

| Priority | Option | Effort | Impact | Note |
|----------|--------|--------|--------|------|
| 1        | **A** (thumbnail decode in grid) | Low | High | Do first: single converter change, use `DecodeToWidth` in grid only. |
| 2        | **B** (thumbnail cache)          | Medium | Medium | Add after A to avoid repeated decode on tab/selection change. |
| 3        | **C** (async + placeholder)      | Medium | UX | Optional: smoother first paint; implement if A+B still feels slow. |
| 4        | **D** (pre-generated thumbs)     | Higher | High (long-term) | Consider if capture/upload pipeline can be changed. |

**Suggested implementation order**

1. **Implement Option A**: Add a grid-specific thumbnail converter using `Bitmap.DecodeToWidth(stream, 480)` (or 600) for local files; use it in `AttendedWeighingMainView.axaml` and `PhotoGridView.axaml`. Keep full decode for ImageViewer and PrintPreview.
2. **Measure**: Compare load time and memory with 8 × 7–8 MB images before/after.
3. **If needed**, add Option B (cache) and optionally C (async + placeholder). Option D is a longer-term improvement if the team controls the photo pipeline.

---

## 5. Files to Touch (for Option A)

| File | Change |
|------|--------|
| `MaterialClient/Converters/CarNullOrEmptyImageConverter.cs` | Add parameter for max decode width and use `DecodeToWidth` when set; or add new `CarThumbnailImageConverter` that uses `DecodeToWidth`. |
| `MaterialClient/App.axaml` | Register the thumbnail converter if a new one is added. |
| `MaterialClient/Views/AttendedWeighing/AttendedWeighingMainView.axaml` | Use thumbnail converter for all 8 `Image` `Source` bindings. |
| `MaterialClient/Views/PhotoGridView.axaml` | Use thumbnail converter for all 8 `Image` `Source` bindings. |
| `ImageViewerWindow.axaml` / `PrintPreviewWindow.axaml` | Keep using full-resolution converter (no change). |

---

## 6. References

- Avalonia `Bitmap.DecodeToWidth` / `DecodeToHeight`: [API docs](https://api-docs.avaloniaui.net/docs/M_Avalonia_Media_Imaging_Bitmap_DecodeToWidth).
- Current converter: `MaterialClient/Converters/CarNullOrEmptyImageConverter.cs` (line 50: `return new Bitmap(absolutePath)`).
- Photo path source: `PhotoGridViewModel` sets `EntryPhoto1`–`ExitPhoto4` from `file.LocalPath` in `LoadFromWeighingRecordAsync` / `LoadFromWaybillAsync` / `LoadFromListItemAsync`.
