# MaterialClient.Common.Tests - Integration Test Analysis Report

**Date:** 2026-01-22  
**Project:** MaterialClient.Common.Tests  
**Target Framework:** .NET 10.0  
**Test Runner:** xUnit 2.9.2 with Reqnroll 3.2.1 (BDD)

---

## Executive Summary

The test project contains **critical dependency injection issues** and **external service dependencies** that prevent a significant portion of integration tests from executing successfully. Out of 37 test files analyzed:

- ✅ **26 tests passing** (mostly unit tests)
- ❌ **2 tests failing** (external service dependencies)
- ⏸️ **31 tests skipped** (hardware dependencies)
- ⚠️ **BDD scenarios failing** (DI configuration issues)
- ⏱️ **1 test timeout** (resource cleanup)

---

## Critical Issues

### 1. Dependency Injection Failures (HIGH PRIORITY)

**Affected Components:**
- All Reqnroll BDD scenarios (Authentication.feature, Authorization.feature, WeighingService.feature, WeighingMatchingService.feature)
- `AuthenticationSteps` class
- Any test depending on `IMaterialPlatformApi`

**Root Cause:**
```
Autofac.Core.DependencyResolutionException: 
Cannot resolve parameter 'MaterialClient.Common.Api.IMaterialPlatformApi materialPlatformApi' 
of constructor 'Void .ctor(...)'
```

**Error Location:**
```
MaterialClient.Common.Tests\Steps\AuthenticationSteps.cs:line 39
at MaterialClient.Common.Tests.Steps.AuthenticationSteps..ctor()
```

**Impact:**
- 🔴 **100% of BDD scenarios fail** before any test logic executes
- Authentication flow tests cannot run
- Session management tests blocked
- Authorization verification tests blocked

**Analysis:**
The test module (`MaterialClientTestBaseModule`) does not register mock implementations for API dependencies. The `AuthenticationSteps` constructor attempts to inject `IMaterialPlatformApi` but no registration exists in the test DI container.

**Code Evidence:**
```csharp:37:46:d:\CodeUp\MaterialClient\MaterialClient.Common.Tests\Steps\AuthenticationSteps.cs
public AuthenticationSteps()
{
    _authService = GetRequiredService<IAuthenticationService>();
    _licenseService = GetRequiredService<ILicenseService>();
    _testService = GetRequiredService<ITestService>();
    _sessionRepository = GetRequiredService<IRepository<UserSession, Guid>>();
    _credentialRepository = GetRequiredService<IRepository<UserCredential, Guid>>();

    // Get the mock API that was registered in the test module
    _mockApi = GetRequiredService<IMaterialPlatformApi>(); // ❌ FAILS HERE
}
```

---

### 2. External Service Dependencies (HIGH PRIORITY)

#### 2.1 Sound Device API Test Failure

**Test:** `SoundDeviceApiTests.PlayAudioAsync_Should_PlayAudioSuccessfully`

**Error:**
```
System.Text.Json.JsonReaderException: 'u' is an invalid start of a value. 
LineNumber: 0 | BytePositionInLine: 0.
```

**Root Cause:**
- Test expects a sound device HTTP service at `http://localhost:8888`
- Service returns `"undefined"` instead of valid JSON
- Likely the service is not running or misconfigured

**Service Endpoint Expected:**
```csharp:22:28:d:\CodeUp\MaterialClient\MaterialClient.Common.Tests\Tests\SoundDeviceApiTests.cs
var soundIP = "localhost";
var playBaseUrl = $"http://{soundIP}:8888";
var playHttpClient = new HttpClient
{
    BaseAddress = new Uri(playBaseUrl),
    Timeout = TimeSpan.FromSeconds(30)
};
```

**Impact:**
- Sound device integration tests cannot run in CI/CD
- Manual verification required for sound features
- Integration test suite is not self-contained

#### 2.2 TTS Service Dependency

The test also depends on a TTS (Text-to-Speech) service at `http://localhost:10008`:

```csharp:34:34:d:\CodeUp\MaterialClient\MaterialClient.Common.Tests\Tests\SoundDeviceApiTests.cs
var ttsUri = $"http://{localIP}:10008/tts_xf.single?text={Uri.EscapeDataString(testText)}&voice_name=xiaoyan&speed=50&volume={volume}&origin=http://{localIP}:10008";
```

---

### 3. Test Timeout Issues (MEDIUM PRIORITY)

