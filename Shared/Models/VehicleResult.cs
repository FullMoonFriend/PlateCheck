namespace Shared.Models;

public class VehicleResult
{
    public string PlateNumber { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string? Vin { get; set; }
    public string? Year { get; set; }
    public string? Make { get; set; }
    public string? Model { get; set; }
    public string? Trim { get; set; }
    public string? BodyStyle { get; set; }
    public string? Color { get; set; }
    public string? FuelType { get; set; }
    public string? EngineDisplacement { get; set; }
    public string? Cylinders { get; set; }
    public string? Transmission { get; set; }
    public string? DriveType { get; set; }
    public string? YearRange { get; set; }
    public string? VehicleType { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? DataSource { get; set; }
}
