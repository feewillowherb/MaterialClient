## 1. Event Infrastructure

- [x] 1.1 Create `SessionRefreshRequiredEto` class in `MaterialClient.Common/Events/`
  - Add properties: `ApiEndpoint` (string), `StatusCode` (int), `OccurredAtUtc` (DateTime)
  - Use class + primary constructor format per AGENTS.md

## 2. Bearer Token Handler Enhancement

- [x] 2.1 Modify `MaterialPlatformBearerTokenHandler.SendAsync` to detect 401 responses
  - Inject `ILocalEventBus` in constructor
  - After calling `base.SendAsync`, check if response status code is 401
  - Publish `SessionRefreshRequiredEto` via `_localEventBus.PublishAsync()`
  - Ensure 401 response still propagates to caller (no exception swallowing)

## 3. Polling Service Event Subscription

- [x] 3.1 Locate `PollingBackgroundService` implementation
  - Find where background workers are registered in `MaterialClientModule`

- [x] 3.2 Add `SessionRefreshRequiredEto` subscription to polling service
  - Override `StartAsync` method
  - Subscribe to event using `_localEventBus.Subscribe<SessionRefreshRequiredEto>()`
  - Manage subscription lifecycle (store handler for cleanup if needed)

- [x] 3.3 Implement re-login logic in event handler
  - Get saved credentials using `AuthenticationService.GetSavedCredentialAsync()`
  - Call `AuthenticationService.LoginAsync()` with saved credentials
  - Log success/failure with appropriate details

## 4. Testing & Validation

- [x] 4.1 Test 401 detection and event publishing
  - Mock API response to return 401
  - Verify `SessionRefreshRequiredEto` is published with correct properties

- [x] 4.2 Test event subscription and re-login
  - Verify polling service receives event
  - Confirm `AuthenticationService.LoginAsync` is called with saved credentials

- [x] 4.3 Test end-to-end scenario
  - Simulate token expiration
  - Verify current request fails as expected (no blocking)
  - Confirm next polling cycle succeeds with new token

- [x] 4.4 Test error scenarios
  - Test when no saved credentials exist
  - Test when re-login fails (invalid credentials, network error)
  - Verify appropriate logging occurs

## 5. Documentation

- [x] 5.1 Update AGENTS.md if new patterns are established
  - Document the 401 handling pattern for future reference