**Test:** `TruckScaleWeightServiceTests.DisposeAsync_Should_CleanupResources`

**Error:**
```
Test execution timed out after 5000 milliseconds
```

**Possible Causes:**
1. Resource cleanup deadlock
2. Unresponsive disposal logic
3. Background threads not terminating
4. Serial port/COM port hanging

**Test Details:**
- Expected timeout: 5000ms (5 seconds)
- Test purpose: Verify proper cleanup of TruckScaleWeightService resources
- Likely involves serial port communication cleanup

---

## Skipped Tests Analysis

### Hardware-Dependent Tests (31 tests skipped)

#### Hikvision Camera Tests (18 skipped)
**Files:** `HikvisionIntegrationTests.cs`, `HikvisionServiceTests.cs`

**Skip Reasons:**
- `"Requires physical Hikvision device"` (16 tests)
- `"Requires physical Hikvision device - long running test"` (2 tests)

**Test Categories:**
- Camera connectivity (`RealCamera_IsOnline_ShouldReturnTrue`)
- JPEG capture (direct & stream-based)
- Concurrent capture stress tests
- Port leak detection
- Batch capture operations
- Resource cleanup verification

**Business Impact:**
These tests verify the critical camera capture functionality used for weighing operations. They cannot run in CI/CD but should be run manually before releases.

#### Ticket Printing Tests (13 skipped)
**File:** `TicketPrintingServiceTests.cs`

**Skip Reason:** `"manual-only"`

**Test Categories:**
- Printer connectivity
- Template rendering
- QR code generation
- Barcode printing
- Multi-page ticket printing
- Error handling

---

## Passing Tests (26 tests)

✅ **Configuration Tests** (5 tests)
- Scenario-based configuration tests
- Direct configuration unit tests
- Configuration validation

✅ **Password Encryption Tests** (10 tests)
- Encryption/decryption functionality
- Special character handling
- Empty string validation
- Invalid input handling
- Key configuration validation

✅ **Extension Tests** (6 tests)
- `ReaderWriterLockSlimExtensions` tests
- Locking mechanism verification
- Performance tests

✅ **Utility Tests** (2 tests)
- `MaterialMath.DetermineOffsetResult` tests
- Weight deviation calculations

✅ **Truck Scale Tests** (1 test)
- `SetWeight_Should_UpdateWeight_And_TriggerObservable`

✅ **Weighing Matching Tests** (2 tests)
- `CopySolidWasteInfoToWaybill` scenarios

---

## Test Infrastructure Analysis

### Test Base Classes

```
MaterialClientTestBase<TStartupModule>
    ↓
MaterialClientEntityFrameworkCoreTestBase
    ↓
Specific Test Classes / BDD Steps
```

**Configuration Strategy:**
- ✅ Uses in-memory configuration (good practice)
- ✅ SQLite in-memory database (`:memory:`)
- ✅ No file-based configuration dependencies
- ❌ Missing mock registrations for external APIs

**Current Configuration:**
```csharp:24:31:d:\CodeUp\MaterialClient\MaterialClient.Common.Tests\MaterialClientTestBase.cs
var inMemorySettings = new Dictionary<string, string>
{
    // Default test configuration
    ["ConnectionStrings:Default"] = "Data Source=:memory:",
    ["BasePlatform:BaseUrl"] = "http://localhost:5000",
    ["BasePlatform:ProductCode"] = "5000",
    ["Encryption:AesKey"] = "MTIzNDU2Nzg5MDEyMzQ1Njc4OTAxMjM0NTY3ODkwMTI="
};
```

### Test Module Structure

**MaterialClientTestBaseModule:**
- ✅ Autofac integration
- ✅ Background jobs disabled (good for tests)
- ✅ Authorization always allowed
- ❌ No API mock registrations

**Missing Registrations:**
```csharp
// Required but not registered:
- IMaterialPlatformApi (mock)
- ISoundDeviceApi (mock or stub)
- IBasePlatformApi (mock)
```

---

## Recommendations

### Immediate Actions (HIGH PRIORITY)

#### 1. Fix Dependency Injection Issues

**Action:** Add mock API registrations to test module

