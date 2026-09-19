using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Rasa.Services.Preloader
{

    using Structures.World;

    /// <summary>
    /// The client's skillCharacter table, row for row: 73 skills, the class that grants each and
    /// the level it takes. Ordered by class and then by skill id so the tiers read down the file.
    /// </summary>
    public class SkillCharacterPreloader : PreloaderBase, IPreloader
    {
        public void Preload(MigrationBuilder migrationBuilder)
        {
            Insert(migrationBuilder, SkillCharacterEntry.TableName, typeof(SkillCharacterEntry));
        }

        protected override IEnumerable<object[]> GetRows()
        {

            // Recruit - tier I, level 1
            yield return new object[] { 1, 1, 1 };   // T1_RECRUIT_FIREARMS
            yield return new object[] { 8, 1, 1 };   // T1_RECRUIT_HAND_TO_HAND
            yield return new object[] { 19, 1, 1 };   // T1_RECRUIT_MOTOR_ASSIST_ARMOR
            yield return new object[] { 49, 1, 1 };   // T1_RECRUIT_LIGHTNING
            yield return new object[] { 165, 1, 1 };   // T1_RECRUIT_SPRINT

            // Soldier - tier II, level 5
            yield return new object[] { 21, 2, 5 };   // T2_SOLDIER_REFLECTIVE_ARMOR
            yield return new object[] { 22, 2, 5 };   // T2_SOLDIER_MACHINE_GUN
            yield return new object[] { 25, 2, 5 };   // T2_SOLDIER_SHRAPNEL
            yield return new object[] { 147, 2, 5 };   // T2_SOLDIER_RAGE

            // Specialist - tier II, level 5
            yield return new object[] { 14, 3, 5 };   // T2_SPECIALIST_TOOLS
            yield return new object[] { 30, 3, 5 };   // T2_SPECIALIST_HAZMAT_ARMOR
            yield return new object[] { 31, 3, 5 };   // T2_SPECIALIST_LEECH_GUN
            yield return new object[] { 36, 3, 5 };   // T2_SPECIALIST_RUIN

            // Commando - tier III, level 15
            yield return new object[] { 24, 4, 15 };   // T3_COMMANDO_LAUNCHERS
            yield return new object[] { 28, 4, 15 };   // T3_COMMANDO_FORCE_BLAST
            yield return new object[] { 39, 4, 15 };   // T3_COMMANDO_GRAVITON_ARMOR
            yield return new object[] { 77, 4, 15 };   // T3_COMMANDO_RUSHING_BLOW
            yield return new object[] { 164, 4, 15 };   // T3_COMMANDO_SCOURGE

            // Ranger - tier III, level 15
            yield return new object[] { 48, 5, 15 };   // T3_RANGER_STEALTH_ARMOR
            yield return new object[] { 54, 5, 15 };   // T3_RANGER_TACTICAL_EVASION
            yield return new object[] { 55, 5, 15 };   // T3_RANGER_NET_GUN
            yield return new object[] { 162, 5, 15 };   // T3_RANGER_SPOTTER
            yield return new object[] { 163, 5, 15 };   // T3_RANGER_FIRE_SUPPORT

            // Sapper - tier III, level 15
            yield return new object[] { 57, 6, 15 };   // T3_SAPPER_MECH_ARMOR
            yield return new object[] { 58, 6, 15 };   // T3_SAPPER_POLARITY_GUN
            yield return new object[] { 111, 6, 15 };   // T3_SAPPER_CRAB_MINES
            yield return new object[] { 160, 6, 15 };   // T3_SAPPER_HACK
            yield return new object[] { 174, 6, 15 };   // T3_SAPPER_SHIELD_EXTENDER

            // Biotechnician - tier III, level 15
            yield return new object[] { 34, 7, 15 };   // T3_BIOTECHNICIAN_CURE
            yield return new object[] { 35, 7, 15 };   // T3_BIOTECHNICIAN_RECONSTRUCTION
            yield return new object[] { 66, 7, 15 };   // T3_BIOTECHNICIAN_BIO_ARMOR
            yield return new object[] { 67, 7, 15 };   // T3_BIOTECHNICIAN_INJECTION_GUN
            yield return new object[] { 173, 7, 15 };   // T3_BIOTECHNICIAN_BIO_AUGMENTATION

            // Grenadier - tier IV, level 30
            yield return new object[] { 40, 8, 30 };   // T4_GRENADIER_PROPELLANT_GUN
            yield return new object[] { 47, 8, 30 };   // T4_GRENADIER_SIG_CONCUSSIVE_WAVE
            yield return new object[] { 79, 8, 30 };   // T4_GRENADIER_SCATTERBOMBS
            yield return new object[] { 80, 8, 30 };   // T4_GRENADIER_TECTONIC_STRIKE
            yield return new object[] { 155, 8, 30 };   // T4_GRENADIER_SACRIFICE

            // Guardian - tier IV, level 30
            yield return new object[] { 23, 9, 30 };   // T4_GUARDIAN_STAFF
            yield return new object[] { 26, 9, 30 };   // T4_GUARDIAN_REFLECTION
            yield return new object[] { 43, 9, 30 };   // T4_GUARDIAN_CONVERSION
            yield return new object[] { 89, 9, 30 };   // T4_GUARDIAN_VORTEX
            yield return new object[] { 92, 9, 30 };   // T4_GUARDIAN_SIG_SHIELD_WAVE

            // Sniper - tier IV, level 30
            yield return new object[] { 50, 10, 30 };   // T4_SNIPER_TORQUE_SHELL_RIFLE
            yield return new object[] { 149, 10, 30 };   // T4_SNIPER_SIG_CRIT_WAVE
            yield return new object[] { 150, 10, 30 };   // T4_SNIPER_SHREDDER_AMMO
            yield return new object[] { 151, 10, 30 };   // T4_SNIPER_TARGET_PAINTING
            yield return new object[] { 166, 10, 30 };   // T4_SNIPER_CALLED_SHOT

            // Spy - tier IV, level 30
            yield return new object[] { 82, 11, 30 };   // T4_SPY_BLADE
            yield return new object[] { 102, 11, 30 };   // T4_SPY_POLYMORPH
            yield return new object[] { 110, 11, 30 };   // T4_SPY_SIG_CLOAK_WAVE
            yield return new object[] { 148, 11, 30 };   // T4_SPY_TRAITOR
            yield return new object[] { 161, 11, 30 };   // T4_SPY_POLARITY_FIELD

            // Demolitionist - tier IV, level 30
            yield return new object[] { 20, 12, 30 };   // T4_DEMOLITIONIST_SIG_EXPLOSIVE_WAVE
            yield return new object[] { 63, 12, 30 };   // T4_DEMOLITIONIST_REALITY_RIPPER
            yield return new object[] { 113, 12, 30 };   // T4_DEMOLITIONIST_CONTROLLED_FISSION
            yield return new object[] { 114, 12, 30 };   // T4_DEMOLITIONIST_SELF_DESTRUCT
            yield return new object[] { 159, 12, 30 };   // T4_DEMOLITIONIST_EXPLOSIVE_NANITES

            // Engineer - tier IV, level 30
            yield return new object[] { 32, 13, 30 };   // T4_ENGINEER_TURRET
            yield return new object[] { 121, 13, 30 };   // T4_ENGINEER_FEEDBACK
            yield return new object[] { 157, 13, 30 };   // T4_ENGINEER_SIG_BASE_WAVE
            yield return new object[] { 158, 13, 30 };   // T4_ENGINEER_TRAP
            yield return new object[] { 172, 13, 30 };   // T4_ENGINEER_BOT_CONSTRUCTION

            // Medic - tier IV, level 30
            yield return new object[] { 37, 14, 30 };   // T4_MEDIC_VIRAL_CONVERSION
            yield return new object[] { 135, 14, 30 };   // T4_MEDIC_DISEASE
            yield return new object[] { 152, 14, 30 };   // T4_MEDIC_MIND_CONTROL
            yield return new object[] { 153, 14, 30 };   // T4_MEDIC_RESISTANCE
            yield return new object[] { 154, 14, 30 };   // T4_MEDIC_SIG_REGENERATION_WAVE

            // Exobiologist - tier IV, level 30
            yield return new object[] { 68, 15, 30 };   // T4_EXOBIOLOGIST_HORTIMUNCULUS
            yield return new object[] { 72, 15, 30 };   // T4_EXOBIOLOGIST_CADAVER_IMMOLATION
            yield return new object[] { 73, 15, 30 };   // T4_EXOBIOLOGIST_REANIMATION
            yield return new object[] { 136, 15, 30 };   // T4_EXOBIOLOGIST_CREATE_CLONE
            yield return new object[] { 156, 15, 30 };   // T4_EXOBIOLOGIST_SIG_REANIMATION_WAVE
        }
    }
}
