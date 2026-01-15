# Team Training Guide: OpenSpec Documentation Migration

**Training Date**: TBD (Schedule with team)
**Training Duration**: 45-60 minutes
**Change Reference**: `md-milestone-document-organization`
**Trainer**: Tech Lead

---

## Training Objectives

By the end of this training session, team members will be able to:

1. ✓ Understand the documentation reorganization and OpenSpec adoption
2. ✓ Navigate the new documentation structure
3. ✓ Create new specifications using OpenSpec templates
4. ✓ Create change proposals for modifications
5. ✓ Access archived legacy documents when needed
6. ✓ Follow documentation best practices

---

## Training Agenda

### Part 1: Overview and Motivation (10 minutes)

**Slide 1: Why We're Changing**

- Old documentation scattered across 4 directories
- 51 documents with unclear validity
- No unified review process
- Difficult to find current information

**Slide 2: OpenSpec Solution**

- Unified structure in `openspec/`
- Formal review and approval process
- Clear status tracking
- Single source of truth

**Slide 3: What Changed**

```
OLD: specs/, ReadOnlyMd/, ReadonlyMd/, docs/ (scattered)
NEW: openspec/specs/, openspec/changes/ (organized)
ARCHIVED: archive/legacy-docs-20260115.tar.gz (preserved)
```

**Key Statistics**:
- 51 documents reviewed and annotated
- 48 documents archived (94%)
- 3 documents migrated as VALID
- 44% storage reduction through compression

---

### Part 2: New Documentation Structure (15 minutes)

**Live Demo: Navigating OpenSpec**

```bash
# Show directory structure
tree openspec/ -L 2

# Explore key directories
cd openspec/specs
cd openspec/changes
cd openspec/docs
```

**Directory Overview**:

1. **`openspec/specs/`** - Capability specifications
   - Current features and capabilities
   - Standardized format
   - Active maintenance

2. **`openspec/changes/`** - Change proposals
   - New features, bug fixes, refactorings
   - Review before implementation
   - Tracked through completion

3. **`openspec/docs/`** - Process documentation
   - Guidelines and best practices
   - Includes boundary guidelines
   - Team reference materials

4. **`archive/legacy-docs-20260115.tar.gz`** - Legacy archive
   - All historical documents
   - Read-only access
   - Extract when needed

**Interactive Exercise** (5 minutes):

Ask team to:
1. Navigate to `openspec/specs/_template/`
2. Read the template structure
3. Navigate to `openspec/changes/_template/`
4. Compare the two templates

---

### Part 3: Creating New Documentation (15 minutes)

**Demo 1: Creating a New Specification**

```bash
# 1. Copy the template
cp -r openspec/specs/_template openspec/specs/my-new-feature

# 2. Edit the specification
cd openspec/specs/my-new-feature
# Edit spec.md, design.md (optional), tasks.md

# 3. Delete template README
rm _template/README.md

# 4. Commit and create PR
git add openspec/specs/my-new-feature
git commit -m "Add spec: My New Feature"
git push
```

**Demo 2: Creating a Change Proposal**

```bash
# 1. Copy the template
cp -r openspec/changes/_template openspec/changes/fix-my-bug-2025-01-15

# 2. Edit the proposal
cd openspec/changes/fix-my-bug-2025-01-15
# Edit proposal.md, design.md (optional), tasks.md

# 3. Delete template README
rm _template/README.md

# 4. Submit for review
git add openspec/changes/fix-my-bug-2025-01-15
git commit -m "Propose fix: My Bug"
git push
```

**Best Practices**:

1. **Always use templates** - Ensures consistency
2. **Fill in all required fields** - Complete specifications
3. **Get review before implementation** - Catch issues early
4. **Update status as you progress** - Keep team informed
5. **Archive completed changes** - Clean structure

---

### Part 4: Accessing Legacy Documents (5 minutes)

**When to Access Legacy Docs**:

- Historical context for design decisions
- Understanding implementation history
- Researching past issues and solutions
- Reference when updating old features

