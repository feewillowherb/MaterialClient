## 1. Service Layer Changes

- [x] 1.1 In `IWeighingMatchingService`, add `public const decimal ManualMatchMinWeightDiff = 0.1m;`
- [x] 1.2 In `WeighingMatchingService`, add `private const decimal ManualMatchMinWeightDiff = 0.1m;` (or reference interface constant)
- [x] 1.3 In `WeighingMatchingService.GetCandidateRecordsAsync`, add optional parameter `decimal? minWeightDiffOverride = null`; when provided, use it instead of `_minWeightDiff` in the `TryMatch` call
- [x] 1.4 In `WeighingMatchingService.ManualMatchAsync`, replace `_minWeightDiff` with `ManualMatchMinWeightDiff` constant in the `TryMatch` call

## 2. ViewModel Layer Changes

- [x] 2.1 In `ManualMatchWindowViewModel.LoadCandidateRecordsAsync`, pass `IWeighingMatchingService.ManualMatchMinWeightDiff` as `minWeightDiffOverride` to `GetCandidateRecordsAsync`
