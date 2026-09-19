using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Rasa.Repositories.UnitOfWork
{
    public abstract class DelegatingUnitOfWorkBase : IUnitOfWork
    {
        private readonly IUnitOfWork _parent;
        private readonly IServiceScope _scope;

        protected DelegatingUnitOfWorkBase(IUnitOfWork parent, IServiceScope scope)
        {
            _parent = parent;
            _scope = scope;
        }

        public void Complete()
        {
            _parent.Complete();
        }

        public IDbContextTransaction BeginTransaction()
        {
            return _parent.BeginTransaction();
        }

        public void Reject()
        {
            _parent.Reject();
        }

        public void Dispose()
        {
            _scope.Dispose();
        }
    }
}