**Implementation:**
```csharp
// In MaterialClientTestBaseModule or a new MaterialClientEntityFrameworkCoreTestModule
public override void ConfigureServices(ServiceConfigurationContext context)
{
    // Existing configuration...
    
    // Register mock APIs for testing
    context.Services.AddSingleton<IMaterialPlatformApi>(sp => 
    {
        var mock = Substitute.For<IMaterialPlatformApi>();
        // Setup default mock responses
        return mock;
    });
    
    context.Services.AddSingleton<ISoundDeviceApi>(sp => 
    {
        var mock = Substitute.For<ISoundDeviceApi>();
        mock.PlayAudioAsync(Arg.Any<SoundDevicePlayRequestDto>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("{\"success\": true}"));
        return mock;
    });
    
    context.Services.AddSingleton<IBasePlatformApi>(sp => 
    {
        var mock = Substitute.For<IBasePlatformApi>();
        // Setup default responses
        return mock;
    });
}
```

**Files to Modify:**
- `MaterialClient.Common.Tests\MaterialClientTestBaseModule.cs`
- OR create `MaterialClient.Common.Tests\EntityFrameworkCore\MaterialClientEntityFrameworkCoreTestModule.cs` with API mocks

**Expected Outcome:**
- ✅ All BDD scenarios can initialize
- ✅ Authentication tests can run
- ✅ ~15-20 additional tests become executable

---

#### 2. Isolate External Service Dependencies

**Option A: Mock External Services (Recommended)**

Create stub implementations for external services:

```csharp
// Tests\Stubs\StubSoundDeviceService.cs
public class StubSoundDeviceService : ISoundDeviceService
{
    public Task<string> PlayTextAsync(string text, CancellationToken ct)
    {
        // Simulate successful response
        return Task.FromResult("{\"success\": true, \"message\": \"Stub response\"}");
    }
}
```

**Option B: Use Test Containers**

For CI/CD environments, consider using Testcontainers to spin up mock HTTP services.

**Option C: Conditional Test Execution**

Add environment-based test execution:

```csharp
[Fact]
public async Task PlayAudioAsync_Should_PlayAudioSuccessfully()
{
    // Skip if external service not available
    if (!await IsSoundDeviceAvailableAsync())
    {
        Skip.If(true, "Sound device service not available");
    }
    
    // Test implementation...
}
```

---

#### 3. Fix Timeout Issues

**Investigation Steps:**
1. Add logging to `TruckScaleWeightServiceTests.DisposeAsync_Should_CleanupResources`
2. Verify serial port disposal logic
3. Check for deadlocks in disposal chain
4. Consider increasing timeout or making async disposal more robust

**Potential Fix:**
```csharp
[Fact]
public async Task DisposeAsync_Should_CleanupResources()
{
    // Arrange
    var service = GetRequiredService<ITruckScaleWeightService>();
    
    // Act
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    await service.DisposeAsync().AsTask(cts.Token);
    
    // Assert
    // Verify cleanup...
}
```

---

### Short-Term Actions (MEDIUM PRIORITY)

#### 4. Create Test Documentation

**Document:**
- Which tests require external services
- How to setup local development environment for integration tests
- CI/CD test execution strategy
- Manual test execution procedures

**Suggested File:** `MaterialClient.Common.Tests\RUNNING_TESTS.md`

#### 5. Separate Test Categories

**Create Test Collections:**
```csharp
// Fast unit tests (no external dependencies)
[Collection("Unit")]
public class ConfigurationUnitTests { }

// Integration tests (mock external services)
[Collection("Integration")]
public class AuthenticationIntegrationTests { }

// Hardware tests (require physical devices)
[Collection("Hardware")]
[Trait("Category", "Manual")]
public class HikvisionHardwareTests { }
```

**CI/CD Filter:**
```bash
# Run only unit and integration tests (exclude hardware)
dotnet test --filter "Category!=Manual"
```

---

### Long-Term Actions (LOW PRIORITY)

#### 6. Implement Test Data Builders

Replace manual entity creation with fluent builders:

```csharp
// Before
await _testService.CreateMaterialAsync(
    name: "测试物料",
    code: "MAT001",
    coId: 1
);

// After
var material = new MaterialBuilder()
    .WithName("测试物料")
    .WithCode("MAT001")
    .WithCompanyId(1)
    .Build();
await _testService.CreateAsync(material);
```

#### 7. Add Contract Testing

For external API dependencies, implement contract tests:
- Pact/Pactflow for API contract verification
- Ensure MaterialPlatformApi responses match expectations

#### 8. Performance Test Baseline

Currently missing performance baseline tests. Consider adding:
- Database query performance tests
- Weighing calculation performance benchmarks
- Camera capture throughput tests

