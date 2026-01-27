---
name: Project Info Window
overview: Implement a modal project information window that displays project details, expiration date, machine code (masked), and authorization code (masked) from UserSession and LicenseInfo via services.
todos:
  - id: create-viewmodel
    content: Create ProjectInfoWindowViewModel with services integration and data masking logic
    status: completed
  - id: create-window-xaml
    content: Create ProjectInfoWindow.axaml with layout matching screenshot design
    status: completed
  - id: create-window-codebehind
    content: Create ProjectInfoWindow.axaml.cs implementing ITransientDependency
    status: completed
  - id: add-command
    content: Add OpenProjectInfoCommand to AttendedWeighingViewModel
    status: completed
  - id: bind-button
    content: Bind Command to 项目信息 button in AttendedWeighingWindow.axaml
    status: completed
isProject: false
---

# Implement Project Info Window

## Overview

Create a modal dialog to display project information including company name, expiration date, and masked machine/auth codes. Data will be retrieved from `UserSession` and `LicenseInfo` entities via `IAuthenticationService` and `ILicenseService`.

## Architecture

### Data Flow

```
ProjectInfoButton (XAML)
  → OpenProjectInfoCommand (AttendedWeighingViewModel)
    → ProjectInfoWindow (Modal Dialog)
      → ProjectInfoWindowViewModel
        → IAuthenticationService.GetCurrentSessionAsync() → UserSession.CompanyName
        → ILicenseService.GetCurrentLicenseAsync() → LicenseInfo (AuthEndTime, MachineCode, AuthToken)
```

### Files to Create

1. **[MaterialClient/Views/ProjectInfoWindow.axaml](MaterialClient/Views/ProjectInfoWindow.axaml)**

   - Modal window similar to [`AuthCodeWindow.axaml`](d:\CodeUp\MaterialClient\MaterialClient\Views\AuthCodeWindow.axaml)
   - Layout: Grid with 4 rows (项目信息, 到期时间, 机器码, 授权码)
   - Custom blue title bar (#6498FE) with close button
   - Window size: 500x300, `CanResize="False"`, `WindowStartupLocation="CenterOwner"`
   - Red text for expiration date (as shown in screenshot)

2. **[MaterialClient/Views/ProjectInfoWindow.axaml.cs](MaterialClient/Views/ProjectInfoWindow.axaml.cs)**

   - Implements `ITransientDependency`
   - Constructor accepts `ProjectInfoWindowViewModel`
   - Handle close button click

3. **[MaterialClient/ViewModels/ProjectInfoWindowViewModel.cs](MaterialClient/ViewModels/ProjectInfoWindowViewModel.cs)**

   - Inherits `ReactiveViewModelBase`
   - Properties: `ProjectName`, `ExpirationDate`, `MachineCode`, `AuthCode`, `CloseCommand`
   - Constructor injects `IAuthenticationService` and `ILicenseService`
   - `InitializeAsync()` method:
     - Get UserSession → extract CompanyName
     - Get LicenseInfo → extract AuthEndTime, MachineCode, AuthToken
     - Format date as "yyyy-MM-dd"
     - Mask codes: show first 4 + "****" + last 4 characters (e.g., "fae5****59d0")

### Files to Modify

4. **[MaterialClient/ViewModels/AttendedWeighingViewModel.cs](MaterialClient/ViewModels/AttendedWeighingViewModel.cs)**

   - Add `OpenProjectInfoCommand` (ReactiveCommand)
   - Command implementation:
     - Get `ProjectInfoWindowViewModel` from DI
     - Create `ProjectInfoWindow` instance
     - Call `await viewModel.InitializeAsync()`
     - Show modal: `await window.ShowDialog<Unit?>(GetParentWindow())`
   - Follow pattern from `OpenSettingsCommand` (line 48 in AttendedWeighingWindow.axaml)

5. **[MaterialClient/Views/AttendedWeighing/AttendedWeighingWindow.axaml](MaterialClient/Views/AttendedWeighing/AttendedWeighingWindow.axaml)**

   - Line 49-52: Add `Command="{Binding OpenProjectInfoCommand}"` to the "项目信息" button

## Key Implementation Details

### Code Masking Logic

```csharp
private string MaskCode(string code)
{
    if (string.IsNullOrEmpty(code) || code.Length <= 8)
        return code;
    return code.Substring(0, 4) + "****" + code.Substring(code.Length - 4);
}
```

### AuthToken Handling

- `LicenseInfo.AuthToken` is `Guid?` type
- Display as string without hyphens: `authToken?.ToString("N")`
- Then apply masking

### Date Formatting

- `AuthEndTime` is `DateTime`
- Format: `authEndTime.ToString("yyyy-MM-dd")`

### Service Usage Pattern

- ViewModel constructor injects services (NOT repositories)
- Call service methods in `InitializeAsync()`
- Example from [`AuthenticationService.cs`](d:\CodeUp\MaterialClient\MaterialClient.Common\Services\Authentication\AuthenticationService.cs):
  - Line 296-298: `GetCurrentSessionAsync()`
  - From [`LicenseService.cs`](d:\CodeUp\MaterialClient\MaterialClient.Common\Services\Authentication\LicenseService.cs):
  - Line 199-201: `GetCurrentLicenseAsync()`

## Visual Design (from screenshot)

- Title bar: Blue (#6498FE) with "项目信息" title and close (X) button
- Content area: White background, left-aligned labels with right-aligned values
- Layout:
  - 项目信息: [ProjectName in black]
  - 到期时间: [ExpirationDate in RED #DC3545]
  - 机器码: [MaskedMachineCode in gray]
  - 授权码: [MaskedAuthCode in gray]
- Font sizes: Title 14px, labels 14px, values 14px

## Testing Checklist

- [ ] Button click opens modal window
- [ ] Window displays correct company name from UserSession
- [ ] Expiration date formatted correctly and shown in red
- [ ] Machine code masked (first 4 + **** + last 4)
- [ ] Auth code (Guid) masked properly
- [ ] Close button works
- [ ] Window is modal (blocks parent interaction)
- [ ] Handles null/missing data gracefully