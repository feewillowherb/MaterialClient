# Legacy Documents Archive Manifest

**Archive Date**: 2026-01-15
**Archive Reason**: OpenSpec workflow transition - consolidating legacy documentation
**Archive Package**: legacy-docs-20260115.zip
**Archive Location**: project root /archive/

---

## Purpose

This archive contains Markdown documents that were marked as **ARCHIVED** or **DEPRECATED** during the OpenSpec documentation reorganization (Change ID: `md-milestone-document-organization`). These documents are preserved for historical reference but are no longer actively maintained.

---

## Archive Contents

### Summary Statistics

- **Total Files**: 48 documents
- **Total Size**: ~426 KB
- **Date Range**: Before OpenSpec adoption (2026-01-15)
- **Status Breakdown**:
  - ARCHIVED: 24 files (227 KB)
  - SUPERSEDED: 24 files (199 KB)

---

## Directory Structure

```
archive/
├── legacy-docs-20260115.zip
├── LEGACY_DOCS_MANIFEST.md (this file)
└── extracted-structure/
    ├── specs/                    # Legacy specifications (SUPERSEDED)
    │   ├── 001-attended-weighing/
    │   ├── 001-entity-init/
    │   └── 002-login-auth/
    ├── ReadOnlyMd/               # Technical analysis (ARCHIVED)
    ├── ReadonlyMd/               # Historical docs (ARCHIVED)
    └── docs/                     # Agent reports (ARCHIVED)
```

---

## File Inventory

### specs/ Directory (24 files - SUPERSEDED)

All legacy specification files superseded by OpenSpec workflow.

#### 001-attended-weighing/ (9 files)
- `spec.md` - Feature specification
- `research.md` - Research document
- `plan.md` - Implementation plan
- `tasks.md` - Task list
- `data-model.md` - Data model specification
- `quickstart.md` - Quick start guide
- `checklists/requirements.md` - Requirements checklist
- `contracts/README.md` - Contracts documentation

#### 001-entity-init/ (9 files)
- `spec.md` - Feature specification
- `research.md` - Research document
- `plan.md` - Implementation plan
- `tasks.md` - Task list
- `data-model.md` - Data model specification
- `quickstart.md` - Quick start guide
- `checklists/requirements.md` - Requirements checklist
- `contracts/README.md` - Contracts documentation

#### 002-login-auth/ (9 files)
- `spec.md` - Feature specification
- `research.md` - Research document
- `plan.md` - Implementation plan
- `tasks.md` - Task list
- `data-model.md` - Data model specification
- `quickstart.md` - Quick start guide
- `checklists/requirements.md` - Requirements checklist
- `contracts/README.md` - Contracts documentation

### ReadOnlyMd/ Directory (7 files - ARCHIVED)

Technical analysis and implementation notes.

- `AttendedWeighingStatus状态机设计评估报告.md` - State machine design evaluation (21 KB)
- `Avalonia ComboBox绑定问题分析报告.md` - UI binding issue analysis (5.6 KB)
- `TruckScaleWeightService背压风险评估报告.md` - Backpressure risk assessment (26 KB)
- `重量稳定性监控优化分析.md` - Weight stability monitoring analysis (18 KB)
- `NET_DVR_RealPlay_V40.md` - Hikvision SDK API docs (5 KB)
- `物料定义实体.md` - Material entity definition (1.8 KB)
- `称重拍照实现.md` - Weighing photo implementation (1.4 KB)

### ReadonlyMd/ Directory (3 files - ARCHIVED)

Historical documentation and implementation records.

- `cap.md` - Capacity planning document (152 KB) - **LARGEST FILE**
- `有人值守实现.md` - Attended weighing implementation (4.6 KB)
- `登录页面.md` - Login page documentation (3.8 KB)

### docs/ Directory (14 files - ARCHIVED)

Agent-generated analysis and implementation reports.

