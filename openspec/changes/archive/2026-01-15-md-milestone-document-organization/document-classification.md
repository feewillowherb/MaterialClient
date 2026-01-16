# Document Classification Summary

**Change ID**: md-milestone-document-organization
**Generated**: 2026-01-15
**Total Documents**: 51

---

## Classification by Type

### 1. Specifications (Legacy)
- **Location**: `specs/`
- **Count**: 24 files across 3 features
- **Features**:
  - `001-attended-weighing`: 9 files (spec, research, plan, tasks, data-model, quickstart, etc.)
  - `001-entity-init`: 9 files (same structure)
  - `002-login-auth`: 9 files (same structure)
- **Type**: Legacy functional specifications
- **Current Relevance**: These are pre-OpenSpec specifications, now superseded by OpenSpec workflow

### 2. Technical Analysis Reports
- **Location**: `ReadOnlyMd/`, `ReadonlyMd/`
- **Count**: 8 files
- **Examples**:
  - `AttendedWeighingStatus状态机设计评估报告.md` - State machine design evaluation
  - `Avalonia ComboBox绑定问题分析报告.md` - UI binding issue analysis
  - `TruckScaleWeightService背压风险评估报告.md` - Backpressure risk assessment
  - `重量稳定性监控优化分析.md` - Weight stability monitoring analysis
- **Type**: Technical analysis and design documents
- **Current Relevance**: Mixed - some describe resolved issues, others document ongoing concerns

### 3. Implementation Notes
- **Location**: `ReadOnlyMd/`, `ReadonlyMd/`
- **Count**: 2 files
- **Examples**:
  - `称重拍照实现.md` - Weighing photo implementation
  - `有人值守实现.md` - Attended weighing implementation
- **Type**: Implementation documentation
- **Current Relevance**: Likely historical records of completed features

### 4. Configuration and Setup
- **Location**: `ReadOnlyMd/`, `ReadonlyMd/`
- **Count**: 2 files
- **Examples**:
  - `系统配置.md` - System configuration
  - `cap.md` - Capacity/planning document
- **Type**: Configuration documentation
- **Current Relevance**: May contain current config or be outdated

### 5. Agent-Generated Reports
- **Location**: `docs/`, `docs/agents/`
- **Count**: 10 files
- **Examples**:
  - `HikvisionOpenStream-Crash-Analysis-Report.md` - Crash analysis (48KB - largest file)
  - `AttendedWeighingService-RxState-Optimization-Report.md` - Rx optimization report
  - `ReaderWriterLockSlim-Performance-Evaluation.md` - Performance evaluation
  - `TruckScaleWeightService-Optimization-2025-12-22.md` - Optimization report
- **Type**: AI-generated analysis and optimization reports
- **Current Relevance**: Mixed - some issues resolved, others may need attention

### 6. Implementation Reports
- **Location**: `docs/`
- **Count**: 7 files
- **Examples**:
  - `Complete-Crash-Fix-Summary.md` - Crash fix summary
  - `Port-Pool-Integration-Fix.md` - Port pool integration fix
  - `AttendedWeighingDetailView-Performance-Optimization.md` - Performance optimization
- **Type**: Implementation completion reports
- **Current Relevance**: Historical records of completed work

### 7. General Documentation
- **Location**: `docs/`, `ReadonlyMd/`, `ReadOnlyMd/`
- **Count**: 5 files
- **Examples**:
  - `hikvision-integration.md` - Hikvision integration docs
  - `TimerToRx.md` - Timer to Rx migration guide
  - `物料定义实体.md` - Material entity definition
  - `登录页面.md` - Login page documentation
- **Type**: General technical documentation
- **Current Relevance**: Varies by topic

---

## Initial Status Assessment

### Likely SUPERSEDED (OpenSpec equivalents exist)
- All 24 files in `specs/` directory (legacy spec format, replaced by OpenSpec)

### Likely ARCHIVED (Historical records, issues resolved)
- `ReadOnlyMd/AttendedWeighingStatus状态机设计评估报告.md` - Design evaluation, likely implemented
- `ReadOnlyMd/Avalonia ComboBox绑定问题分析报告.md` - Bug analysis, likely resolved
- `docs/HikvisionOpenStream-Crash-Analysis-Report.md` - Crash analysis, may be resolved (verify)
- `docs/Complete-Crash-Fix-Summary.md` - Fix summary, historical record
- `docs/Port-Pool-Integration-Fix.md` - Fix record, historical
- Various implementation reports

### Likely VALID (Current reference value)
- `ReadOnlyMd/系统配置.md` - System config (verify if current)
- `docs/hikvision-integration.md` - Integration docs (may be current)
- `docs/TimerToRx.md` - Technical pattern documentation
- Some agent reports with pending recommendations

### Needs Review (Unclear status)
- `ReadonlyMd/cap.md` - Unclear purpose, needs review
- `ReadOnlyMd/NET_DVR_RealPlay_V40.md` - API documentation (verify if needed)
- `docs/内存溢出问题分析报告.md` - Memory leak analysis (verify if resolved)
- Reports with unclear implementation status

---

## Storage Analysis

**Total Size**: ~446 KB
**Largest Files**:
1. `ReadonlyMd/cap.md` - 152 KB
2. `docs/HikvisionOpenStream-Crash-Analysis-Report.md` - 48 KB
3. `docs/AttendedWeighingService-RxState-Optimization-Report.md` - 29 KB
4. `specs/002-login-auth/research.md` - 30 KB
5. `specs/002-login-auth/quickstart.md` - 25 KB

**Directory Breakdown**:
- `specs/`: ~199 KB (45%)
- `docs/`: ~88 KB (20%)
- `ReadOnlyMd/`: ~77 KB (17%)
- `ReadonlyMd/`: ~162 KB (36%)

---

## Recommendations

1. **All `specs/` content**: Migrate to OpenSpec archive format, mark as SUPERSEDED
2. **ReadOnlyMd/ReadonlyMd analysis reports**: Review for current relevance, likely ARCHIVE
3. **Agent reports**: Check implementation status, ARCHIVE if resolved, VALID if pending
4. **General docs**: Review individually for current relevance
5. **Configuration files**: Verify against actual system configuration

---

## Next Steps

1. Annotate each document with formal metadata header
2. Conduct team review to validate classifications
3. Create dependency analysis
4. Execute compression and migration strategy
