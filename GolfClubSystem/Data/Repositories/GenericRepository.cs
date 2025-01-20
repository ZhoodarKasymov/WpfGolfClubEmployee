using GolfClubSystem.Context;
using Microsoft.EntityFrameworkCore;

namespace GolfClubSystem.Data.Repositories
{
    public class GenericRepository<T> where T : class
    {
        private readonly MyDbContext _context;
        private readonly DbSet<T> _dbSet;

        public GenericRepository(MyDbContext context)
        {
            _context = context;
            _dbSet = _context.Set<T>();
        }

        public IQueryable<T> GetAll(bool asNoTracking = false)
        {
            return asNoTracking ? _dbSet.AsNoTracking() : _dbSet;
        }

        public async Task<T> GetByIdAsync(int id)
        {
            return await _dbSet.FindAsync(id);
        }

        public async Task AddAsync(T entity)
        {
            await _dbSet.AddAsync(entity);
            await _context.SaveChangesAsync();
        }

        public async Task AddRangeAsync(IEnumerable<T> entities)
        {
            await _dbSet.AddRangeAsync(entities);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(T entity)
        {
            var trackedEntity = _context.ChangeTracker.Entries<T>().FirstOrDefault(e => e.Entity == entity);
            if (trackedEntity == null)
            {
                _dbSet.Attach(entity);
            }

            _dbSet.Update(entity);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateRangeAsync(IEnumerable<T> entities)
        {
            foreach (var entity in entities)
            {
                var trackedEntity = _context.ChangeTracker.Entries<T>().FirstOrDefault(e => e.Entity == entity);
                if (trackedEntity == null)
                {
                    _dbSet.Attach(entity);
                }

                _dbSet.Update(entity);
            }
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var entity = await GetByIdAsync(id);
            if (entity is not null)
            {
                _dbSet.Remove(entity);
                await _context.SaveChangesAsync();
            }
        }

        public void Attach(T entity)
        {
            _dbSet.Attach(entity);
        }
    }
}