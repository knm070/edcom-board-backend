namespace Edcom.TaskManager.Application.Services.OrgMember;

public static class OrgMemberErrors
{
    public static readonly Error NotFound = Error.NotFound("OrgMember.NotFound");
    public static readonly Error Forbidden = Error.Failure("OrgMember.Forbidden");
    public static readonly Error AlreadyMember = Error.Conflict("OrgMember.AlreadyMember");
}
