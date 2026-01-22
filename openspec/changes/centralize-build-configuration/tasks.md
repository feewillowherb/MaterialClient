## 1. Preparation

- [x] 1.1 Verify .NET SDK version supports Central Package Management (requires SDK 6.0+)
- [x] 1.2 List all .csproj files in the solution to identify affected projects
- [x] 1.3 Extract all unique package references and their versions from all .csproj files

## 2. Create Directory.Build.props

- [x] 2.1 Create `Directory.Build.props` at solution root
- [x] 2.2 Add AutoConstructor package reference with metadata:
  - `PrivateAssets` = `all`
  - `IncludeAssets` = `runtime; build; native; contentfiles; analyzers`
- [x] 2.3 Verify the file structure is correct

## 3. Create Directory.Packages.props

- [x] 3.1 Create `Directory.Packages.props` at solution root
- [x] 3.2 Enable Central Package Management: `<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>`
- [x] 3.3 Add `<ItemGroup>` with `<PackageVersion>` entries for all packages found in step 1.3
- [x] 3.4 Ensure all package versions match current versions in .csproj files

## 4. Update MaterialClient.Common.csproj

- [x] 4.1 Remove AutoConstructor PackageReference (now in Directory.Build.props)
- [x] 4.2 Remove version attributes from all PackageReference elements
- [x] 4.3 Verify package references still work correctly

## 5. Update MaterialClient.Common.Tests.csproj

- [x] 5.1 Remove version attributes from all PackageReference elements
- [x] 5.2 Verify all packages are defined in Directory.Packages.props
- [x] 5.3 Verify package references still work correctly

## 6. Update MaterialClient.csproj

- [x] 6.1 Remove version attributes from all PackageReference elements
- [x] 6.2 Verify all packages are defined in Directory.Packages.props
- [x] 6.3 Verify package references still work correctly

## 7. Update Other Projects (if any)

- [x] 7.1 Check for MaterialClientToolkit.csproj or other projects
- [x] 7.2 Remove version attributes from PackageReference elements in any additional projects
- [x] 7.3 Verify all packages are defined in Directory.Packages.props

## 8. Validation

- [x] 8.1 Run `dotnet restore` to verify package resolution
- [x] 8.2 Run `dotnet build` to verify compilation succeeds
- [x] 8.3 Verify AutoConstructor source generator works in all projects (check for generated constructors)
- [ ] 8.4 Run existing tests to ensure no regressions
- [x] 8.5 Verify no duplicate package references or version conflicts

## 9. Documentation

- [x] 9.1 Update project.md if needed to document the new build configuration approach
- [x] 9.2 Add comments in Directory.Build.props explaining its purpose
- [x] 9.3 Add comments in Directory.Packages.props explaining CPM usage
