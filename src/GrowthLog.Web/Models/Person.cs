namespace GrowthLog.Web.Models;

public class Person
{
    public string Id { get; set; } = Guid.NewGuid().ToString("d");
    public string? UserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string? LastName { get; set; }
    public string DateOfBirth { get; set; } = string.Empty; // YYYY-MM-DD
    public string? AvatarUrl { get; set; }
    public string CreatedUtc { get; set; } = DateTime.UtcNow.ToString("o");
    public bool IsDeleted { get; set; }

    public string FullName => string.IsNullOrWhiteSpace(LastName)
        ? FirstName
        : $"{FirstName} {LastName}";
}
