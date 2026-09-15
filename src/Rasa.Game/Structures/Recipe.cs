using System.Collections.Generic;

namespace Rasa.Structures
{
    using World;

    /// <summary>A fabrication recipe with its ingredients; see RecipeManager.</summary>
    public class Recipe
    {
        public struct Input
        {
            public uint ClassId;
            public uint Quantity;
        }

        /// <summary>The schematic's item template id.</summary>
        public uint TemplateId { get; }
        public uint EnergyCost { get; }
        public uint KraftwerksSeconds { get; }
        public uint ResultTemplateId { get; }
        public uint ResultAmount { get; }
        public uint MinLevel { get; }
        public List<Input> Inputs { get; } = new List<Input>();

        public Recipe(RecipeEntry entry)
        {
            TemplateId = entry.Id;
            EnergyCost = entry.EnergyCost;
            KraftwerksSeconds = entry.KraftwerksSeconds;
            ResultTemplateId = entry.ResultTemplateId;
            ResultAmount = entry.ResultAmount;
            MinLevel = entry.MinLevel;
        }
    }
}
