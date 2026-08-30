namespace handytool_api.Models;

/// <summary>
/// Well-known values for <see cref="AnalyticsEvent.EventType"/>.
///
/// Deliberately constants over an enum: the column is a plain string, so adding "property_viewed" or
/// "contact_form_submitted" later is a one-line change here and needs no migration. Callers may send
/// any snake_case name; these are simply the ones the system itself writes.
/// </summary>
public static class AnalyticsEventTypes
{
    public const string PageView = "page_view";
    public const string Heartbeat = "heartbeat";
    public const string PageLeave = "page_leave";
    public const string Login = "login";
    public const string Logout = "logout";

    /// <summary>Event types the API records on its own behalf; clients cannot claim to be one of these.</summary>
    public static readonly string[] ServerWritten = [Login, Logout];
}
