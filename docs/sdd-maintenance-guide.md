# SDD Maintenance Guide

**Version**: 1.0
**Created**: 2026-01-15
**Owner**: Lead Architect / Tech Lead

---

## Purpose

This guide defines the process for maintaining the Software Design Document (SDD) to ensure it remains accurate and useful throughout the project lifecycle.

---

## Maintenance Schedule

### Quarterly Reviews

**Frequency**: Every 3 months (January, April, July, October)

**Activities**:
1. Review technology stack versions - Update if changed
2. Verify architecture diagrams match implementation
3. Check ADRs for accuracy - Update or deprecate if needed
4. Review technical debt status - Update priorities/resolutions
5. Validate data model documentation - Update entity changes
6. Update constraint documentation - Add new constraints as discovered

**Responsibility**: Lead Architect

---

## Update Triggers

### Automatic Triggers

The SDD MUST be updated when:

| Trigger | Section(s) to Update | Timeline |
|---------|---------------------|----------|
| New service added | Module Design (Section 2) | Before PR merge |
| Service interface changed | Module Design (Section 2) | Before PR merge |
| New entity added | Data Model (Section 4) | Before PR merge |
| Entity schema changed | Data Model (Section 4) | Before PR merge |
| Major architecture change | Architecture Overview (Section 1), Diagrams (Section 5) | Before PR merge |
| Technology version upgrade | Technology Stack (Section 1.2) | Before upgrade |
| New ADR needed | Technical Decisions (Section 6) | When decision made |
| Performance issue identified | Constraints & Risks (Section 7.4) | When discovered |
| Technical debt resolved | Technical Debt (Section 7.4) | When resolved |

### Manual Triggers

The SDD SHOULD be updated when:
- Code review reveals documentation gaps
- Onboarding feedback indicates unclear areas
- Architecture changes are planned
- Retrospective identifies documentation needs

---

## Update Process

### 1. Identify Update Need

**Who**: Any team member

**How**:
- Create GitHub issue with label `documentation`
- Tag issue with `sdd` label
- Describe what needs updating and why

**Example Issue Template**:
```markdown
## SDD Update Needed

**Section**: Module Design (Section 2)
**Reason**: New service `BarcodeScannerService` added in PR #123
**Changes Required**:
- Add service to catalog table
- Document interface and dependencies
- Add to component diagram if needed

**PR Reference**: #123
**Priority**: High (blocks merge)
```

---

### 2. Make Updates

**Who**: Assignee (usually tech lead or architect)

**Process**:
1. Checkout `main` branch
2. Create `docs/update-sdd-<section>-<date>` branch
3. Edit `docs/SDD.md`
4. Update document metadata (Last Updated date)
5. Commit with descriptive message
6. Create PR for review

**Commit Message Format**:
```
docs(sdd): Update Module Design section

- Add BarcodeScannerService to service catalog
- Document interface and dependencies
- Update component diagram

Refs: #124
```

---

### 3. Review and Merge

**Review Checklist**:
- [ ] Technical accuracy verified
- [ ] Consistency with codebase
- [ ] No conflicting information
- [ ] Diagrams render correctly (Mermaid syntax)
- [ ] Tables formatted properly
- [ ] Links/anchors work

**Approval**: Tech Lead or Architect

**Merge**: Squash merge to `main`

---

## Section-Specific Guidelines

### Section 1: Architecture Overview

**When to Update**:
- Technology stack version changes
- New architectural patterns introduced
- System boundaries change

**Review Focus**:
- Version numbers accurate
- Pattern descriptions match implementation
- Positioning statement still accurate

---

### Section 2: Module Design

**When to Update**:
- New service added
- Service interface changed
- New dependencies added

**Review Focus**:
- Service catalog complete
- Interfaces match code
- Dependencies accurate
- State management approach documented

---

### Section 3: State Management Architecture

**When to Update**:
- New Rx patterns introduced
- State management refactored
- Performance optimizations applied

**Review Focus**:
- Rx patterns documented
- Disposal patterns followed
- Threading model accurate

---

### Section 4: Data Model

**When to Update**:
- New entity added
- Entity schema changed
- New enum added
- Relationships changed

**Review Focus**:
- All entities listed
- Fields match code
- Relationships accurate
- Enums documented

---

### Section 5: Architecture Diagrams

**When to Update**:
- New components added
- Data flows changed
- Deployment changed

**Review Focus**:
- Mermaid syntax valid
- Components current
- Relationships accurate
- All diagrams render

---

### Section 6: Technical Decisions (ADRs)

**When to Update**:
- New technical decision made
- Existing decision reversed
- Decision implemented/deprecated

**Review Focus**:
- Status current (Accepted/Superseded/Deprecated)
- Context still accurate
- Consequences documented

**ADR Status Changes**:
```markdown
**Status**: Superseded by ADR-012
**Superseded**: 2026-02-01
**Reason**: Migrated to unified state pattern
```

---

### Section 7: Constraints & Risks

**When to Update**:
- New constraint discovered
- Technical debt resolved
- Risk mitigated

**Review Focus**:
- Constraints accurate
- Technical debt current
- Priority levels appropriate

---

### Section 8: Development Guidelines

**When to Update**:
- New best practices identified
- Coding standards changed
- Testing strategy updated

**Review Focus**:
- Guidelines followed
- Examples current
- Test coverage goals met

