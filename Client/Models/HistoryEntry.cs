namespace Client.Models;

public class HistoryEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string PlateNumber { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string? Vin { get; set; }
    public string? Make { get; set; }
    public string? Model { get; set; }
    public string? Year { get; set; }
    public string? YearRange { get; set; }
    public string? Color { get; set; }
    public string? Trim { get; set; }
    public string? BodyStyle { get; set; }
    public string? VehicleType { get; set; }
    public string? FuelType { get; set; }
    public string? EngineDisplacement { get; set; }
    public string? Cylinders { get; set; }
    public string? Transmission { get; set; }
    public string? DriveType { get; set; }
    public string? DataSource { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public string DisplayTitle
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(Year)) parts.Add(Year);
            else if (!string.IsNullOrEmpty(YearRange)) parts.Add(YearRange);
            if (!string.IsNullOrEmpty(Make)) parts.Add(Make);
            if (!string.IsNullOrEmpty(Model)) parts.Add(Model);
            if (parts.Count == 0) return Vin ?? PlateNumber ?? "Unknown";
            return string.Join(" ", parts);
        }
    }

    public string DisplaySubtitle
    {
        get
        {
            if (!string.IsNullOrEmpty(PlateNumber))
            {
                return string.IsNullOrEmpty(State)
                    ? PlateNumber
                    : $"{PlateNumber} ({State})";
            }
            return Vin ?? string.Empty;
        }
    }

    public string RelativeTime
    {
        get
        {
            var diff = DateTime.UtcNow - Timestamp;
            if (diff.TotalMinutes < 1) return "just now";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
            if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
            return Timestamp.ToString("MMM d");
        }
    }
}
