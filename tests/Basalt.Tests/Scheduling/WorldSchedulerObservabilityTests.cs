namespace Basalt.Tests.Scheduling;

using Basalt.Server;
using Basalt.Server.Scheduling;
using Basalt.Server.World;
using Basalt.Tests.Support;

public sealed class WorldSchedulerObservabilityTests
{
    [Fact]
    public void GetMetrics_ReturnsAllWorkers()
    {
        Server server = TestServerFactory.CreateMultiWorkerServer(workerCount: 4);

        try
        {
            IReadOnlyList<WorkerLoadMetrics> metrics = server.Scheduler.GetMetrics();

            Assert.Equal(4, metrics.Count);
            Assert.Equal([0, 1, 2, 3], metrics.Select(static metric => metric.WorkerId).OrderBy(static id => id));
        }
        finally
        {
            server.Stop();
        }
    }

    [Fact]
    public void Metrics_ActiveWorldCount_UpdatesOnAttachDetach()
    {
        Server server = TestServerFactory.CreateMultiWorkerServer(workerCount: 4);
        WorldScheduler scheduler = TestServerFactory.RequireWorldScheduler(server);

        try
        {
            World world = TestServerFactory.CreateRegisteredWorld(server, "metrics_world", [0, 1]);
            server.Scheduler.RequestAttach(world);
            TestServerFactory.DrainAllWorkers(scheduler);

            Assert.NotNull(world.AttachedWorkerId);
            int workerId = world.AttachedWorkerId.Value;
            WorkerLoadMetrics attachedMetrics = server.Scheduler.GetMetrics()[workerId];
            Assert.True(attachedMetrics.ActiveWorldCount >= 1);

            world.PresentPlayerCount = 0;
            server.Scheduler.RequestDetach(world);
            TestServerFactory.DrainAllWorkers(scheduler);

            WorkerLoadMetrics detachedMetrics = server.Scheduler.GetMetrics()[workerId];
            Assert.Equal(0, detachedMetrics.ActiveWorldCount);
            Assert.Null(world.AttachedWorkerId);
        }
        finally
        {
            server.Stop();
        }
    }

    [Fact]
    public void RunOnWorldThread_ExecutesOnWorkerThread()
    {
        Server server = TestServerFactory.CreateMultiWorkerServer(workerCount: 4);
        WorldScheduler scheduler = TestServerFactory.RequireWorldScheduler(server);

        try
        {
            World world = TestServerFactory.CreateRegisteredWorld(server, "thread_world", [0, 1]);
            server.Scheduler.RequestAttach(world);
            TestServerFactory.DrainAllWorkers(scheduler);

            Assert.NotNull(world.AttachedWorkerId);
            WorldWorker worker = scheduler.Pool.GetWorker(world.AttachedWorkerId.Value);

            int callerThreadId = Environment.CurrentManagedThreadId;
            int actionThreadId = -1;
            server.RunOnWorldThread(world, () => actionThreadId = Environment.CurrentManagedThreadId);

            Assert.NotEqual(callerThreadId, actionThreadId);
            Assert.Equal(worker.WorkerThreadId, actionThreadId);
            Assert.Equal(worker.LastActionThreadId, actionThreadId);
        }
        finally
        {
            server.Stop();
        }
    }
}
