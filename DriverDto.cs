using System;
using System.Linq;
namespace Kalendarz1.Domain.Dto;

public sealed class DriverDto
{
    public int DriverID { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName  { get; set; } = "";
    public string FullName  => (FirstName + " " + LastName).Trim();
    public string? Phone { get; set; }
    public bool Active { get; set; }
}

// TVehicle (tractor / trailer) Kind: 1=ci¹gnik, 2=naczepa
public sealed class CarTrailerDto
{
    public int VehicleID { get; set; }
    public int Kind { get; set; } // 1=ci¹gnik,2=naczepa
    public string Registration { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public decimal? CapacityKg { get; set; }
    public int? PalletSlotsH1 { get; set; }
    public decimal? E2Factor { get; set; }
    public bool Active { get; set; }
    public string DisplayName => string.Join(" ", new[]{Registration, Brand, Model}.Where(s=>!string.IsNullOrWhiteSpace(s)));
}

public sealed class LocationDto
{
    public int LocationID { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? PostalCode { get; set; }
    public string? City { get; set; }
    public string? Notes { get; set; }
    public bool Active { get; set; }
}

public sealed class TripDto
{
    public long TripID { get; set; }
    public DateTime TripDate { get; set; }
    public DateTime? PlannedDeparture { get; set; }
    public DateTime? PickupWindowFrom { get; set; }
    public DateTime? PickupWindowTo { get; set; }
    public int? DriverID { get; set; }
    public int? CarID { get; set; } // VehicleID ci¹gnika
    public int? TrailerID { get; set; } // VehicleID naczepy
    public string Status { get; set; } = "Planned";
    public string? Notes { get; set; }
    public string? CreatedBy { get; set; }
}

public sealed class TripListRowDto
{
    public long TripID { get; set; }
    public DateTime TripDate { get; set; }
    public DateTime? PlannedDeparture { get; set; }
    public string? DriverName { get; set; }
    public string? CarName { get; set; }
    public string? TrailerName { get; set; }
    public decimal FillPercent { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public sealed class TripLoadDto
{
    public long LoadID { get; set; }
    public long TripID { get; set; }
    public int LocationID { get; set; }
    public int Pallets { get; set; }
    public int Crates { get; set; }
    public int? GrossWeightKg { get; set; }
    public int? OrderID { get; set; }
    public string? LocationName { get; set; }
}

public sealed class SettingsDto
{
    public int CratesPerPallet { get; set; } = 36;
    public int CrateWeightKg { get; set; } = 15;
}

public sealed class EventLogDto
{
    public DateTime At { get; set; } = DateTime.UtcNow;
    public string User { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Entity { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? Notes { get; set; }
}