- `HikvisionOpenStream-Crash-Analysis-Report.md` - Crash analysis (48 KB)
- `AttendedWeighingService-RxState-Optimization-Report.md` - Rx optimization (29 KB)
- `ReaderWriterLockSlim-Performance-Evaluation.md` - Performance evaluation (22 KB)
- `AttendedWeighingDetailView-Code-Analysis-2025-12-22.md` - Code analysis (15 KB)
- `AttendedWeighingService-Rx-Evaluation-Report.md` - Rx evaluation (16 KB)
- `AttendedWeighingDetailView-Code-Changes-2025-12-22.md` - Code changes (14 KB)
- `TruckScaleWeightService-Optimization-2025-12-22.md` - Optimization report (12 KB)
- `AttendedWeighingDetailView-Optimization-Summary-2025-12-22.md` - Optimization summary (9 KB)
- `Complete-Crash-Fix-Summary.md` - Crash fix summary (7 KB)
- `Port-Pool-Integration-Fix.md` - Port pool fix (8 KB)
- `ReaderWriterLockSlim-Performance-Summary.md` - Performance summary (4 KB)
- `AttendedWeighingDetailView-Performance-Optimization.md` - Performance optimization (7 KB)
- `内存溢出问题分析报告.md` - Memory leak analysis (9 KB)
- `agents/avalonia-reactiveui-threading-2025-01-31.md` - Threading analysis (6 KB)
- `agents/hikvision-agent-2025-10-30.md` - Hikvision agent report (1.7 KB)
- `agents/TruckScaleWeightService-Optimization-2025-12-22.md` - Service optimization (12 KB)

---

## Metadata Tags

Each archived file contains the following metadata header:

```markdown
<!--
DOCUMENT_STATUS: [ARCHIVED/SUPERSEDED]
LAST_REVIEWED: 2026-01-15
REVIEWER: Claude (OpenSpec Migration)
NOTES: [Status explanation and archival reason]
-->
```

---

## Migration Path

### For SUPERSEDED Documents (specs/)

**New Location**: `openspec/archive/legacy/specs/`
**Access Method**: Extract from archive or access via redirect stub

### For ARCHIVED Documents

**New Location**: This archive package
**Access Method**: Extract when needed for historical reference
**Retention**: Permanent (kept in git history)

---

## Recovery Instructions

### To Extract All Files:

```bash
cd archive
unzip legacy-docs-20260115.zip
```

### To Extract Specific Directory:

```bash
cd archive
unzip legacy-docs-20260115.zip "specs/*"
```

### To Extract Specific File:

```bash
cd archive
unzip legacy-docs-20260115.zip "ReadOnlyMd/AttendedWeighingStatus状态机设计评估报告.md"
```

---

## Valid Documents (NOT in Archive)

The following documents were marked as VALID and are NOT included in this archive:

1. `ReadOnlyMd/系统配置.md` - System configuration (migrated to OpenSpec docs/)
2. `docs/TimerToRx.md` - Timer to Rx migration pattern (migrated to OpenSpec docs/)
3. `docs/hikvision-integration.md` - Hikvision integration (migrated to OpenSpec docs/)

---

## Archive Integrity

**Archive Creation Date**: 2026-01-15
**Archive Creator**: Claude (OpenSpec Migration Agent)
**Archive Method**: ZIP compression with standard compression
**Checksum Verification**: 

```bash
# Verify archive integrity
unzip -t legacy-docs-20260115.zip
```

---

## Contact Information

**Questions About Archive**:
- Reference: `openspec/changes/md-milestone-document-organization/proposal.md`
- Review: `openspec/changes/md-milestone-document-organization/validity-assessment-report.md`
- Dependencies: `openspec/changes/md-milestone-document-organization/dependency-analysis-report.md`

**Archive Restoration**:
If archive is damaged or lost, all files remain in git history prior to deletion commit.

---

## Revision History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2026-01-15 | Initial archive creation |

---

**End of Manifest**