**How to Access**:

```bash
# Extract entire archive
tar -xzf archive/legacy-docs-20260115.tar.gz

# Extract specific directory
tar -xzf archive/legacy-docs-20260115.tar.gz specs/001-attended-weighing/

# Extract specific file
tar -xzf archive/legacy-docs-20260115.tar.gz "ReadOnlyMd/状态机设计评估报告.md"
```

**Important**: Legacy documents are READ-ONLY. Do NOT update them. Create new OpenSpec docs for corrections.

---

### Part 5: Q&A and Discussion (10-15 minutes)

**Common Questions**:

**Q: What if I need to update a legacy document?**
A: DON'T. Create a new OpenSpec document referencing the legacy doc for context.

**Q: Where do I put bug fix documentation?**
A: Create a change proposal in `openspec/changes/<change-id>/`

**Q: Can I still create documents in the old directories?**
A: NO. All new documentation must go in OpenSpec structure.

**Q: What if I can't find what I need in the archive?**
A: Check git history or ask the Tech Lead.

**Q: Do I need to update all legacy docs to OpenSpec format?**
A: NO. Legacy docs are archived as-is. Only create new OpenSpec docs when needed.

---

## Training Materials

### Handout: Quick Reference Card

Distribute the **Decision Tree** from `openspec/docs/documentation-boundary-guidelines.md`:

```
I need to document something
├─ New feature? → openspec/specs/<feature>/
├─ Change/bug fix? → openspec/changes/<id>/
├─ Process docs? → openspec/docs/<topic>.md
└─ Update legacy? → DON'T - create new OpenSpec doc
```

### Presentation Slides

Create slides with:
- Overview of changes
- Before/after comparison
- New directory structure
- Workflow diagrams
- Examples

### Exercise Workbook

Provide hands-on exercises:
1. Create a simple specification
2. Create a change proposal
3. Extract a document from archive
4. Find a specific document in OpenSpec structure

---

## Post-Training Follow-up

### Immediate Actions (Week 1)

1. **Team Communication**
   - Send announcement email (see template below)
   - Post summary in team chat
   - Update project README with link to boundary guidelines

2. **Monitoring**
   - Check that new docs go in OpenSpec structure
   - Answer questions as they arise
   - Collect feedback on process

3. **Support**
   - Office hours for questions
   - Pair programming for first few specs
   - Review first change proposals together

### Ongoing Actions (Month 1)

1. **Weekly Check-ins**
   - Any issues with new structure?
   - Need clarification on process?
   - Suggestions for improvement?

2. **Quality Assurance**
   - Review new specs for template compliance
   - Ensure change proposals follow process
   - Verify proper archiving of completed changes

3. **Process Refinement**
   - Collect team feedback
   - Update templates based on usage
   - Improve documentation as needed

---

## Announcement Email Template

**Subject**: 🔔 Important: Documentation Reorganization - OpenSpec Adoption

**Team**,

We've completed a major reorganization of project documentation to improve clarity and maintainability. Here's what you need to know:

### What Changed

✅ **New Home for Documentation**: All new documentation now lives in `openspec/` directory
✅ **Unified Process**: Standardized templates and review workflow
✅ **Cleaner Structure**: Legacy docs archived, easier to find current info

### Key Actions for You

1. **📚 Read the Guidelines**: `openspec/docs/documentation-boundary-guidelines.md`
2. **🎯 Use Templates**: Always start from templates in `_template/` directories
3. **❓ Ask Questions**: Reach out if you need help navigating the new structure

### Training Session

**When**: [Date and Time]
**Where**: [Location/Link]
**Duration**: 45-60 minutes

We'll cover:
- New documentation structure
- How to create specifications and change proposals
- Accessing archived legacy documents
- Q&A

### Quick Reference

| Need | Location |
|------|----------|
| New feature spec | `openspec/specs/<feature>/spec.md` |
| Bug fix/Change | `openspec/changes/<id>/proposal.md` |
| Process docs | `openspec/docs/<topic>.md` |
| Legacy docs | Extract from `archive/legacy-docs-20260115.tar.gz` |

