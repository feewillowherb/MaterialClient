## Detail Popup Regression Checklist

### Preconditions
- Build succeeds for `MaterialClient.sln`.
- Use a dataset containing at least one standard-mode item and one solid-waste-mode item.

### Standard Mode
1. Open attended weighing list and open detail popup for a `WeighingMode.Standard` item.
2. Confirm popup renders without exception.
3. Edit standard fields (plate number, provider, material row) and click `保存`.
4. Confirm save succeeds and no binding/cast exception is logged.

### SolidWaste Mode
1. Open attended weighing list and open detail popup for a `WeighingMode.SolidWaste` item.
2. Confirm popup renders without `InvalidCastException`.
3. Edit solid-waste fields and click `保存`.
4. Confirm save succeeds and no binding/cast exception is logged.

### Negative/Regression Focus
- Switching between standard and solid-waste detail popups in the same app session does not trigger compiled-binding cast errors.
- Material selection popup in standard mode still opens and closes correctly.
