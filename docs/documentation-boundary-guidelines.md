# Documentation Boundary Guidelines

**Version**: 1.0
**Effective Date**: 2026-01-15
**Change Reference**: `md-milestone-document-organization`

---

## Overview

This document establishes clear boundaries between legacy documentation practices and the current OpenSpec specification-driven development workflow. All team members should follow these guidelines when creating, storing, or maintaining project documentation.

---

## Temporal Boundary

### Cutoff Date: 2026-01-15

**Definition**: The date OpenSpec workflow was officially adopted as the project's primary specification process.

#### Legacy Period (Before 2026-01-15)

- **Documentation Style**: Ad-hoc Markdown documents in various directories
- **Locations**: `specs/`, `ReadOnlyMd/`, `ReadonlyMd/`, `docs/`
- **Management**: Scattered, no unified process
- **Status**: Archived in `archive/legacy-docs-20260115.tar.gz`
- **Maintenance**: **NO UPDATES** - Read-only historical reference

#### Current Period (2026-01-15 and Later)

- **Documentation Style**: OpenSpec specification format
- **Locations**: `openspec/specs/`, `openspec/changes/`, `openspec/archive/`
- **Management**: Unified, specification-driven workflow
- **Status**: Active, maintained, reviewed
- **Maintenance**: **ACTIVE MAINTENANCE** - Follow OpenSpec validation process

---

## Process Boundary

### Old Process: Legacy Documentation (Deprecated)

**Characteristics**:
- Individual authors creating standalone documents
- No standardized format or structure
- No formal review or approval process
- Scattered locations, inconsistent naming
- Unclear validity and relevance
- Mixed documentation types (specs, analysis, reports, notes)

**Typical Workflow**:
```
Author → Write MD → Save to random directory → Forgotten
```

**Problems**:
- Documents become outdated quickly
- No way to track implementation status
- Difficult to find current information
- Duplicate or contradictory documents
- No clear ownership or maintenance

### New Process: OpenSpec Workflow (Current)

**Characteristics**:
- Structured change proposal workflow
- Standardized formats (proposal.md, design.md, tasks.md)
- Formal review and approval gates
- Centralized location with clear hierarchy
- Clear status tracking (Draft, Approved, In Progress, Completed)
- Traceability from proposal to implementation

**Typical Workflow**:
```
Idea → Create Proposal → Design → Tasks → Review → Approval → Implementation → Archive
```

**Benefits**:
- All specifications go through review process
- Implementation tasks linked to specifications
- Clear history of changes and decisions
- Single source of truth for project capabilities
- Consistent documentation quality

---

## Directory Boundary

### Legacy Locations (DO NOT USE)

These directories are **ARCHIVED**. Do not add new documents here.

| Directory | Status | Action |
|-----------|--------|--------|
| `specs/` | **ARCHIVED** | All files in `archive/legacy-docs-20260115.tar.gz` |
| `ReadOnlyMd/` | **ARCHIVED** | All files in archive, except `系统配置.md` |
| `ReadonlyMd/` | **ARCHIVED** | All files in archive |
| `docs/` | **ARCHIVED** | Most files in archive, see exceptions below |

**Exceptions** (Migrated to OpenSpec):
- `docs/TimerToRx.md` → `openspec/docs/timer-to-rx-pattern.md`
- `docs/hikvision-integration.md` → `openspec/docs/hikvision-integration.md`

### Current Locations (USE THESE)

All new documentation should be created in the OpenSpec structure.

