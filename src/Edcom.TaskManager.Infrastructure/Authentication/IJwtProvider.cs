namespace Edcom.TaskManager.Infrastructure.Authentication;

public interface IJwtProvider
{
    string Generate(long userId, string email, string role);
}
