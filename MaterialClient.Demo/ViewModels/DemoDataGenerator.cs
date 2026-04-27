using MaterialClient.Demo.Models;

namespace MaterialClient.Demo.ViewModels;

public static class DemoDataGenerator
{
    public static List<CandidateRecord> GetDemoRecords()
    {
        var now = DateTime.Now;
        return
        [
            new() { LicensePlate = "YueB12345", Supplier = "XX Building Materials", Weight = 25.50, EntryTime = now.AddMinutes(-5), ElapsedTime = "5 min" },
            new() { LicensePlate = "YueB67890", Supplier = "YY Sand & Gravel", Weight = 32.00, EntryTime = now.AddMinutes(-10), ElapsedTime = "10 min" },
            new() { LicensePlate = "YueC11111", Supplier = "ZZ Concrete Co.", Weight = 48.75, EntryTime = now.AddMinutes(-15), ElapsedTime = "15 min" },
            new() { LicensePlate = "YueB22222", Supplier = "AA Transport Ltd.", Weight = 18.30, EntryTime = now.AddMinutes(-20), ElapsedTime = "20 min" },
            new() { LicensePlate = "YueD33333", Supplier = "XX Building Materials", Weight = 55.00, EntryTime = now.AddMinutes(-25), ElapsedTime = "25 min" },
            new() { LicensePlate = "YueB44444", Supplier = "BB Logistics", Weight = 22.80, EntryTime = now.AddMinutes(-30), ElapsedTime = "30 min" },
            new() { LicensePlate = "YueC55555", Supplier = "YY Sand & Gravel", Weight = 40.10, EntryTime = now.AddMinutes(-35), ElapsedTime = "35 min" },
            new() { LicensePlate = "YueA66666", Supplier = "CC Mining Corp.", Weight = 60.00, EntryTime = now.AddMinutes(-40), ElapsedTime = "40 min" },
        ];
    }
}
