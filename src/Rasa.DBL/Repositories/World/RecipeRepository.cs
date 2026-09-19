using System.Collections.Generic;
using System.Linq;

namespace Rasa.Repositories.World
{
    using Context.World;
    using Structures.World;

    public interface IRecipeRepository
    {
        List<RecipeEntry> GetRecipes();
        List<RecipeInputEntry> GetRecipeInputs();
    }

    public class RecipeRepository : IRecipeRepository
    {
        private readonly WorldContext _worldContext;

        public RecipeRepository(WorldContext worldContext)
        {
            _worldContext = worldContext;
        }

        public List<RecipeEntry> GetRecipes()
        {
            return _worldContext.CreateNoTrackingQuery(_worldContext.RecipeEntries).ToList();
        }

        public List<RecipeInputEntry> GetRecipeInputs()
        {
            return _worldContext.CreateNoTrackingQuery(_worldContext.RecipeInputEntries).ToList();
        }
    }
}
