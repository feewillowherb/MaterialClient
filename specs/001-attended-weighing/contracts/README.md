# API Contracts: 有人值守功能

**Date**: 2025-11-06  
**Feature**: [spec.md](../spec.md)

## Overview

This feature requires HTTP APIs for hardware interface testing and configuration. The APIs are implemented as ABP API Controllers and exposed via Swagger.

## Hardware Testing APIs

### 1. Truck Scale Weight API

**Base Path**: `/api/hardware/truck-scale`

#### GET /api/hardware/truck-scale/weight

Get current truck scale weight value (for testing).

**Response**:
```json
{
  "weight": 1234.56
}
```

**Status Codes**:
- 200 OK: Success
- 500 Internal Server Error: Service error

---

#### POST /api/hardware/truck-scale/weight

Set truck scale weight test value.

**Request Body**:
```json
{
  "weight": 1234.56
}
```

**Response**:
```json
{
  "success": true,
  "message": "Weight value updated"
}
```

**Status Codes**:
- 200 OK: Success
- 400 Bad Request: Invalid weight value
- 500 Internal Server Error: Service error

---

### 2. Plate Number Capture API

**Base Path**: `/api/hardware/plate-number`

#### GET /api/hardware/plate-number

Get current captured plate number (for testing).

**Response**:
```json
{
  "plateNumber": "京A12345"
}
```

**Status Codes**:
- 200 OK: Success
- 500 Internal Server Error: Service error

---

#### POST /api/hardware/plate-number

Set plate number test value.

**Request Body**:
```json
{
  "plateNumber": "京A12345"
}
```

**Response**:
```json
{
  "success": true,
  "message": "Plate number updated"
}
```

**Status Codes**:
- 200 OK: Success
- 400 Bad Request: Invalid input
- 500 Internal Server Error: Service error

---

### 3. Vehicle Photo API

**Base Path**: `/api/hardware/vehicle-photos`

#### GET /api/hardware/vehicle-photos

Get vehicle photos (returns 4 identical test images).

**Response**:
```json
{
  "photos": [
    {
      "url": "https://fastly.picsum.photos/id/201/500/300.jpg?hmac=v0GEqa-YATYYy9hkxWbMmoxVAp_JtNKUSpkfKBtwuBE",
      "index": 1
    },
    {
      "url": "https://fastly.picsum.photos/id/201/500/300.jpg?hmac=v0GEqa-YATYYy9hkxWbMmoxVAp_JtNKUSpkfKBtwuBE",
      "index": 2
    },
    {
      "url": "https://fastly.picsum.photos/id/201/500/300.jpg?hmac=v0GEqa-YATYYy9hkxWbMmoxVAp_JtNKUSpkfKBtwuBE",
      "index": 3
    },
    {
      "url": "https://fastly.picsum.photos/id/201/500/300.jpg?hmac=v0GEqa-YATYYy9hkxWbMmoxVAp_JtNKUSpkfKBtwuBE",
      "index": 4
    }
  ]
}
```

**Status Codes**:
- 200 OK: Success
- 500 Internal Server Error: Service error

---

### 4. Bill Photo API

**Base Path**: `/api/hardware/bill-photo`

#### GET /api/hardware/bill-photo

Get bill photo (returns 1 test image).

**Response**:
```json
{
  "photo": {
    "url": "https://fastly.picsum.photos/id/201/500/300.jpg?hmac=v0GEqa-YATYYy9hkxWbMmoxVAp_JtNKUSpkfKBtwuBE"
  }
}
```

**Status Codes**:
- 200 OK: Success
- 500 Internal Server Error: Service error

---

## Implementation Notes

- All APIs are implemented as ABP API Controllers
- Controllers inherit from `AbpControllerBase` or similar ABP base class
- APIs are exposed via Swagger UI (accessible at `/swagger` when HTTP Host is running)
- All hardware services add comment "待对接设备" (Device pending integration)
- Test values are stored in memory (can be reset on application restart)
- No authentication required (current stage)

## Service Interfaces (Internal)

### ITruckScaleWeightService

```csharp
public interface ITruckScaleWeightService
{
    Task<decimal> GetWeightAsync();
    Task SetWeightAsync(decimal weight);
}
```

### IPlateNumberCaptureService

```csharp
public interface IPlateNumberCaptureService
{
    Task<string?> CaptureAsync();
    Task SetTestValueAsync(string? plateNumber);
}
```

### IVehiclePhotoService

```csharp
public interface IVehiclePhotoService
{
    Task<IList<VehiclePhoto>> GetPhotosAsync();
}

public class VehiclePhoto
{
    public string Url { get; set; }
    public int Index { get; set; }
}
```

### IBillPhotoService

```csharp
public interface IBillPhotoService
{
    Task<BillPhoto?> GetPhotoAsync();
}

public class BillPhoto
{
    public string Url { get; set; }
}
```

