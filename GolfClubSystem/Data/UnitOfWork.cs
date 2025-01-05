using GolfClubSystem.Context;
using GolfClubSystem.Data.Repositories;
using GolfClubSystem.Models;

namespace GolfClubSystem.Data;

public class UnitOfWork : IDisposable
{
    private readonly MyDbContext _context;
    public GenericRepository<User> UserRepository { get; }
    public GenericRepository<Worker> WorkerRepository { get; }
    public GenericRepository<Organization> OrganizationRepository { get; }
    public GenericRepository<Zone> ZoneRepository { get; }

    public UnitOfWork()
    {
        _context = new AppDbContextFactory().CreateDbContext([]);
        UserRepository = new GenericRepository<User>(_context);
        WorkerRepository = new GenericRepository<Worker>(_context);
        OrganizationRepository = new GenericRepository<Organization>(_context);
        ZoneRepository = new GenericRepository<Zone>(_context);
    }

    public async Task SaveAsync()
    {
        await _context.SaveChangesAsync();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}