using System.Collections.Generic;
using System.Linq;

namespace Rasa.Repositories.World
{
    using Context.World;
    using Structures.World;

    public interface IActionRepository
    {
        List<ActionEntry> GetActions();
        List<ActionLevelEntry> GetActionLevels();
        List<ActionCostEntry> GetActionCosts();
        List<ActionPropertyEntry> GetActionProperties();
        List<ActionItemRequirementEntry> GetActionItemRequirements();
        List<ItemTemplateActionEntry> GetItemTemplateActions();
        List<SkillCharacterEntry> GetSkillCharacters();
    }

    public class ActionRepository : IActionRepository
    {
        private readonly WorldContext _worldContext;

        public ActionRepository(WorldContext worldContext)
        {
            _worldContext = worldContext;
        }

        public List<ActionEntry> GetActions()
        {
            return _worldContext.CreateNoTrackingQuery(_worldContext.ActionEntries).ToList();
        }

        public List<ActionLevelEntry> GetActionLevels()
        {
            return _worldContext.CreateNoTrackingQuery(_worldContext.ActionLevelEntries).ToList();
        }

        public List<ActionCostEntry> GetActionCosts()
        {
            return _worldContext.CreateNoTrackingQuery(_worldContext.ActionCostEntries).ToList();
        }

        public List<ActionPropertyEntry> GetActionProperties()
        {
            return _worldContext.CreateNoTrackingQuery(_worldContext.ActionPropertyEntries).ToList();
        }

        public List<ActionItemRequirementEntry> GetActionItemRequirements()
        {
            return _worldContext.CreateNoTrackingQuery(_worldContext.ActionItemRequirementEntries).ToList();
        }

        public List<ItemTemplateActionEntry> GetItemTemplateActions()
        {
            return _worldContext.CreateNoTrackingQuery(_worldContext.ItemTemplateActionEntries).ToList();
        }

        /// <summary>
        /// Which class grants each skill and the level it takes, in one read. Loaded once at
        /// startup: 73 rows that never change.
        /// </summary>
        public List<SkillCharacterEntry> GetSkillCharacters()
        {
            return _worldContext.CreateNoTrackingQuery(_worldContext.SkillCharacterEntries).ToList();
        }
    }
}
