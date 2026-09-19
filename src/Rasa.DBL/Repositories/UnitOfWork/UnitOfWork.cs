using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Rasa.Repositories.UnitOfWork
{
    public abstract class UnitOfWork
    {
        private readonly DbContext _dbContext;

        protected UnitOfWork(DbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public void Complete()
        {
            if (_dbContext.ChangeTracker.HasChanges())
            {
                _dbContext.SaveChanges();
            }
        }

        public IDbContextTransaction BeginTransaction()
        {
            return _dbContext.Database.BeginTransaction();
        }

        public void Reject()
        {
            if (_dbContext.ChangeTracker.HasChanges())
            {
                _dbContext.ChangeTracker.Clear();
            }
        }

        public void Dispose()
        {
            Reject();
            _dbContext.Dispose();
        }
    }
}