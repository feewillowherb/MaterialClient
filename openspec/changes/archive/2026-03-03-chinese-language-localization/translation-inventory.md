# Translation Inventory

This document provides a comprehensive inventory of all files requiring translation for the Chinese Language Localization change.

## Files Requiring Translation

### Markdown Documentation Files (12 files)

#### Documentation in `docs/` directory (10 files)
- `docs/SDD.md` - Software Design Document
- `docs/existing-docs-inventory.md` - Documentation inventory
- `docs/design-creatable-pageable-searchable-selection.md` - Design document
- `docs/evaluation-generic-selection-popup-merge-search-and-trigger.md` - Evaluation document
- `docs/evaluation-photo-grid-image-loading-performance.md` - Evaluation document
- `docs/evaluation-remove-defaultweighingmode-bootstrap.md` - Evaluation document
- `docs/popup-selection-analysis.md` - Analysis document
- `docs/proposal-creatable-pageable-searchable-selection.md` - Proposal document
- `docs/sdd-creation-summary.md` - Creation summary
- `docs/sdd-gap-analysis.md` - Gap analysis
- `docs/sdd-maintenance-guide.md` - Maintenance guide
- `docs/sdd-quality-assessment.md` - Quality assessment

#### Root-level Markdown files (1 file)
- `HikLpr_OpenSpec_Proposal.md` - OpenSpec proposal document
- Note: `CLAUDE.md` should remain in English as it contains system instructions

### C# Source Files (256 files)

#### MaterialClient project (160 files)
- Located in `MaterialClient/` directory
- Requires comment translation to Chinese

#### MaterialClient.Common project (72 files)
- Located in `MaterialClient.Common/` directory
- Requires comment translation to Chinese

#### MaterialClient.Toolkit project (24 files)
- Located in `MaterialClient.Toolkit/` directory
- Requires comment translation to Chinese

### Project Files (3 files)

#### .csproj files requiring metadata updates
- `MaterialClient/MaterialClient.csproj` - Update description to Chinese
- `MaterialClient.Common/MaterialClient.Common.csproj` - Update description to Chinese
- `MaterialClient.Toolkit/MaterialClient.Toolkit.csproj` - Update description to Chinese

## Excluded Files

The following files are **EXCLUDED** from translation:

### OpenSpec System Files (MUST remain in English)
- `openspec/specs/**/*.md` - All specification documents
- `openspec/changes/**/*.md` - All change documents (proposal.md, tasks.md, design.md)
- `CLAUDE.md` - System instructions file

### Third-Party Files
- `.cursor/` directory - IDE-specific configurations
- `.specify/` directory - AI assistant templates
- `archive/` directory - Archived legacy documentation

### Build and Configuration Files
- All `.json`, `.xml`, `.config`, `.editorconfig`, `.gitignore` files
- All `.axaml` and `.axaml.cs` UI files (not in scope for this change)

## Translation Statistics

- **Total Files Requiring Translation:** 271 files
  - Markdown files: 12 files
  - C# files: 256 files
  - Project files: 3 files
- **Total Lines of Code**: Estimated 15,000+ lines of comments to translate
- **Estimated Effort**: 20-30 hours of manual translation work

## Priority Classification

### High Priority (Critical)
- `docs/SDD.md` - Primary design document
- `docs/existing-docs-inventory.md` - Documentation reference
- Project metadata files (.csproj)

### Medium Priority (Important)
- All documentation files in `docs/`
- Root-level Markdown files
- Code comments in MaterialClient and MaterialClient.Common projects

### Low Priority (Optional)
- Code comments in MaterialClient.Toolkit project
- Evaluation and analysis documents

## Notes

1. Code content itself (variable names, method names, class names) must remain in English
2. Technical and professional terminology should retain English notes (e.g., API, HTTP, REST, JSON, XML)
3. All OpenSpec specification documents must remain in English per system requirements
4. Translation should maintain original meaning and technical accuracy
5. Code comments should provide clear guidance to Chinese-speaking developers
