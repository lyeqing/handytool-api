namespace handytool_api.Models;

public class CompanyAccount
{
    // Database columns
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime? ExpiresAt { get; set; }

    public int SeatLimit { get; set; } = 1;

    public int AccountTypeId { get; set; } = AccountType.FreeId;

    public DateTime CreatedDate { get; set; }

    public DateTime ModifiedDate { get; set; }

    // Relationships — not additional database columns
    public AccountType AccountType { get; set; } = null!;

    public ICollection<UserAccount> Users { get; set; } = new List<UserAccount>();
}
