using Edcom.TaskManager.Application.Services.User.Contracts;

namespace Edcom.TaskManager.Application.Services.User;

public class UpdateUserAdminRequestValidator : AbstractValidator<UpdateUserAdminRequest>
{
    public UpdateUserAdminRequestValidator()
    {
        // IsSystemAdmin is a bool — no additional validation needed
    }
}
