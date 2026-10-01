namespace GrowthLog.Web.Models;

public class Person
{
    public string Id { get; set; } = Guid.NewGuid().ToString("d");
    public string? UserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string? LastName { get; set; }
    public string DateOfBirth { get; set; } = string.Empty; // YYYY-MM-DD
    public string? AvatarUrl { get; set; }
    public string CreatedUtc { get; set; } = DateTime.UtcNow.ToString("o");
    public string? CreatedByUserId { get; set; }
    public bool IsDeleted { get; set; }

    public string FullName
    {
        get
        {
            var parts = new[] { FirstName, MiddleName, LastName }
                .Where(p => !string.IsNullOrWhiteSpace(p));
            return string.Join(" ", parts);
        }
    }
}
