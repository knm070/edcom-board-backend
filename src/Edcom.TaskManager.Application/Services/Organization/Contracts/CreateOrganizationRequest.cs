namespace Edcom.TaskManager.Application.Services.Organization.Contracts;

public class CreateOrganizationRequest
{
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public string? Description { get; set; }
    public string? LogoUrl { get; set; }
}
