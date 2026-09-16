using System.Collections.Generic;
using System.Linq;

namespace Rasa.Repositories.World
{
    using Context.World;
    using Structures.World;

    public interface IMapMarkerRepository
    {
        List<MapMarkerEntry> GetMapMarkers();
    }

    /// <summary>
    /// Read-only. The rows are recovered client data rather than anything the game produces, so
    /// they are seeded by the preloader and changed by regenerating it.
    /// </summary>
    public class MapMarkerRepository : IMapMarkerRepository
    {
        private readonly WorldContext _worldContext;

        public MapMarkerRepository(WorldContext worldContext)
        {
            _worldContext = worldContext;
        }

        public List<MapMarkerEntry> GetMapMarkers()
        {
            return _worldContext.CreateNoTrackingQuery(_worldContext.MapMarkerEntries).ToList();
        }
    }
}
