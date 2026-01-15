# Team Review Guide

**Change ID**: md-milestone-document-organization
**Review Date**: TBD (Scheduled by team lead)
**Review Type**: Document Validity Assessment Validation

---

## Purpose

This guide outlines the team review process for validating document status annotations and classifications made during Phase 1 of the documentation reorganization.

---

## Review Agenda

### 1. Overview Presentation (15 minutes)

**Presenter**: Tech Lead or OpenSpec Migration Lead
**Topics**:
- OpenSpec adoption background
- Documentation organization problem statement
- Assessment methodology and findings
- Status distribution summary

### 2. Document Classification Review (30 minutes)

**Focus Areas**:

#### A. Superseded Specifications (24 files)
- **Question**: Are all `specs/` files truly superseded by OpenSpec?
- **Decision Point**: Confirm migration to `openspec/archive/legacy/specs/`
- **Key Stakeholder**: Tech Lead, Senior Developers

#### B. Technical Analysis Documents (8 files)
- **Files**: `ReadOnlyMd/` analysis reports
- **Question**: Do any of these contain unresolved issues or ongoing concerns?
- **Key Files to Review**:
  - `AttendedWeighingStatus状态机设计评估报告.md` - State machine evaluation
  - `TruckScaleWeightService背压风险评估报告.md` - Backpressure risk assessment
  - `重量稳定性监控优化分析.md` - Weight stability monitoring
- **Decision Point**: Archive vs. VALID status
- **Key Stakeholder**: Senior Developers, System Architect

#### C. Agent Reports (10 files)
- **Files**: `docs/` agent-generated reports
- **Question**: Have all recommendations been implemented? Are any issues still pending?
- **Key Files to Review**:
  - `HikvisionOpenStream-Crash-Analysis-Report.md` (48 KB)
  - `AttendedWeighingService-RxState-Optimization-Report.md` (29 KB)
  - `ReaderWriterLockSlim-Performance-Evaluation.md` (22 KB)
- **Decision Point**: Confirm archival, identify any pending action items
- **Key Stakeholder**: Senior Developers, Performance Engineer

#### D. Configuration Documentation (1 file)
- **File**: `ReadOnlyMd/系统配置.md`
- **Question**: Is this configuration current? Should it be migrated?
- **Decision Point**: VALID → migrate to OpenSpec docs/ or update → DEPRECATED
- **Key Stakeholder**: DevOps, System Administrator

#### E. Large File Review (1 file)
- **File**: `ReadonlyMd/cap.md` (152 KB - largest file)
- **Question**: What is the purpose of this file? Is it still needed?
- **Decision Point**: Keep as VALID or ARCHIVE
- **Key Stakeholder**: Tech Lead, Project Manager

#### F. Technical Pattern Documentation (2 files)
- **Files**:
  - `docs/TimerToRx.md` - Rx migration pattern
  - `docs/hikvision-integration.md` - Hikvision integration
- **Question**: Are these currently referenced by team members?
- **Decision Point**: Confirm VALID status, migrate to OpenSpec
- **Key Stakeholder**: All Developers

---

## Decision Matrix

Use this matrix to document team decisions for each document category:

| Category | Proposed Status | Team Decision | Rationale | Action |
|----------|----------------|---------------|-----------|--------|
| Legacy specs (24 files) | SUPERSEDED | | | |
| Technical analysis (8 files) | ARCHIVED | | | |
| Agent reports (10 files) | ARCHIVED | | | |
| System config (1 file) | VALID | | | |
| cap.md (1 file) | ARCHIVED | | | |
| Technical patterns (2 files) | VALID | | | |

---

## Review Questions

### For Each Document Category:

1. **Accuracy**: Is the proposed status accurate?
2. **Dependencies**: Does any system component depend on this document?
3. **Alternatives**: Is there a better location/format for this information?
4. **Action Items**: Do any recommendations need implementation before archival?

### Specific Concerns:

1. **Have all crash fixes been verified?**
   - Review: `Complete-Crash-Fix-Summary.md`
   - Verify: No recurring crashes in production

2. **Are all optimization reports complete?**
   - Review: `AttendedWeighingService-RxState-Optimization-Report.md`
   - Verify: Rx optimization fully implemented

3. **Is the system configuration current?**
   - Review: `ReadOnlyMd/系统配置.md`
   - Verify: Matches actual system settings

4. **What is the purpose of cap.md?**
   - Review: `ReadonlyMd/cap.md`
   - Determine: Capacity planning? Requirements? Archive?

---

## Review Outcomes

### Possible Decisions:

1. **Confirm**: Status is correct, proceed with proposed action
2. **Change**: Update status based on team feedback
3. **Defer**: Postpone decision until more information available
4. **Split**: Different files in category need different treatments

### Action Items:

- [ ] All document statuses validated
- [ ] Status annotations updated based on feedback
- [ ] Priority documents identified for migration
- [ ] Dependencies documented
- [ ] Approval to proceed to Phase 2

---

## Post-Review Actions

### If Approved:

1. Update document annotations with any changes
2. Proceed to Task 2.1: SDD Dependency Analysis
3. Execute Phase 2: Compression and Cleanup

### If Changes Required:

1. Update annotations per team decisions
2. Regenerate Validity Assessment Report
3. Schedule follow-up review if needed

---

## Meeting Logistics

**Recommended Attendees**:
- Tech Lead (required)
- Senior Developers (required)
- System Architect (optional but recommended)
- DevOps Engineer (for config review)
- Project Manager (for cap.md review)

**Estimated Duration**: 45-60 minutes

**Required Materials**:
- Validity Assessment Report (printed or shared)
- Document Classification Summary
- This Review Guide
- Project timeline and OpenSpec adoption context

**Decision Making**: Consensus or majority vote with Tech Lead veto authority

---

## Notes Template

```
Meeting Date: _______________
Attendees: ___________________
_________________________________________

Decisions Made:
-
-
-

Action Items:
- [ ]
- [ ]

Concerns Raised:
-
-

Follow-up Required: Yes / No
Next Review Date: _______________
Approval to Proceed: Yes / No
```

---

## Contact Information

**Questions About Review Process**:
- Contact: Tech Lead or OpenSpec Migration Lead
- Reference: `openspec/changes/md-milestone-document-organization/proposal.md`

**Document-Specific Questions**:
- Review the document's metadata header for REVIEWER notes
- Check validity assessment report for detailed rationale

---

**Document Version**: 1.0
**Created**: 2026-01-15
**Created By**: Claude (OpenSpec Migration Agent)
**Status**: Awaiting Team Review
