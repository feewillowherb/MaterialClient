namespace MaterialClient.Common.Services.Hardware;

/// <summary>
/// Bill photo service interface
/// </summary>
public interface IBillPhotoService
{
    /// <summary>
    /// Capture bill photo
    /// </summary>
    /// <returns>Photo file path (can be empty if capture fails)</returns>
    Task<string?> CaptureBillPhotoAsync();
}

/// <summary>
/// Bill photo service implementation
/// Returns fixed test file path for hardware simulation
/// TODO: Replace with actual hardware integration when device is available
/// </summary>
public class BillPhotoService : IBillPhotoService
{
    private string? _testBillPhotoPath = "assets/test_bill.jpg"; // Default test bill photo

    /// <summary>
    /// Capture bill photo
    /// </summary>
    /// <returns>Photo file path (can be empty if capture fails)</returns>
    public Task<string?> CaptureBillPhotoAsync()
    {
        // Return fixed test file path
        // In production, this would capture photo from actual camera hardware
        return Task.FromResult(_testBillPhotoPath);
    }

    /// <summary>
    /// Set test bill photo path for testing purposes (for hardware simulation API)
    /// </summary>
    /// <param name="photoPath">Photo file path</param>
    public void SetBillPhotoPath(string? photoPath)
    {
        _testBillPhotoPath = photoPath;
    }

    /// <summary>
    /// Get current test bill photo path
    /// </summary>
    public string? GetBillPhotoPath()
    {
        return _testBillPhotoPath;
    }
}