---

## Documentation Quality Standards

### Accuracy

- All code references must be accurate
- Version numbers must match actual versions
- Diagrams must reflect actual architecture

**Verification**:
```bash
# Check for outdated references
grep -r "Version.*9\." docs/SDD.md  # Should be 10.x
```

---

### Completeness

- All services documented
- All entities listed
- All ADRs for major decisions present

**Checklist**:
- Service catalog matches `Services/` directory
- Entity list matches `Entities/` directory
- ADR count matches significant decisions

---

### Clarity

- Technical jargon explained
- Acronyms defined on first use
- Examples provided for complex concepts

**Review**:
- Can new developer understand architecture?
- Can AI assistant parse documentation?

---

### Consistency

- Terminology consistent throughout
- Formatting follows style guide
- Diagrams use consistent notation

**Enforcement**:
- Use predefined templates (ADR, service docs)
- Markdown linter (markdownlint)
- Peer review

---

## OpenSpec Integration

### Pre-Commit Checklist

Add to OpenSpec workflow:

```yaml
# .openspec/workflows/feature-workflow.yml
check_sdd_update:
  - if: "changes affect architecture or module design"
    check: |
      Has docs/SDD.md been updated?
      Are affected sections current?
```

---

### Code Review Checklist

Add to PR template:

```markdown
## Documentation

- [ ] SDD updated if architecture changed
- [ ] New services documented in Module Design
- [ ] New entities documented in Data Model
- [ ] ADR created for significant decisions
```

---

## Roles and Responsibilities

### Lead Architect

**Primary Responsibilities**:
- Own SDD maintenance schedule
- Review and approve all updates
- Conduct quarterly reviews
- Resolve documentation conflicts

**Time Allocation**: 2-4 hours per month

---

### Tech Lead

**Primary Responsibilities**:
- Review documentation updates in PRs
- Identify documentation gaps during code reviews
- Mentor team on documentation practices

**Time Allocation**: 1-2 hours per week

---

### Developers

**Primary Responsibilities**:
- Update documentation for their changes
- Identify and report documentation gaps
- Follow documentation guidelines

**Time Allocation**: Per change (typically 15-30 minutes)

---

## Tools and Resources

### Markdown Linting

```bash
# Install markdownlint
npm install -g markdownlint-cli

# Check SDD
markdownlint docs/SDD.md

# Auto-fix issues
markdownlint docs/SDD.md --fix
```

### Mermaid Validation

```bash
# Use Mermaid live editor
# https://mermaid.live/
# Paste diagrams to validate syntax
```

### Link Checking

```bash
# Find broken anchors
grep -n '\[.*\](#' docs/SDD.md | while read line; do
  anchor=$(echo "$line" | sed 's/.*#\(.*\)/\1/')
  if ! grep -q "### $anchor" docs/SDD.md; then
    echo "Broken anchor: $anchor"
  fi
done
```

---

## Metrics

### Track These Metrics

| Metric | Target | Measurement |
|--------|--------|-------------|
| Documentation freshness | < 1 month stale | Time since last update |
| PR documentation compliance | > 90% | PRs with SDD updates |
| Onboarding time | < 1 week | New developer time to productivity |
| Documentation issues | < 5 per quarter | GitHub issues labeled `documentation` |

### Quarterly Report Template

```markdown
# SDD Maintenance Report - QX 2026

## Updates Made
- Section X updated: [description]
- ADR-XXX added: [description]

## Metrics
- Documentation freshness: X days
- PR compliance: XX%
- Onboarding time: X weeks

## Issues Identified
- [List gaps or inaccuracies found]

## Action Items
- [ ] [Next quarter priorities]
```

---

## Escalation Path

### Documentation Gaps

1. **Developer** identifies gap → Creates issue
2. **Tech Lead** prioritizes → Assigns to developer
3. **Developer** updates → Creates PR
4. **Tech Lead** reviews → Approves/requests changes
5. **Lead Architect** approves → Merges

### Conflicting Information

1. **Any team member** identifies conflict → Creates issue
2. **Tech Lead** investigates → Determines correct version
3. **Lead Architect** resolves → Updates documentation
4. **Team** notified → Announcement in standup

---

## Continuous Improvement

### Retrospective

**Frequency**: Annually

**Activities**:
- Review maintenance process effectiveness
- Identify pain points
- Update guidelines based on lessons learned
- Adjust time allocations if needed

**Output**: Updated maintenance guide

---

### Training

**New Developer Onboarding**:
1. Review SDD with new developer
2. Explain maintenance process
3. Assign documentation buddy
4. Monitor first documentation update

**Team Training**:
- Quarterly documentation workshop
- Share documentation best practices
- Review tools and automation

---

## Appendix

### Quick Reference

| Task | Command/File | Notes |
|------|--------------|-------|
| Update SDD | Edit `docs/SDD.md` | Update metadata date |
| Check links | `markdownlint` | Run before commit |
| Validate diagrams | Mermaid Live Editor | https://mermaid.live/ |
| Create ADR | Use ADR template | Section 6 format |
| Report issue | GitHub label `sdd` | Include section reference |

---

### Contact

**SDD Owner**: Lead Architect
**Process Questions**: Tech Lead
**Tool Support**: DevOps Engineer

---

**Document Version**: 1.0
**Last Updated**: 2026-01-15
**Next Review**: 2026-04-15
