namespace GrowthLog.Web.Models;

public static class MembershipKinds
{
    public const string Adult = "Adult";
    public const string Child = "Child";

    public static readonly string[] All = { Adult, Child };
}

public class PersonFamilyMembership
{
    public string Id { get; set; } = Guid.NewGuid().ToString("d");
    public string PersonId { get; set; } = string.Empty;
    public string FamilyId { get; set; } = string.Empty;
    public string MembershipKind { get; set; } = MembershipKinds.Child;
    public string JoinedUtc { get; set; } = DateTime.UtcNow.ToString("o");
    public bool IsActive { get; set; } = true;
}
