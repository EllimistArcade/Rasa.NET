using System.Collections.Generic;

namespace Rasa.Data
{
    /// <summary>
    /// The fifteen character classes, from generated.client.characterclass.
    ///
    /// They form a binary tree four deep - one Recruit, two at tier 2, four at tier 3, eight at
    /// tier 4 - and the ids run in tier order, which is why <see cref="Parent"/> reads as a
    /// simple table rather than a query.
    /// </summary>
    public enum CharacterClass : uint
    {
        None = 0,

        Recruit = 1,

        Soldier = 2,
        Specialist = 3,

        Commando = 4,
        Ranger = 5,
        Sapper = 6,
        Biotechnician = 7,

        Grenadier = 8,
        Guardian = 9,
        Sniper = 10,
        Spy = 11,
        Demolitionist = 12,
        Engineer = 13,
        Medic = 14,
        Exobiologist = 15
    }

    /// <summary>
    /// The class tree, and the two questions asked of it.
    ///
    /// This is <c>client/gameuiutil.py</c>'s <c>g_ClassTree</c>, whose rows are
    /// <c>(name, parent, child, child)</c>. Only the parent link is kept here: the children are
    /// derivable from it, and keeping one direction means the two cannot disagree.
    ///
    /// The tiers are not guessed. Every skill in the client's <c>skillCharacter</c> table carries
    /// the class that grants it and the level it needs, and grouping those levels by depth in
    /// this tree gives 1, 5, 15 and 30 with no row disagreeing - across all 73 skills, and
    /// agreeing in turn with the T1/T2/T3/T4 prefix each skill's own name carries. The clone
    /// credits granted at 5, 15 and 30 are the same three numbers from a third direction: one
    /// credit per branch point, so a player can come back and take the other side.
    /// </summary>
    public static class CharacterClassTree
    {
        private static readonly CharacterClass[] Parents =
        {
            CharacterClass.None,            // 0  - not a class
            CharacterClass.None,            // 1  Recruit, the root
            CharacterClass.Recruit,         // 2  Soldier
            CharacterClass.Recruit,         // 3  Specialist
            CharacterClass.Soldier,         // 4  Commando
            CharacterClass.Soldier,         // 5  Ranger
            CharacterClass.Specialist,      // 6  Sapper
            CharacterClass.Specialist,      // 7  Biotechnician
            CharacterClass.Commando,        // 8  Grenadier
            CharacterClass.Commando,        // 9  Guardian
            CharacterClass.Ranger,          // 10 Sniper
            CharacterClass.Ranger,          // 11 Spy
            CharacterClass.Sapper,          // 12 Demolitionist
            CharacterClass.Sapper,          // 13 Engineer
            CharacterClass.Biotechnician,   // 14 Medic
            CharacterClass.Biotechnician    // 15 Exobiologist
        };

        /// <summary>The level a class may first be trained in, by tier.</summary>
        private static readonly int[] TierLevels = { 0, 1, 5, 15, 30 };

        public static bool Exists(CharacterClass characterClass)
        {
            return characterClass > CharacterClass.None && (int)characterClass < Parents.Length;
        }

        /// <summary>The class this one advanced from, or None for Recruit and for a bad id.</summary>
        public static CharacterClass Parent(CharacterClass characterClass)
        {
            return Exists(characterClass) ? Parents[(int)characterClass] : CharacterClass.None;
        }

        /// <summary>
        /// gameuiutil's <c>IsCharacterClass(lowerClass, higherClass)</c>: whether a player of
        /// <paramref name="playerClass"/> counts as a member of <paramref name="ofClass"/>,
        /// which is true for their own class and for every class on the way up to Recruit.
        ///
        /// This is the rule the skills window draws with - it shows the spend buttons only when
        /// the skill's class answers true here - and it is why a Commando may still train
        /// Soldier and Recruit skills but never a Ranger's.
        /// </summary>
        public static bool Is(CharacterClass playerClass, CharacterClass ofClass)
        {
            if (!Exists(playerClass) || !Exists(ofClass))
                return false;

            // Bounded by the number of classes rather than run until it reaches the root. The
            // table below is a constant and cannot cycle - but a walk that trusts it to terminate
            // turns a one-line typo in it into the world loop hanging, which is a bad way to find
            // out. Every honest chain is at most four long.
            var c = playerClass;

            for (var steps = 0; steps < Parents.Length && c != CharacterClass.None; steps++, c = Parent(c))
                if (c == ofClass)
                    return true;

            return false;
        }

        /// <summary>1 for Recruit, 4 for the eight leaves. 0 for anything that is not a class.</summary>
        public static int TierOf(CharacterClass characterClass)
        {
            if (!Exists(characterClass))
                return 0;

            var tier = 1;
            var c = characterClass;

            // Bounded for the same reason Is() is, and bounded at the depth the tree actually
            // has: a chain still climbing past tier 4 is a cycle or a wrong parent, and it has
            // no tier to report. Returning 0 rather than a larger number is what keeps
            // LevelFor's lookup below inside its four-entry table.
            while (Parent(c) != CharacterClass.None)
            {
                if (tier >= TierLevels.Length - 1)
                    return 0;

                c = Parent(c);
                tier++;
            }

            return tier;
        }

        /// <summary>The level at which this class's tier opens: 1, 5, 15 or 30.</summary>
        public static int LevelFor(CharacterClass characterClass)
        {
            var tier = TierOf(characterClass);

            return tier == 0 ? 0 : TierLevels[tier];
        }

        /// <summary>
        /// gameuiutil's <c>IsTrainableClass(currentClassId, classId)</c>: whether a character of
        /// <paramref name="playerClass"/> may advance into <paramref name="into"/>.
        ///
        /// The client's rule is one line - the target's parent has to be the class you are now -
        /// so advancement is one step at a time and there is no skipping a tier. A Recruit picks
        /// Soldier or Specialist and nothing else, whatever their level.
        ///
        /// Note this is not <see cref="Is"/>. That one walks the whole line upward and answers
        /// what a character already counts as; this one looks at a single link and answers where
        /// they may go next.
        /// </summary>
        public static bool CanAdvanceTo(CharacterClass playerClass, CharacterClass into)
        {
            return Exists(playerClass) && Exists(into) && Parent(into) == playerClass;
        }

        /// <summary>
        /// The classes a character of this class and level may advance into: the direct children,
        /// once their tier's level is reached. Empty at tier 4, which is the end of the tree.
        /// </summary>
        public static List<CharacterClass> AdvancementsFor(CharacterClass playerClass, int level)
        {
            var available = new List<CharacterClass>();

            if (!Exists(playerClass))
                return available;

            for (var id = 1; id < Parents.Length; id++)
            {
                var candidate = (CharacterClass)id;

                if (CanAdvanceTo(playerClass, candidate) && level >= LevelFor(candidate))
                    available.Add(candidate);
            }

            return available;
        }
    }
}
