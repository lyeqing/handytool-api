namespace handytool_api.Models;

/// <summary>
/// The kind of client an analytics identity belongs to. The website is the only implemented one
/// today, but the tracking tables are keyed on (ClientType, ClientId) so native apps can be added
/// without a schema change: a web client's id is the <c>visitor_id</c> cookie, a native client's id
/// would be its persistent installation id.
/// </summary>
public enum ClientType
{
    Web = 0,
    Ios = 1,
    Android = 2
}
