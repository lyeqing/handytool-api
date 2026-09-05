namespace handytool_api.Models;

/// <summary>
/// One device/browser we have seen, anonymous until it identifies itself.
///
/// For <see cref="ClientType.Web"/> the <see cref="Id"/> IS the VisitorId - the opaque GUID held in
/// the HttpOnly <c>visitor_id</c> cookie. It identifies a browser, not a person: incognito windows,
/// a second laptop and a phone are all separate rows.
/// </summary>
public class TrackingClient
{
    // Database columns
    /// <summary>Opaque random GUID. For the website this is the VisitorId. Never derived from a user id or a token.</summary>
    public Guid Id { get; set; }

    public ClientType ClientType { get; set; } = ClientType.Web;

    public DateTime FirstSeenDate { get; set; }

    public DateTime LastSeenDate { get; set; }

    public DateTime CreatedDate { get; set; }

    // Relationships — not additional database columns
    public ICollection<TrackingSession> Sessions { get; set; } = new List<TrackingSession>();

    public ICollection<TrackingClientUser> Users { get; set; } = new List<TrackingClientUser>();
}