---

## Test Execution Strategy

### CI/CD Pipeline (Automated)

```bash
# Phase 1: Fast unit tests (< 1 minute)
dotnet test --filter "FullyQualifiedName!~Integration&Category!=Manual" --logger "trx"

# Phase 2: Integration tests with mocks (< 5 minutes)
dotnet test --filter "FullyQualifiedName~Integration&Category!=Manual" --logger "trx"
```

**Expected Pass Rate:** ~90% (after DI fixes)

### Local Development (Semi-Automated)

```bash
# Run all tests except hardware-dependent ones
dotnet test --filter "Category!=Manual"
```

**Expected Pass Rate:** ~95% (after DI and external service fixes)

### Manual Testing (Pre-Release)

```bash
# Run hardware-dependent tests
dotnet test --filter "Category=Manual"
```

**Requirements:**
- Hikvision camera connected at configured IP
- Ticket printer connected
- Sound device service running on localhost:8888
- TTS service running on localhost:10008

**Expected Pass Rate:** ~100% (in properly configured environment)

---

## Risk Assessment

| Risk | Impact | Likelihood | Mitigation |
|------|--------|------------|------------|
| BDD tests never run due to DI issues | HIGH | Certain | Immediate DI fix required |
| External service failures block CI/CD | HIGH | High | Mock external services |
| Hardware tests never executed | MEDIUM | Medium | Manual test checklist |
| Test coverage gaps | MEDIUM | Medium | Add missing unit tests |
| Timeout issues in production | LOW | Low | Fix disposal logic |

---

## Metrics

### Current State
- **Total Test Files:** 37
- **Total Tests:** ~60 (estimated)
- **Passing:** 26 (43%)
- **Failing:** 2 (3%)
- **Skipped:** 31 (52%)
- **Not Executable:** ~20 (33% - DI issues)

### Target State (After Fixes)
- **Passing:** 55+ (92%)
- **Failing:** 0 (0%)
- **Skipped (Manual):** 31 (52%)
- **Executable in CI/CD:** ~29 tests (48%)

---

## Conclusion

The test suite has **good coverage** but suffers from **infrastructure issues** that prevent effective test execution:

1. **Critical blocker:** Dependency injection configuration prevents BDD scenarios from running
2. **External dependencies:** Tests depend on external HTTP services without fallback mechanisms
3. **Hardware dependencies:** Properly categorized but need manual execution procedures

**Immediate Priority:**
Fix DI issues in test module to unblock BDD scenarios (~15-20 tests).

**Short-term Priority:**
Mock external services to enable full CI/CD test execution.

**Long-term Priority:**
Establish comprehensive test execution strategy with proper categorization.

---

## Appendix

### Test File Inventory

#### Unit Tests (Fast, No Dependencies)
- ✅ `ConfigurationTestExamples.cs`
- ✅ `PasswordEncryptionServiceTests.cs`
- ✅ `ReaderWriterLockSlimExtensionsTests.cs`
- ✅ `MaterialMathTests.cs`
- ✅ `WeighingMatchingServiceSolidWasteTransferTests.cs`
- ✅ `MachineCodeServiceTests.cs`
- ✅ `WeightScaleRxTests.cs` (partial)

#### Integration Tests (Require Fixes)
- ❌ `AuthenticationSteps.cs` (DI issue)
- ❌ `WeighingServiceSteps.cs` (DI issue)
- ❌ `WeighingMatchingServiceSteps.cs` (DI issue)
- ❌ `SoundDeviceApiTests.cs` (external service)
- ❌ `SoundDeviceServiceTests.cs` (external service)
- ⏱️ `TruckScaleWeightServiceTests.cs` (timeout)
- ⚠️ `AttendedWeighingServiceTests.cs` (depends on DI fixes)

#### Hardware Tests (Manual Execution Required)
- ⏸️ `HikvisionIntegrationTests.cs` (18 tests)
- ⏸️ `HikvisionServiceTests.cs` (6 tests)
- ⏸️ `TicketPrintingServiceTests.cs` (13 tests)
- ⏸️ `PlayM4DecoderNativeCrashTests.cs`

#### Pending Implementation
- 💤 `LicenseServiceIntegrationTests.cs` (commented out, waiting for LicenseService)

---

**Report Generated:** 2026-01-22  
**Next Review:** After DI fixes are implemented  
**Owner:** Development Team
