using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Options;

using JetBrains.Annotations;

namespace Rasa.Context
{
    using Configuration;
    using Configuration.ContextSetup;
    using Initialization;
    using Repositories;
    using Structures.Interfaces;

    public abstract class RasaDbContextBase : DbContext, IInitializable
    {
        private readonly IOptions<DatabaseConfiguration> _databaseConfiguration;
        private readonly IDbContextConfigurationService _dbContextConfigurationService;

        protected RasaDbContextBase(IOptions<DatabaseConfiguration> databaseConfiguration, 
            IDbContextConfigurationService dbContextConfigurationService)
        {
            _databaseConfiguration = databaseConfiguration;
            _dbContextConfigurationService = dbContextConfigurationService;
        }

        public IQueryable<T> CreateTrackingQuery<T>(IQueryable<T> query)
            where T : class
        {
            return query.AsTracking();
        }

        public IQueryable<T> CreateNoTrackingQuery<T>(IQueryable<T> query)
            where T : class
        {
            return query.AsNoTracking();
        }

        [CanBeNull]
        public T Find<T>(IQueryable<T> query, uint id)
            where T : class, IHasId
        {
            return query.FirstOrDefault(e => e.Id == id);
        }

        [NotNull]
        public T FindEnsuring<T>(IQueryable<T> query, uint id)
            where T : class, IHasId
        {
            return query.FirstOrDefault(e => e.Id == id) ?? throw CreateNotFound<T>(id);
        }

        [CanBeNull]
        public T GetReadable<T>(IQueryable<T> query, uint id)
            where T : class, IHasId
        {
                query = CreateNoTrackingQuery(query);
                    return Find(query, id);
        }

        [NotNull]
        public T GetReadableEnsuring<T>(IQueryable<T> dbSet, uint id)
            where T : class, IHasId
        {
            return GetReadable(dbSet, id) ?? throw CreateNotFound<T>(id);
        }

        [CanBeNull]
        public T GetWritable<T>(IQueryable<T> query, uint id)
            where T : class, IHasId
        {
            query = CreateTrackingQuery(query);
            return Find(query, id);
        }

        [NotNull]
        public T GetWritableEnsuring<T>(IQueryable<T> dbSet, uint id)
            where T : class, IHasId
        {
            return GetWritable(dbSet, id) ?? throw CreateNotFound<T>(id);
        }

        private static EntityNotFoundException CreateNotFound<T>(uint id) where T : class, IHasId
        {

            return new EntityNotFoundException(typeof(T).Name, nameof(IHasId.Id), id);
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var configuration = GetDatabaseConnectionConfiguration();
            _dbContextConfigurationService.Configure(optionsBuilder, configuration);
        }

        protected abstract DatabaseConnectionConfiguration GetDatabaseConnectionConfiguration();

        /// <summary>
        /// Applies the pending migrations for this context, one at a time, naming each one as it
        /// starts and again as it finishes.
        ///
        /// Database.Migrate() applies the whole backlog behind a single call, so a first run on an
        /// empty database - which is every migration this repository has, some of them seeding
        /// thousands of rows - showed one line and then nothing for however long it took. Naming
        /// the migration that is running is the difference between a server that looks wedged and
        /// one that is visibly working, and when a migration does fail, the last line printed is
        /// the one that failed.
        ///
        /// IMigrator.Migrate(target) applies everything up to and including its target; walking
        /// GetPendingMigrations() in order therefore applies exactly one per call.
        ///
        /// This is still Sqlite only, as it was: MySql schemas are managed outside the server, and
        /// a server that migrated them on boot would apply a schema change to a live database
        /// without anyone asking it to.
        /// </summary>
        public void Initialize()
        {
            if (_databaseConfiguration.Value.GetDatabaseProvider() != DatabaseProvider.Sqlite)
                return;

            var context = GetType().Name;
            var pending = this.Database.GetPendingMigrations().ToList();

            if (pending.Count == 0)
            {
                Console.WriteLine($"{context}: schema is up to date, nothing to apply.");
                return;
            }

            Console.WriteLine($"{context}: applying {pending.Count} migration{(pending.Count == 1 ? "" : "s")}, please wait...");

            var migrator = this.GetService<IMigrator>();
            var run = Stopwatch.StartNew();

            for (var i = 0; i < pending.Count; i++)
            {
                var step = Stopwatch.StartNew();

                Console.WriteLine($"{context}: [{i + 1}/{pending.Count}] {pending[i]} - starting");
                migrator.Migrate(pending[i]);
                step.Stop();
                Console.WriteLine($"{context}: [{i + 1}/{pending.Count}] {pending[i]} - done in {step.ElapsedMilliseconds} ms");
            }

            run.Stop();

            Console.WriteLine($"{context}: {pending.Count} migration{(pending.Count == 1 ? "" : "s")} applied in {run.ElapsedMilliseconds} ms. Database ready.");
        }
    }
}