```
openspec/
├── specs/                   # Current capability specifications
│   ├── <capability-name>/
│   │   ├── spec.md         # Capability specification
│   │   ├── design.md       # Design documentation (optional)
│   │   └── tasks.md        # Implementation tasks
│   └── _template/          # Specification template
│
├── changes/                 # Active and completed change proposals
│   ├── <change-id>/
│   │   ├── proposal.md     # Change proposal
│   │   ├── design.md       # Design doc (optional)
│   │   └── tasks.md        # Implementation tasks
│   └── archive/            # Completed changes
│       └── <change-id>/
│
├── archive/                 # Archived content
│   └── legacy/             # Migrated legacy documents
│       └── specs/          # Legacy specs (read-only)
│
├── docs/                    # OpenSpec process documentation
│   ├── documentation-boundary-guidelines.md (this file)
│   ├── AGENTS.md           # Agent guidelines
│   ├── project.md          # Project overview
│   └── PROPOSAL_DESIGN_GUIDELINES.md
│
├── AGENTS.md                # AI agent instructions
├── project.md               # Project overview
└── PROPOSAL_DESIGN_GUIDELINES.md
```

---

## Maintenance Responsibility Boundary

### Legacy Documents: Archive Only

**Maintenance Policy**: **READ-ONLY - NO UPDATES**

- **Purpose**: Historical reference and archival
- **Updates**: **NOT PERMITTED**
- **Corrections**: If critical error found, create new OpenSpec document with correction
- **Access**: Extract from archive when needed
- **Location**: `archive/legacy-docs-20260115.tar.gz`

**Example**: If you find an error in a legacy analysis report:
1. Do NOT update the archived document
2. Create a new OpenSpec document noting the correction
3. Reference the legacy document for context

### Current Documents: Active Maintenance

**Maintenance Policy**: **ACTIVE MAINTENANCE ENCOURAGED**

- **Purpose**: Current project specifications and change proposals
- **Updates**: Follow OpenSpec review process
- **Corrections**: Update via change proposal or direct edit with review
- **Access**: Direct access in `openspec/` directory
- **Location**: `openspec/specs/`, `openspec/changes/`, `openspec/docs/`

**Example**: If you need to update a capability specification:
1. Create change proposal or update existing spec
2. Follow OpenSpec review process
3. Update specification document
4. Track changes in git

---

## Document Category Boundary

### Legacy Document Categories (ARCHIVED)

| Category | Examples | Status | New Home |
|----------|----------|--------|----------|
| Legacy Specifications | `specs/001-attended-weighing/spec.md` | SUPERSEDED | `openspec/archive/legacy/specs/` |
| Technical Analysis | `ReadOnlyMd/*分析报告.md` | ARCHIVED | Archive package |
| Implementation Notes | `ReadOnlyMd/*实现.md` | ARCHIVED | Archive package |
| Agent Reports | `docs/*Report.md` | ARCHIVED | Archive package |
| Configuration | `ReadOnlyMd/系统配置.md` | VALID | `openspec/docs/system-configuration.md` |

### Current Document Categories (ACTIVE)

| Category | Template | Location | Status |
|----------|----------|----------|--------|
| Capability Specifications | `openspec/specs/_template/` | `openspec/specs/<capability>/` | ACTIVE |
| Change Proposals | `openspec/changes/_template/` | `openspec/changes/<id>/` | ACTIVE |
| Design Documents | Standard format | Within change/spec | ACTIVE |
| Implementation Tasks | Standard format | Within change/spec | ACTIVE |
| Process Documentation | N/A | `openspec/docs/` | ACTIVE |

---

## Decision Tree: Where Should I Put This Document?

Use this flowchart to determine where to store new documentation.

```
START: I need to document something
    │
    ├─ Is it a new feature or capability?
    │   └─ YES → Create in: openspec/specs/<capability-name>/
    │           Files: spec.md, design.md (optional), tasks.md
    │
    ├─ Is it a change to existing functionality?
    │   └─ YES → Create in: openspec/changes/<change-id>/
    │           Files: proposal.md, design.md (optional), tasks.md
    │
    ├─ Is it documentation ABOUT the OpenSpec process?
    │   └─ YES → Create in: openspec/docs/
    │           Filename: <topic>.md
    │
    ├─ Is it an update to a legacy document?
    │   └─ YES → DO NOT UPDATE LEGACY
    │           → Create new OpenSpec document
    │           → Reference legacy for context
    │
    ├─ Is it a bug fix or small change?
    │   └─ YES → Create in: openspec/changes/<change-id>/
    │           Follow change proposal process
    │
    └─ Is it general project documentation?
        └─ YES → Create in: openspec/docs/
                Filename: <topic>.md
```

