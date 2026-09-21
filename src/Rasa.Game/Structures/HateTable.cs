using System;
using System.Collections.Generic;
using System.Linq;

namespace Rasa.Structures
{
    /// <summary>
    /// A creature's aggro table: how much it hates each actor that has hurt it, healed its
    /// enemies or been noticed by it, keyed by entity id. What it fights is whoever tops the
    /// table and can still be fought (Managers.Threat.ChooseTarget); what raises an entry is
    /// Managers.Threat's business. A plain dictionary under a lock - the behaviour worker reads
    /// it while missiles and effect ticks on the same map write to it.
    /// </summary>
    public class HateTable
    {
        private readonly Dictionary<ulong, double> _hate = new Dictionary<ulong, double>();
        private readonly object _lock = new object();

        public void Add(ulong entityId, double amount)
        {
            if (entityId == 0 || amount <= 0 || double.IsNaN(amount))
                return;

            lock (_lock)
                _hate[entityId] = (_hate.TryGetValue(entityId, out var hate) ? hate : 0) + amount;
        }

        /// <summary>Makes sure an actor is on the table at all, with at least this much.</summary>
        public void Ensure(ulong entityId, double atLeast)
        {
            if (entityId == 0)
                return;

            lock (_lock)
                if (!_hate.TryGetValue(entityId, out var hate) || hate < atLeast)
                    _hate[entityId] = atLeast;
        }

        public double Of(ulong entityId)
        {
            lock (_lock)
                return _hate.TryGetValue(entityId, out var hate) ? hate : 0;
        }

        public bool Contains(ulong entityId)
        {
            lock (_lock)
                return _hate.ContainsKey(entityId);
        }

        public void Remove(ulong entityId)
        {
            lock (_lock)
                _hate.Remove(entityId);
        }

        public void Clear()
        {
            lock (_lock)
                _hate.Clear();
        }

        public int Count
        {
            get
            {
                lock (_lock)
                    return _hate.Count;
            }
        }

        /// <summary>The entries, most hated first.</summary>
        public List<KeyValuePair<ulong, double>> Ranked()
        {
            lock (_lock)
                return _hate.OrderByDescending(e => e.Value).ToList();
        }

        /// <summary>
        /// Who the creature should be fighting: the most hated entry that <paramref name="canFight"/>
        /// allows, except that the current target keeps it unless someone hates it more by
        /// <paramref name="takeoverPercent"/> - so two attackers doing about the same do not have
        /// the creature turning back and forth every think. 0 when nobody on the table can be
        /// fought.
        /// </summary>
        public ulong Top(Func<ulong, bool> canFight, ulong current, int takeoverPercent)
        {
            var ranked = Ranked();
            var best = ranked.FirstOrDefault(e => canFight(e.Key));

            if (best.Key == 0)
                return 0;

            if (current == 0 || current == best.Key || !canFight(current))
                return best.Key;

            var held = Of(current);

            return best.Value * 100.0 > held * Math.Max(100, takeoverPercent) ? best.Key : current;
        }
    }
}
