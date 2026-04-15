namespace Edcom.TaskManager.Application.Jobs;

/// <summary>
/// Registers all Hangfire recurring jobs.
/// Call this after app.UseHangfireDashboard() in Program.cs.
/// </summary>
public static class JobsRegistrar
{
    public static void RegisterRecurringJobs()
    {
        // TODO: Register recurring jobs here as you add them:
        // RecurringJob.AddOrUpdate<IExampleJob>(
        //     "example-job",
        //     job => job.ExecuteAsync(),
        //     "*/30 * * * *");
    }
}
