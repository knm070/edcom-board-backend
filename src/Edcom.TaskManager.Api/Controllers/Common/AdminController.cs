namespace Edcom.TaskManager.Api.Controllers.Common;

[Authorize(Roles = "Admin")]
public abstract class AdminController : AuthorizedController { }
