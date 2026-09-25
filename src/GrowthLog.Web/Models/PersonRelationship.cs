namespace GrowthLog.Web.Models;

public static class RelationshipTypes
{
    public const string Parent = "Parent";
    public const string Spouse = "Spouse";

    public static readonly string[] All = { Parent, Spouse };
}

public class PersonRelationship
{
    public string Id { get; set; } = Guid.NewGuid().ToString("d");
    public string FromPersonId { get; set; } = string.Empty;
    public string ToPersonId { get; set; } = string.Empty;
    public string RelationshipType { get; set; } = RelationshipTypes.Parent;
    public string CreatedUtc { get; set; } = DateTime.UtcNow.ToString("o");
}

/// <summary>
/// A relationship joined with the names of both people, for display.
/// </summary>
public class PersonRelationshipView
{
    public string Id { get; set; } = string.Empty;
    public string FromPersonId { get; set; } = string.Empty;
    public string FromPersonName { get; set; } = string.Empty;
    public string ToPersonId { get; set; } = string.Empty;
    public string ToPersonName { get; set; } = string.Empty;
    public string RelationshipType { get; set; } = string.Empty;
}
