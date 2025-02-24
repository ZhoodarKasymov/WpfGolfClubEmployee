using GolfClubSystem.Context;
using GolfClubSystem.Data.Repositories;
using GolfClubSystem.Models;

namespace GolfClubSystem.Data;

public class UnitOfWork : IDisposable
{
    public readonly MyDbContext Context;
    public GenericRepository<User> UserRepository { get; }
    public GenericRepository<Worker> WorkerRepository { get; }
    public GenericRepository<Organization> OrganizationRepository { get; }
    public GenericRepository<Zone> ZoneRepository { get; }
    public GenericRepository<Schedule> ScheduleRepository { get; }
    public GenericRepository<Employeehistory> HistoryRepository { get; }
    public GenericRepository<NotifyHistory> NotifyHistoryRepository { get; }
    public GenericRepository<NotifyJob> NotifyJobRepository { get; }

    public UnitOfWork()
    {
        Context = new AppDbContextFactory().CreateDbContext([]);
        UserRepository = new GenericRepository<User>(Context);
        WorkerRepository = new GenericRepository<Worker>(Context);
        OrganizationRepository = new GenericRepository<Organization>(Context);
        ZoneRepository = new GenericRepository<Zone>(Context);
        ScheduleRepository = new GenericRepository<Schedule>(Context);
        HistoryRepository = new GenericRepository<Employeehistory>(Context);
        NotifyHistoryRepository = new GenericRepository<NotifyHistory>(Context);
        NotifyJobRepository = new GenericRepository<NotifyJob>(Context);
    }

    public async Task SaveAsync()
    {
        await Context.SaveChangesAsync();
    }

    public void Dispose()
    {
        Context.Dispose();
    }
}