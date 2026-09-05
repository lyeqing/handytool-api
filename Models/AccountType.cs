namespace handytool_api.Models;

/// <summary>Subscription plan identity is separate from the access level it grants.</summary>
public class AccountType
{
    // Constants
    public const int FreeId = 1;

    public const int LightId = 2;

    public const int FullId = 3;

    // Database columns
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int AccessLevel { get; set; }
}
