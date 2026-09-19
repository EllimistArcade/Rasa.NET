using System;
using Microsoft.EntityFrameworkCore.Storage;

namespace Rasa.Repositories.UnitOfWork
{
    public interface IUnitOfWork : IDisposable
    {
        void Complete();
        void Reject();

        /// <summary>
        /// Wraps everything done through this unit of work until the returned transaction is
        /// committed, including the SaveChanges that several repository methods do for
        /// themselves. Without it, an operation spanning more than one of those - a trade moving
        /// four items and two credit balances - commits in pieces, and a failure halfway leaves
        /// the earlier pieces done.
        /// </summary>
        IDbContextTransaction BeginTransaction();
    }
}