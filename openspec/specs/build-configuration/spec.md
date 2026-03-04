# build-configuration Specification

## Purpose
TBD - created by archiving change centralize-build-configuration. Update Purpose after archive.
## Requirements
### Requirement: Centralized Build Configuration

The system SHALL use `Directory.Build.props` at the solution root to define common build settings and package references that apply to all projects.

#### Scenario: AutoConstructor available to all projects
- **WHEN** a project in the solution uses the `[AutoConstructor]` attribute
- **THEN** the AutoConstructor source generator SHALL be available without explicit package reference in the project's `.csproj` file
- **AND** the package reference SHALL be defined in `Directory.Build.props` with:
  - `PrivateAssets` set to `all`
  - `IncludeAssets` set to `runtime; build; native; contentfiles; analyzers`

#### Scenario: Common packages automatically available
- **WHEN** a package reference is added to `Directory.Build.props`
- **THEN** all projects in the solution SHALL automatically have access to that package
- **AND** projects SHALL NOT need to explicitly reference the package in their `.csproj` files

### Requirement: Centralized Package Version Management

The system SHALL use `Directory.Packages.props` with Central Package Management (CPM) to manage all package versions in a single location.

#### Scenario: Package versions defined centrally
- **WHEN** a package version is updated in `Directory.Packages.props`
- **THEN** all projects using that package SHALL automatically use the updated version
- **AND** projects SHALL reference packages without version numbers in their `.csproj` files

#### Scenario: Version consistency across projects
- **WHEN** multiple projects reference the same package
- **THEN** all projects SHALL use the same version as defined in `Directory.Packages.props`
- **AND** version conflicts SHALL be prevented by the build system

#### Scenario: Package reference without version
- **WHEN** a project adds a PackageReference in its `.csproj` file
- **THEN** the PackageReference SHALL NOT include a Version attribute
- **AND** the version SHALL be resolved from `Directory.Packages.props`
- **UNLESS** the package is project-specific and not defined in `Directory.Packages.props`

### Requirement: Build Configuration File Structure

The system SHALL maintain build configuration files at the solution root level.

#### Scenario: Directory.Build.props at solution root
- **WHEN** the solution is built
- **THEN** `Directory.Build.props` SHALL exist at the solution root directory
- **AND** it SHALL be automatically imported by all projects in the solution

#### Scenario: Directory.Packages.props at solution root
- **WHEN** the solution is built
- **THEN** `Directory.Packages.props` SHALL exist at the solution root directory
- **AND** it SHALL contain `<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>`
- **AND** it SHALL contain all package version definitions

#### Scenario: Projects reference packages without versions
- **WHEN** a `.csproj` file contains a PackageReference
- **THEN** the PackageReference SHALL NOT include a Version attribute
- **AND** the version SHALL be resolved from `Directory.Packages.props`
- **UNLESS** the package is not defined in `Directory.Packages.props` (project-specific packages)