**DO NOT** create new documents in old directories (`specs/`, `ReadOnlyMd/`, `docs/`).

### Resources

- 📖 Boundary Guidelines: `openspec/docs/documentation-boundary-guidelines.md`
- 📋 Templates: `openspec/specs/_template/` and `openspec/changes/_template/`
- 🗂️ Archive Manifest: `archive/LEGACY_DOCS_MANIFEST.md`

Questions? Contact [Tech Lead] or attend the training session.

Thanks,
[Your Name]

---

## Evaluation and Feedback

### Training Evaluation Form

After training, collect feedback:

1. **Clarity of Presentation** (1-5): ___
2. **Hands-on Exercises Useful?** (Yes/No): ___
3. **Feel Confident Using OpenSpec?** (1-5): ___
4. **What Was Most Helpful?**: _______________
5. **What Needs More Clarification?**: _______________
6. **Additional Topics for Future Training?**: _______________

### Success Metrics

- ✓ 100% of team attends training
- ✓ No new documents created in legacy directories (30 days)
- ✓ At least 5 new specs/change proposals created (30 days)
- ✓ Positive feedback from 80%+ of team
- ✓ Reduced confusion about documentation location

---

## Trainer's Checklist

### Before Training

- [ ] Review all training materials
- [ ] Prepare presentation slides
- [ ] Print quick reference cards
- [ ] Set up demo environment
- [ ] Test directory navigation examples
- [ ] Prepare exercise workbooks
- [ ] Schedule meeting and send calendar invites
- [ ] Send announcement email

### During Training

- [ ] Take attendance
- [ ] Present overview (10 min)
- [ ] Demo new structure (15 min)
- [ ] Show creation workflow (15 min)
- [ ] Demo archive access (5 min)
- [ ] Facilitate Q&A (10-15 min)
- [ ] Distribute materials
- [ ] Collect initial feedback

### After Training

- [ ] Send follow-up email with slides
- [ ] Post recording (if virtual)
- [ ] Schedule office hours
- [ ] Monitor new documentation creation
- [ ] Collect evaluation forms
- [ ] Address any issues raised
- [ ] Plan refresher if needed

---

## Additional Resources

### For Team Members

- **Boundary Guidelines**: `openspec/docs/documentation-boundary-guidelines.md`
- **Templates**: `openspec/specs/_template/`, `openspec/changes/_template/`
- **Archive Manifest**: `archive/LEGACY_DOCS_MANIFEST.md`
- **This Training Guide**: `openspec/changes/md-milestone-document-organization/team-training-guide.md`

### For Trainers

- **Validity Assessment Report**: `openspec/changes/md-milestone-document-organization/validity-assessment-report.md`
- **Dependency Analysis**: `openspec/changes/md-milestone-document-organization/dependency-analysis-report.md`
- **Team Review Guide**: `openspec/changes/md-milestone-document-organization/team-review-guide.md`
- **Proposal**: `openspec/changes/md-milestone-document-organization/proposal.md`

---

## Troubleshooting Common Issues

### Issue: Team members still creating docs in old locations

**Solution**:
- Gentle reminder about new process
- Point to boundary guidelines
- Offer to help move document to correct location

### Issue: Confusion about when to use spec vs. change proposal

**Solution**:
- Use decision tree from boundary guidelines
- When in doubt, use change proposal
- Ask Tech Lead for guidance

### Issue: Can't find document in archive

**Solution**:
- Check archive manifest for file list
- Use `tar -tzf` to list contents
- Check git history if not in archive

### Issue: Template too complex

**Solution**:
- Focus on required fields first
- Optional sections can be added later
- Provide simplified examples

---

**Training Guide Version**: 1.0
**Created**: 2026-01-15
**Created By**: Claude (OpenSpec Migration Agent)
**Status**: Ready for Training
**Next Review**: After training session for improvements
