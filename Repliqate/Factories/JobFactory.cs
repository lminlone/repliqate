using Quartz;
using Quartz.Simpl;
using Quartz.Spi;

namespace Repliqate.Factories;

public class JobFactory : SimpleJobFactory
{
    private readonly IServiceProvider _provider;

    public JobFactory(IServiceProvider serviceProvider)
    {
        _provider = serviceProvider;
    }

    public override IJob NewJob(TriggerFiredBundle bundle, IScheduler scheduler)
    {
        try
        {
            // this will inject dependencies that the job requires
            return (IJob)_provider.GetService(bundle.JobDetail.JobType);
        }
        catch (Exception e)
        {
            throw new SchedulerException(string.Format("Problem while instantiating job '{0}' from the Aspnet Core IOC.", bundle.JobDetail.Key), e);
        }

    }
}