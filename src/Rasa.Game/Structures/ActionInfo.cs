using System.Collections.Generic;

namespace Rasa.Structures
{
    using Data;

    /// <summary>
    /// One action the client knows how to perform, with its per-level data: the action table
    /// plus action_level, action_cost, action_property and action_item_requirement, loaded by
    /// AbilityManager. Mirrors the client's ActorActionInfo (client/actions/__init__.py), which
    /// reads the same tables.
    /// </summary>
    public class ActionInfo
    {
        public ActionId ActionId { get; set; }
        public string Name { get; set; }

        /// <summary>The client module that runs the action, e.g. "abilities.lightning". The best statement of what kind of action it is.</summary>
        public string Module { get; set; }

        public bool IsCharged { get; set; }

        /// <summary>By action arg id: the pump level for abilities.</summary>
        public Dictionary<uint, ActionLevelInfo> Levels { get; } = new Dictionary<uint, ActionLevelInfo>();
    }

    public class ActionLevelInfo
    {
        public ActionId ActionId { get; set; }
        public uint Level { get; set; }

        public int WindupMs { get; set; }
        public int RecoveryMs { get; set; }
        public int MaxRange { get; set; }

        /// <summary>The cooldown, in milliseconds. 0 for none.</summary>
        public int ReuseMs { get; set; }

        /// <summary>
        /// When set the client starts its own cooldown on perform, counting recovery + reuse; the
        /// server does the same so both agree. When clear the server says when the cooldown
        /// starts, with ActionReuseTimerRestarted.
        /// </summary>
        public bool StartReuseOnPerform { get; set; }

        public List<ActionCost> Costs { get; } = new List<ActionCost>();
        public Dictionary<AbilityProperty, int> Properties { get; } = new Dictionary<AbilityProperty, int>();
        public List<ActionItemRequirement> ItemRequirements { get; } = new List<ActionItemRequirement>();

        public bool Has(AbilityProperty property) => Properties.ContainsKey(property);

        public int Get(AbilityProperty property, int fallback = 0) => Properties.TryGetValue(property, out var value) ? value : fallback;
    }

    public struct ActionCost
    {
        public Attributes Attribute;
        public int Amount;
    }

    public struct ActionItemRequirement
    {
        public EntityClasses ItemClass;
        public uint Quantity;
    }
}