### Example Scenarios

#### Scenario 1: New Feature
**Situation**: We're adding a new "barcode scanning" feature.

**Answer**: Create `openspec/specs/barcode-scanning/` with `spec.md`, `design.md`, `tasks.md`

#### Scenario 2: Bug Fix
**Situation**: We need to fix a crash in the video streaming.

**Answer**: Create `openspec/changes/video-stream-crash-fix/` with `proposal.md` and `tasks.md`

#### Scenario 3: Process Improvement
**Situation**: We want to document our code review process.

**Answer**: Create `openspec/docs/code-review-process.md`

#### Scenario 4: Updating Old Spec
**Situation**: The old `specs/001-attended-weighing/spec.md` needs updates.

**Answer**: DO NOT UPDATE. Create `openspec/changes/attended-weighing-updates/` or update current OpenSpec spec.

---

## Migration Examples

### Example 1: Legacy Spec → OpenSpec

**Before** (Legacy):
```
specs/001-attended-weighing/
├── spec.md
├── plan.md
├── tasks.md
└── ...
```

**After** (OpenSpec):
```
openspec/specs/attended-weighing/
├── spec.md
├── design.md (optional)
└── tasks.md
```

**Migration**: Legacy spec archived, new OpenSpec spec created (if needed)

### Example 2: Analysis Report → Change Proposal

**Before** (Legacy):
```
docs/HikvisionOpenStream-Crash-Analysis-Report.md
```

**After** (OpenSpec):
```
openspec/changes/hikvision-crash-fix/
├── proposal.md     # Problem statement and solution
├── design.md       # Technical design
└── tasks.md        # Implementation tasks
```

**Migration**: Analysis report archived, change proposal created for fixes

---

## Quick Reference Card

### DO's ✓

- ✓ Create new specs in `openspec/specs/`
- ✓ Create change proposals in `openspec/changes/`
- ✓ Follow OpenSpec templates and formats
- ✓ Review and update current OpenSpec documents
- ✓ Reference legacy documents from new OpenSpec docs
- ✓ Extract from archive when historical context needed

### DON'Ts ✗

- ✗ Create new documents in `specs/`, `ReadOnlyMd/`, `ReadonlyMd/`, `docs/`
- ✗ Update existing legacy documents
- ✗ Move files from archive back to source directories
- ✗ Reference legacy docs as "current" - they're archived
- ✗ Use legacy document formats for new specifications

---

## Contact and Support

### Questions About Documentation Boundaries?

**Primary Contact**: Tech Lead
**Reference Documents**:
- This document: `openspec/docs/documentation-boundary-guidelines.md`
- OpenSpec workflow: `openspec/AGENTS.md`
- Project overview: `openspec/project.md`

### Need to Access Legacy Documents?

**Archive Location**: `archive/legacy-docs-20260115.tar.gz`
**Manifest**: `archive/LEGACY_DOCS_MANIFEST.md`
**Extraction**: `tar -xzf archive/legacy-docs-20260115.tar.gz`

### Propose Changes to These Guidelines?

Create a change proposal in `openspec/changes/` following the standard process.

---

## Revision History

| Version | Date | Changes | Author |
|---------|------|---------|--------|
| 1.0 | 2026-01-15 | Initial boundary guidelines | Claude (OpenSpec Migration) |

---

**Document Status**: ACTIVE
**Last Reviewed**: 2026-01-15
**Next Review**: 2026-07-15 (6 months)
**Maintainer**: Tech Lead
