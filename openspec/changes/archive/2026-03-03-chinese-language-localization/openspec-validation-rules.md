# OpenSpec Validation Rules

This document defines validation rules to ensure OpenSpec specification documents remain in English during the Chinese language localization process.

## OpenSpec English Language Requirement

**CRITICAL REQUIREMENT:** All OpenSpec specification documents MUST remain in English. This is a non-negotiable requirement of the OpenSpec system.

## Files That MUST Remain in English

### Core OpenSpec Files

```
openspec/specs/**/*.md           # All specification documents
openspec/changes/**/proposal.md   # Change proposals
openspec/changes/**/tasks.md      # Task lists
openspec/changes/**/design.md     # Design documents
openspec/changes/**/spec.md       # Change specifications
```

### System Configuration Files

```
openspec/AGENTS.md               # Agent configuration
openspec/README.md               # System documentation
CLAUDE.md                        # System instructions
```

## Validation Rules

### Rule 1: No Chinese Characters in OpenSpec Files

**Rule:** OpenSpec specification files (`.md` files in `openspec/` directory) must NOT contain Chinese characters.

**Validation Check:**
```bash
# Check for Chinese characters in OpenSpec files
find openspec/ -name "*.md" -exec grep -l "[\u4e00-\u9fff]" {} \;
```

**Expected Result:** No files should be returned.

### Rule 2: File Path Validation

**Rule:** Ensure no OpenSpec files are accidentally translated or moved.

**Valid Paths:**
```
✓ openspec/specs/**/spec.md
✓ openspec/changes/{change-name}/proposal.md
✓ openspec/changes/{change-name}/tasks.md
✓ openspec/changes/{change-name}/design.md
```

**Invalid Actions:**
```
✗ Moving OpenSpec files out of openspec/ directory
✗ Creating Chinese versions (e.g., spec-zh.md)
✗ Modifying OpenSpec file structure
✗ Renaming OpenSpec files to indicate language
```

### Rule 3: Content Validation

**Rule:** OpenSpec document content must remain in English only.

**Valid Content Examples:**
```
✓ This is the specification for the weighing module
✓ The API provides CRUD operations for records
✓ Implementation should follow the MVVM pattern
```

**Invalid Content Examples:**
```
✗ 这是称重模块的规范
✗ API 提供记录的 CRUD 操作
✗ 实现应该遵循 MVVM 模式
```

## Validation Process

### Pre-Commit Validation

Before committing any changes, run the following validation:

```bash
#!/bin/bash

echo "Validating OpenSpec files for Chinese content..."

# Check for Chinese characters in OpenSpec files
CHINESE_FILES=$(find openspec/ -name "*.md" -exec grep -l "[\u4e00-\u9fff]" {} \;)

if [ -n "$CHINESE_FILES" ]; then
    echo "ERROR: Found Chinese characters in OpenSpec files:"
    echo "$CHINESE_FILES"
    exit 1
fi

echo "✓ All OpenSpec files are in English"
exit 0
```

### Automated Validation Script

Create a validation script in the project root:

```bash
#!/bin/bash
# validate-openspec-english.sh

# Set error exit
set -e

echo "=== OpenSpec English Language Validation ==="
echo ""

# Define OpenSpec file patterns
OPENSPEC_PATTERNS=(
    "openspec/specs/**/*.md"
    "openspec/changes/**/proposal.md"
    "openspec/changes/**/tasks.md"
    "openspec/changes/**/design.md"
    "openspec/changes/**/spec.md"
)

# Check each pattern
for pattern in "${OPENSPEC_PATTERNS[@]}"; do
    echo "Checking: $pattern"

    # Find files matching the pattern
    FILES=$(find openspec/ -name "*.md" | grep -E "(spec|proposal|tasks|design)\.md$" || true)

    for file in $FILES; do
        # Check for Chinese characters
        if grep -q "[\u4e00-\u9fff]" "$file"; then
            echo "  ✗ ERROR: Found Chinese characters in $file"
            echo "    Showing lines with Chinese content:"
            grep -n "[\u4e00-\u9fff]" "$file" | head -5
            exit 1
        else
            echo "  ✓ OK: $file"
        fi
    done
done

echo ""
echo "=== All OpenSpec files validated successfully ==="
exit 0
```

## Integration with Git Hooks

### Pre-Commit Hook

Create a `.git/hooks/pre-commit` file:

