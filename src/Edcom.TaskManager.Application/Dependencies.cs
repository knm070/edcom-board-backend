using Edcom.TaskManager.Application.Services.Auth;
using Edcom.TaskManager.Application.Services.Epic;
using Edcom.TaskManager.Application.Services.OrgMember;
using Edcom.TaskManager.Application.Services.Organization;
using Edcom.TaskManager.Application.Services.Space;
using Edcom.TaskManager.Application.Services.Sprint;
using Edcom.TaskManager.Application.Services.Tag;
using Edcom.TaskManager.Application.Services.Ticket;
using Edcom.TaskManager.Application.Services.TicketComment;
using Edcom.TaskManager.Application.Services.WorkflowStatus;

namespace Edcom.TaskManager.Application;

public static class Dependencies
{
    public static IServiceCollection ConfigureApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining(typeof(Dependencies));

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IOrganizationService, OrganizationService>();
        services.AddScoped<ISpaceService, SpaceService>();
        services.AddScoped<IWorkflowStatusService, WorkflowStatusService>();
        services.AddScoped<ISprintService, SprintService>();
        services.AddScoped<IEpicService, EpicService>();
        services.AddScoped<IOrgMemberService, OrgMemberService>();
        services.AddScoped<ITagService, TagService>();
        services.AddScoped<ITicketService, TicketService>();
        services.AddScoped<ITicketCommentService, TicketCommentService>();

        return services;
    }
}
