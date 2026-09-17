using System.Collections.Generic;

namespace Rasa.Data
{
    /// <summary>
    /// What a harvest yields, by species.
    ///
    /// This one is invented, not recovered, and is kept in code rather than in the database to
    /// say so. tool_type and the creature flags were columns that existed and had been filled
    /// with placeholders; a creature-to-harvest-item table never existed in this schema at all -
    /// CreatureLootData is an empty class and corpse loot is still a coin flip on one template.
    /// Data in a table reads as data that was restored, so until there is something to restore
    /// this lives where its provenance is obvious.
    ///
    /// The items themselves are real: 149 Loot_Junk_&lt;family&gt;_&lt;part&gt; entity classes,
    /// each with exactly one item template, named for the family that drops them and for the
    /// part - Atta_Mandible, Boargar_Sacrum, Machina_Control_Chip. Twenty species have them,
    /// which is why only twenty appear here. Anything else yields nothing, and that is a refusal
    /// the player is told about rather than a silent failure.
    /// </summary>
    public static class HarvestYield
    {
        /// <summary>Templates a species can give up, one of which is picked per successful harvest.</summary>
        public static readonly IReadOnlyDictionary<CreatureFlag, uint[]> BySpecies =
            new Dictionary<CreatureFlag, uint[]>
        {
            // Atta_Mandible, Atta_Ommatidium, Atta_Ovipositor, Atta_Phalanges, Atta_Tarsus, Atta_Tergum
            [CreatureFlag.SpeciesAtta] = new uint[] { 42282, 42283, 42284, 42285, 42286, 42287 },

            // Barb_Tick_Mandible, Barb_Tick_Tarsus
            [CreatureFlag.SpeciesBarbTick] = new uint[] { 42290, 42291 },

            // Beam_Manta_Aerofoil, Beam_Manta_Oculus, Beam_Manta_Tail_Barb
            [CreatureFlag.SpeciesBeamManta] = new uint[] { 42292, 42293, 42294 },

            // Boargar_Tusk, Boargar_Sacrum, Boargar_Ear, Boargar_Spinal_Nodule
            [CreatureFlag.SpeciesBoargar] = new uint[] { 41636, 41639, 42296, 42297 },

            // Caretaker_Insignia, Caretaker_Mask, Caretaker_Sigil
            [CreatureFlag.SpeciesCaretaker] = new uint[] { 42300, 42301, 42302 },

            // Filcher_Cochlea, Filcher_Wing, Filcher_Crest, Filcher_Talon
            [CreatureFlag.SpeciesFilcher] = new uint[] { 41642, 41643, 42313, 42314 },

            // Fithik_Adiabatic_Membrane, Fithik_Ovipositor, Fithik_Claw, Fithik_Mandible, Vestigial_Fithik_Wing
            [CreatureFlag.SpeciesFithik] = new uint[] { 41644, 41645, 42315, 42316, 42378 },

            // Howler_Unguis, Howler_Fang, Howler_Muzzle
            [CreatureFlag.SpeciesHowler] = new uint[] { 41646, 42323, 42324 },

            // Hunter_Eyeplate, Hunter_Insignia
            [CreatureFlag.SpeciesHunter] = new uint[] { 42326, 42327 },

            // Kael_Claw
            [CreatureFlag.SpeciesKael] = new uint[] { 42330 },

            // Light_Bender_Emblem, Light_Bender_Insignia, Light_Bender_Plumule, Light_Bender_Proboscis, Light_Bender_Quill
            [CreatureFlag.SpeciesLightbender] = new uint[] { 42332, 42333, 42334, 42335, 42336 },

            // Machina_Control_Chip
            [CreatureFlag.SpeciesMachina] = new uint[] { 50211 },

            // Miasma_Ganglion, Miasma_Levitator, Miasma_Ichor, Miasma_Protoplasm
            [CreatureFlag.SpeciesMiasma] = new uint[] { 41651, 41652, 42341, 42342 },

            // Thrax_Technician_Emblem, Thrax_Technician_Insignia
            [CreatureFlag.SpeciesTechnician] = new uint[] { 42369, 42370 },

            // Thrax_Medal, Thrax_Skull, Thrax_Barb, Thrax_Emblem, Thrax_Faceplate, Thrax_Incisor, Thrax_Infantry_Insignia, Thrax_Signet, Thrax_Spine_Plate
            [CreatureFlag.SpeciesThrax] = new uint[] { 41665, 41666, 42362, 42363, 42364, 42365, 42366, 42367, 42368 },

            // Treeback_Dendritic_Spine, Treeback_Periderm, Treeback_Proboscis, Treeback_Vibrissa
            [CreatureFlag.SpeciesTreeback] = new uint[] { 41669, 41670, 42374, 42375 },

            // Tree_Lurker_Epidermis, Tree_Lurker_Keratin, Tree_Lurker_Mentum, Tree_Lurker_Oculus
            [CreatureFlag.SpeciesTreelurker] = new uint[] { 41667, 41668, 42372, 42373 },

            // Treemite_Fibrocartilage, Treemite_Zootoxin, Treemite_Carapace, Treemite_Tarsus
            [CreatureFlag.SpeciesTreemite] = new uint[] { 41671, 41672, 42376, 42377 },

            // Warnet_Aerofoil, Warnet_Hemolymph, Warnet_Abdomen, Warnet_Hemolyph, Warnet_Prothorax
            [CreatureFlag.SpeciesWarnet] = new uint[] { 41675, 41676, 42379, 42380, 42381 },

            // Xanx_Brainstem, Xanx_Thorax_Piece, Xanx_Mandible, Xanx_Oculus, Xanx_Tarsus
            [CreatureFlag.SpeciesXanx] = new uint[] { 41677, 41678, 42382, 42383, 42384 },
        };

        /// <summary>
        /// Whether anything is known to come off this species. Checked before the roll, so a
        /// player is not charged ammo and an attempt for a corpse that could never have given
        /// them anything.
        /// </summary>
        public static bool Has(CreatureFlag species) => BySpecies.ContainsKey(species);
    }
}
