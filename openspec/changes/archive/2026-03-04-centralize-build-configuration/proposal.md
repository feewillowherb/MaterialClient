# Change: Centralize Build Configuration and Package Management

**Change ID**: `centralize-build-configuration`
**Status**: Draft
**Created**: 2026-01-23
**Type**: Refactoring

---

## Why

### Background

Currently, package references and build settings are scattered across individual `.csproj` files. The AutoConstructor package reference is only in `MaterialClient.Common.csproj`, but it should be available to all projects. Package versions are duplicated across multiple project files, making version management and updates cumbersome.

### Problems

1. **Inconsistent package availability**: AutoConstructor is only referenced in one project, but other projects may need it
2. **Version management overhead**: Package versions are duplicated across multiple `.csproj` files, requiring manual updates in multiple places
3. **Maintenance burden**: Adding common packages or updating versions requires editing multiple files
4. **Risk of version drift**: Different projects may accidentally use different versions of the same package

---

## What Changes

### Overview

This change centralizes common build configuration and package version management using MSBuild directory-level props files:
- `Directory.Build.props` for common settings and package references (e.g., AutoConstructor)
- `Directory.Packages.props` for centralized package version management using Central Package Management (CPM)

### Detailed Changes

1. **Create `Directory.Build.props`** at solution root:
   - Add AutoConstructor package reference with proper metadata:
     - `PrivateAssets` set to `all`
     - `IncludeAssets` set to `runtime; build; native; contentfiles; analyzers`
   - This makes AutoConstructor available to all projects automatically

2. **Create `Directory.Packages.props`** at solution root:
   - Enable Central Package Management (`ManagePackageVersionsCentrally=true`)
   - Define all package versions in one location
   - Projects reference packages without version numbers (versions come from Directory.Packages.props)

3. **Update all `.csproj` files**:
   - Remove AutoConstructor package reference from `MaterialClient.Common.csproj` (moved to Directory.Build.props)
   - Remove version numbers from PackageReference elements (versions come from Directory.Packages.props)
   - Keep project-specific package references but without versions

---

## Impact

### Expected Benefits

- **Consistency**: All projects automatically get common packages like AutoConstructor
- **Maintainability**: Single source of truth for package versions
- **Efficiency**: Update package versions in one place instead of multiple files
- **Reduced errors**: Eliminates risk of version mismatches across projects
- **Standard practice**: Aligns with modern .NET project management best practices

### Risks and Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Build failures if Directory.Packages.props not recognized | High | Verify .NET SDK version supports CPM (requires .NET SDK 6.0+), test build after changes |
| Projects may have different package needs | Low | Directory.Build.props only adds common packages; project-specific packages remain in .csproj files |
| Migration complexity | Medium | Update one project at a time, test builds incrementally |

### Affected Files

- `Directory.Build.props` (new)
- `Directory.Packages.props` (new)
- `MaterialClient.Common/MaterialClient.Common.csproj`
- `MaterialClient.Common.Tests/MaterialClient.Common.Tests.csproj`
- `MaterialClient/MaterialClient.csproj`
- `MaterialClientToolkit/MaterialClientToolkit.csproj` (if exists)

---

## Success Criteria

- [ ] `Directory.Build.props` created with AutoConstructor reference
- [ ] `Directory.Packages.props` created with all package versions
- [ ] All `.csproj` files updated to remove versions from PackageReference
- [ ] AutoConstructor removed from individual project files
- [ ] Solution builds successfully
- [ ] All projects can use AutoConstructor attributes
- [ ] Package versions are managed centrally

---

## Next Steps

1. Review and approve this proposal
2. Implement changes according to tasks.md
3. Test build and verify AutoConstructor works in all projects
4. Validate package version management

---

## References

- [MSBuild Directory.Build.props documentation](https://learn.microsoft.com/en-us/visualstudio/msbuild/customize-your-build)
- [Central Package Management (CPM) documentation](https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management)
