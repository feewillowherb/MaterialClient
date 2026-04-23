## 1. Event Message

- [x] 1.1 Create `LogoutRequestedMessage` marker class in `MaterialClient.Common/Events/LogoutRequestedMessage.cs` following the existing pattern (e.g., `DetailCloseRequestedMessage`)

## 2. AttendedWeighingViewModel Logout Logic

- [x] 2.1 Add `LogoutCommand` property (`IReactiveCommand`) to `AttendedWeighingViewModel` using `[ReactiveCommand]` attribute with an async handler
- [x] 2.2 Inject `IAuthenticationService` into `AttendedWeighingViewModel` (if not already injected)
- [x] 2.3 Implement the logout handler: show confirmation dialog, call `LogoutAsync()`, call `ClearSavedCredentialAsync()`, publish `LogoutRequestedMessage` via `MessageBus.Current.SendMessage()`, hide `AttendedWeighingWindow`, resolve `LoginWindow` from DI, reset and show it

## 3. AttendedWeighingWindow UI

- [x] 3.1 Add a "退出登录" button to `AttendedWeighingWindow.axaml` in the top menu bar, positioned after the "数据同步" button, using the same `transparent-button` class and `Foreground="White"` style, bind `Command="{Binding LogoutCommand}"`

## 4. LoginWindowViewModel Reset

- [x] 4.1 Add a public method or property to `LoginWindowViewModel` to reset the login form (clear username, password, and `IsLoginSuccessful` state) for the re-login scenario
- [x] 4.2 Call the reset method from the `AttendedWeighingViewModel` logout handler before showing `LoginWindow`

## 5. Verification

- [ ] 5.1 Manual test: click "退出登录" in top menu bar, confirm dialog appears, cancel — verify no logout occurs
- [ ] 5.2 Manual test: click "退出登录" in top menu bar, confirm — verify session is cleared, LoginWindow appears with empty form
- [ ] 5.3 Manual test: after logout and re-login, verify the app functions normally in `AttendedWeighingWindow`
