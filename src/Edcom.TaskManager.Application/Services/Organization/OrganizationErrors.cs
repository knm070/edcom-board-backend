namespace Edcom.TaskManager.Application.Services.Organization;

public static class OrganizationErrors
{
    public static readonly Error NotFound           = Error.NotFound("Organization.NotFound");
    public static readonly Error SlugAlreadyExists  = Error.Conflict("Organization.SlugAlreadyExists");
}
