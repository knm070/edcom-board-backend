namespace Edcom.TaskManager.Application.Services.Organization.Contracts;

public class OrganizationResponse
{
    public long      Id               { get; set; }
    public string    Name             { get; set; } = null!;
    public string    Slug             { get; set; } = null!;
    public string?   Description      { get; set; }
    public string?   LogoUrl          { get; set; }
    public bool      IsActive         { get; set; }
    public long      CreatedByUserId  { get; set; }
    public DateTime  CreatedAt        { get; set; }
    public DateTime? UpdatedAt        { get; set; }
    public int       MemberCount      { get; set; }
    public int       SpaceCount       { get; set; }
    public int       IssueCount       { get; set; }
}