```bash
#!/bin/bash

# Validate OpenSpec files before commit
echo "Validating OpenSpec language requirement..."

# Run the validation script
bash ./validate-openspec-english.sh

# If validation fails, prevent the commit
if [ $? -ne 0 ]; then
    echo ""
    echo "COMMIT ABORTED: OpenSpec files must remain in English"
    echo "Please fix the validation errors before committing."
    exit 1
fi

exit 0
```

### Pre-Push Hook (Additional Safety)

Create a `.git/hooks/pre-push` file:

```bash
#!/bin/bash

# Final validation before push
echo "Running final OpenSpec validation before push..."

bash ./validate-openspec-english.sh

if [ $? -ne 0 ]; then
    echo ""
    echo "PUSH ABORTED: OpenSpec files must remain in English"
    exit 1
fi

exit 0
```

## Manual Validation Checklist

Before considering any translation complete, verify:

- [ ] No Chinese characters in `openspec/specs/**/*.md`
- [ ] No Chinese characters in `openspec/changes/**/proposal.md`
- [ ] No Chinese characters in `openspec/changes/**/tasks.md`
- [ ] No Chinese characters in `openspec/changes/**/design.md`
- [ ] No Chinese characters in `openspec/changes/**/spec.md`
- [ ] No OpenSpec files moved outside `openspec/` directory
- [ ] No Chinese versions of OpenSpec files created
- [ ] `CLAUDE.md` remains in English
- [ ] All git hooks are in place and executable

## CI/CD Integration

### GitHub Actions Workflow

Create `.github/workflows/openspec-validation.yml`:

```yaml
name: OpenSpec Language Validation

on:
  pull_request:
    paths:
      - 'openspec/**'
      - 'CLAUDE.md'

jobs:
  validate-openspec-english:
    runs-on: ubuntu-latest
    steps:
      - name: Checkout code
        uses: actions/checkout@v3

      - name: Validate OpenSpec files
        run: |
          echo "Validating OpenSpec language requirement..."
          find openspec/ -name "*.md" -exec sh -c 'grep -q "[\u4e00-\u9fff]" "$1" && echo "ERROR: Chinese found in $1" && exit 1' _ {} \;
          echo "✓ All OpenSpec files are in English"
```

### Azure Pipelines

Create `azure-pipelines-openspec-validation.yml`:

```yaml
trigger:
  branches:
    include:
      - main
      - develop
  paths:
    include:
      - openspec/**
      - CLAUDE.md

pool:
  vmImage: 'ubuntu-latest'

steps:
- script: |
    echo "Validating OpenSpec language requirement..."
    find openspec/ -name "*.md" | xargs grep -l "[\u4e00-\u9fff]" && exit 1 || echo "✓ All OpenSpec files are in English"
  displayName: 'Validate OpenSpec English Language'
```

## Documentation and Training

### Developer Guidelines

All developers working on the project must:

1. **Understand the Requirement:** OpenSpec files must remain in English
2. **Run Validation:** Always run validation before committing changes
3. **Report Issues:** Immediately report any validation failures
4. **Follow Process:** Use the established translation workflow
5. **Stay Updated:** Keep validation scripts and hooks up to date

### Onboarding Checklist

New team members should:

- [ ] Read this validation rules document
- [ ] Understand the OpenSpec English language requirement
- [ ] Set up local git hooks
- [ ] Run validation script successfully
- [ ] Complete a test commit without errors

## Troubleshooting

### Common Issues

**Issue:** Validation script reports false positives

**Solution:** Update the character range or add exceptions for specific patterns

**Issue:** Git hooks not executing

**Solution:** Ensure hooks are executable (`chmod +x .git/hooks/*`)

**Issue:** CI/CD validation failing locally but not in pipeline

**Solution:** Check for environment differences (OS, encoding, etc.)

### Emergency Procedures

If validation fails and needs urgent resolution:

1. Identify the problematic file(s)
2. Review the content that triggered the failure
3. Either:
   - Move Chinese content to the appropriate non-OpenSpec file
   - Revert the problematic changes
4. Re-run validation
5. Commit only after validation passes

## Conclusion

These validation rules ensure that:
- OpenSpec specification documents remain in English as required
- The OpenSpec system continues to function correctly
- Translation work does not compromise system integrity
- All team members follow consistent practices

Regular review and updates of these validation rules will maintain the effectiveness of the OpenSpec system while supporting the Chinese language localization goals of the MaterialClient project.
