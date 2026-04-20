using Edcom.TaskManager.Application.Services.User.Contracts;

namespace Edcom.TaskManager.Application.Services.User;

public class UpdateUserStatusRequestValidator : AbstractValidator<UpdateUserStatusRequest>
{
    public UpdateUserStatusRequestValidator()
    {
        // IsActive is a bool — no additional validation needed
    }
}
