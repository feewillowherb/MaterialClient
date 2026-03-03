# Tasks: View Files Categorization

This task list breaks down the implementation work for organizing control files into a separate `Views/Controls` folder with proper namespace isolation.

---

## 1. Preparation and Analysis

- [x] 1.1 Create feature branch for the migration
  - Create branch named `feature/view-files-categorization`
  - Switch to the new branch

- [x] 1.2 Scan Views folder for all XAML files
  - List all .axaml files in Views/ directory
  - Record total count of files

- [x] 1.3 Identify control files by root element type
  - Parse each XAML file to check root element
  - Identify files with `<UserControl>` or `<Control>` root elements
  - Generate list of control files to move

- [x] 1.4 Identify view files that reference controls
  - Search for all XAML files that use identified controls
  - Record which controls are used in which view files
  - Generate list of files needing reference updates

- [x] 1.5 Verify no namespace conflicts
  - Check for existing `Views/Controls/` folder
  - Check for existing `MaterialClient.Views.Controls` namespace
  - Confirm safe to proceed with migration

- [x] 1.6 Search for hardcoded file path references
  - Search codebase for any scripts or tools referencing specific Views paths
  - Identify any build scripts, documentation, or tooling with hardcoded paths
  - Record files needing path updates beyond XAML/.csproj

- [x] 1.7 Check resource dictionaries and App.axaml
  - Examine App.axaml for control registrations or references
  - Check any resource dictionary files for control references
  - Record any resource files needing updates

---

## 2. File Structure Implementation

- [x] 2.1 Create Views/Controls folder
  - Create `Views/Controls/` directory structure
  - Verify folder creation succeeded

- [x] 2.2 Move all identified control files to Views/Controls
  - Move each control .axaml file from Views/ to Views/Controls/
  - Move each control .axaml.cs file from Views/ to Views/Controls/
  - Verify all control files are in new location

---

## 3. Namespace Updates in Control Files

- [x] 3.1 Update x:Class attributes in control XAML files
  - For each control .axaml file in Views/Controls/
  - Update `x:Class="MaterialClient.Views.<ClassName>"` to `x:Class="MaterialClient.Views.Controls.<ClassName>"`
  - Verify all XAML files have correct x:Class

- [x] 3.2 Update namespace declarations in control code-behind files
  - For each control .axaml.cs file in Views/Controls/
  - Update `namespace MaterialClient.Views` to `namespace MaterialClient.Views.Controls`
  - Verify all code-behind files have correct namespace

---

## 4. Reference Updates in View Files

- [x] 4.1 Add xmlns:Controls declaration to view XAML files
  - For each view file that uses controls
  - Add `xmlns:Controls="using:MaterialClient.Views.Controls"` to root element
  - Verify all using view files have the declaration

- [x] 4.2 Update control references to use Controls prefix
  - For each control reference in view files
  - Replace `<ControlName>` with `<Controls:ControlName>`
  - Replace `</ControlName>` with `</Controls:ControlName>`
  - Verify all references use the Controls prefix

---

## 5. Project File and Resource Updates

- [x] 5.1 Update project file (.csproj) file paths
  - For each moved control file
  - Update file path from `Views\<filename>.axaml` to `Views/Controls/<filename>.axaml`
  - Verify all control file paths are correct

- [x] 5.2 Update resource dictionary references if needed
  - Update App.axaml if it references moved controls
  - Update any resource dictionaries if they reference moved controls
  - Verify all resource references are correct

- [x] 5.3 Update any hardcoded path references found in analysis
  - Update build scripts if needed
  - Update documentation if needed
  - Update any tooling configuration if needed

---

## 6. Build Verification

- [x] 6.1 Clean build directory
  - Clean the project to remove any cached artifacts

- [x] 6.2 Build project in Debug configuration
  - Build project using Debug configuration
  - Resolve any build errors or warnings
  - Verify build succeeds with zero errors

- [x] 6.3 Build project in Release configuration
  - Build project using Release configuration
  - Resolve any build errors or warnings
  - Verify build succeeds with zero errors

- [x] 6.4 Check for compiler warnings
  - Review all compiler warnings
  - Address any warnings related to namespace or file changes
  - Verify no critical warnings remain

---

## 7. Runtime Verification

- [x] 7.1 Run application from Debug build
  - Launch application from Debug configuration
  - Verify application starts without errors
  - Check for any namespace-related runtime errors

- [x] 7.2 Run application from Release build
  - Launch application from Release configuration
  - Verify application starts without errors
  - Check for any namespace-related runtime errors

- [x] 7.3 Manually test all views using controls
  - Navigate to each view that uses moved controls
  - Verify controls render correctly
  - Verify controls function as expected
  - Document any visual or functional differences

- [x] 7.4 Verify control styling is preserved
  - Check that all control styles are applied correctly
  - Verify custom styling is not broken by namespace changes
  - Verify control templates work as expected

---

## 8. Code Review Preparation

- [x] 8.1 Generate change summary
  - Create list of all files moved
  - Create list of all files modified
  - Calculate total lines changed

- [x] 8.2 Create migration documentation
  - Document the new file organization pattern
  - Create guidelines for adding new controls to Views/Controls
  - Document namespace conventions for controls

- [x] 8.3 Review changes for completeness
  - Verify all control files were moved
  - Verify all references were updated
  - Verify no files were missed

---

## 9. Final Steps

- [ ] 9.1 Commit all migration changes
  - Stage all changed files
  - Create comprehensive commit message describing the migration
  - Commit changes to feature branch

- [ ] 9.2 Create pull request
  - Create pull request from feature branch to main
  - Include migration documentation in PR description
  - Reference this change in the PR

- [ ] 9.3 Notify team of new file organization
  - Communicate the new Views/Controls folder structure to team
  - Share migration documentation
  - Provide guidelines for adding new controls

---

## Task Dependencies

```
1. Preparation and Analysis
    ↓
2. File Structure Implementation
    ↓
3. Namespace Updates in Control Files
    ↓
4. Reference Updates in View Files
    ↓
5. Project File and Resource Updates
    ↓
6. Build Verification
    ↓
7. Runtime Verification
    ↓
8. Code Review Preparation
    ↓
9. Final Steps
```

**Note**: Tasks must be completed in the order shown due to dependencies. Earlier tasks provide the foundation for later tasks.

---

## Success Criteria

All tasks are complete when:
- [ ] All control files are in Views/Controls/ folder
- [ ] All namespaces are correctly updated
- [ ] All references use the Controls prefix
- [ ] Project builds without errors or warnings
- [ ] Application runs successfully
- [ ] All controls render and function correctly
- [ ] Team is notified of new file organization

---

## Estimated Effort

- **Phase 1 (Preparation and Analysis)**: 1-2 hours
- **Phase 2-5 (Implementation)**: 3-5 hours (depending on number of files)
- **Phase 6-7 (Verification)**: 1-2 hours
- **Phase 8-9 (Final Steps)**: 1 hour

**Total Estimated Time**: 6-10 hours
