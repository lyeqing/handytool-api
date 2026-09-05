namespace handytool_api.Models;

/// <summary>Persistent trial consumption: device lifetime counters and IP/day abuse counters.</summary>
public class TrialUsage
{
    // Database columns
    public string Key { get; set; } = string.Empty;
    public int CreatedCount { get; set; }
    public DateTime UpdatedAt { get; set; }
}
