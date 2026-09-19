using System.Collections.Generic;

namespace Rasa.Managers
{
    using Game;
    using Repositories.UnitOfWork;
    using Structures;

    /// <summary>
    /// The fabrication recipes, by schematic item template id. Loaded once from recipe and
    /// recipe_input; the rules that use them are in KraftwerksManager.
    /// </summary>
    public class RecipeManager
    {
        private static RecipeManager _instance;
        private static readonly object InstanceLock = new object();

        private readonly IGameUnitOfWorkFactory _gameUnitOfWorkFactory;
        private readonly Dictionary<uint, Recipe> _recipes = new Dictionary<uint, Recipe>();

        public static RecipeManager Instance
        {
            get
            {
                // ReSharper disable once InvertIf
                if (_instance == null)
                {
                    lock (InstanceLock)
                    {
                        if (_instance == null)
                            _instance = new RecipeManager(Server.GameUnitOfWorkFactory);
                    }
                }

                return _instance;
            }
        }

        private RecipeManager(IGameUnitOfWorkFactory gameUnitOfWorkFactory)
        {
            _gameUnitOfWorkFactory = gameUnitOfWorkFactory;
        }

        public int Count => _recipes.Count;

        public bool TryGet(uint schematicTemplateId, out Recipe recipe)
        {
            return _recipes.TryGetValue(schematicTemplateId, out recipe);
        }

        /// <summary>Loads the tables. Runs after the item templates, whose ids it refers to.</summary>
        public void RecipeInit()
        {
            using var unitOfWork = _gameUnitOfWorkFactory.CreateWorld();
            var skipped = 0;

            foreach (var entry in unitOfWork.Recipes.GetRecipes())
            {
                if (!ItemManager.Instance.ItemTemplateItemClass.ContainsKey(entry.Id) || !ItemManager.Instance.ItemTemplateItemClass.ContainsKey(entry.ResultTemplateId))
                {
                    Logger.WriteLog(LogType.Error, $"recipe {entry.Id}: the schematic or its result template {entry.ResultTemplateId} is not an item template; skipped");
                    skipped++;
                    continue;
                }

                _recipes[entry.Id] = new Recipe(entry);
            }

            var inputs = 0;

            foreach (var entry in unitOfWork.Recipes.GetRecipeInputs())
            {
                if (!_recipes.TryGetValue(entry.RecipeId, out var recipe))
                    continue;

                recipe.Inputs.Add(new Recipe.Input { ClassId = entry.InputClassId, Quantity = entry.Quantity });
                inputs++;
            }

            Logger.WriteLog(LogType.Initialize, $"Loaded {_recipes.Count} recipes with {inputs} inputs" + (skipped > 0 ? $" ({skipped} skipped)" : ""));
        }
    }
}
