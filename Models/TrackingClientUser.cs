namespace handytool_api.Models;

/// <summary>
/// "This browser later turned out to belong to this account."
///
/// Written once, the first time an authenticated request arrives from a client. It is deliberately
/// many-to-many in both directions:
/// <list type="bullet">
///   <item>one user has many clients - two browsers, incognito, a phone, a tablet;</item>
///   <item>one client has many users - a shared machine, or a demo account.</item>
/// </list>
/// This row is what lets us reach back to a visitor's anonymous history without ever rewriting it.
/// </summary>
public class TrackingClientUser
{
    public long Id { get; set; }

    public Guid ClientId { get; set; }

    public long UserId { get; set; }

    /// <summary>When this client was first seen carrying this user's credential.</summary>
    public DateTime FirstIdentifiedAt { get; set; }

    /// <summary>Most recent authenticated sighting. Cheap way to rank a user's devices by recency.</summary>
    public DateTime LastIdentifiedAt { get; set; }

    public TrackingClient Client { get; set; } = null!;

    public UserAccount User { get; set; } = null!;
}
