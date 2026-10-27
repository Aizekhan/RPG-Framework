using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RPGFramework.Skills;

namespace RPGFramework.Tools.SkillEditor
{
    /// <summary>
    /// A skill tree made in the inspector (e.g. "Warrior"): a list of <see cref="SkillAsset"/>s.
    /// <see cref="ToSkillTree"/> turns it into the <see cref="SkillTree"/> a <see cref="SkillManager"/> uses;
    /// <see cref="Validate"/> finds what would make a skill impossible to learn.
    /// </summary>
    [CreateAssetMenu(fileName = "NewSkillTree", menuName = "RPG Framework/Skill Tree", order = 106)]
    public class SkillTreeAsset : ScriptableObject
    {
        [Header("Tree Identity")]
        [Tooltip("Unique id the game uses for this tree (e.g. 'warrior')")]
        public string treeId = "tree_new";

        [Tooltip("Name shown to players")]
        public string treeName = "New Skill Tree";

        [Tooltip("What this tree is about, shown to players")]
        [TextArea(2, 4)]
        public string description = "";

        [Header("Skills")]
        [Tooltip("Every skill in this tree; prerequisites must be skills from this list")]
        public List<SkillAsset> skills = new List<SkillAsset>();

        /// <summary>
        /// Adds every skill of this tree to <paramref name="tree"/>. Empty slots are skipped; a skill whose id is
        /// already in the tree is ignored by the tree (see <see cref="Validate"/>).
        /// </summary>
        public void RegisterInto(SkillTree tree)
        {
            if (tree == null || skills == null)
                return;

            foreach (var skill in skills)
            {
                if (skill != null)
                    tree.AddSkill(skill.ToSkill());
            }
        }

        /// <summary>
        /// A new <see cref="SkillTree"/> holding every skill of this tree.
        /// </summary>
        public SkillTree ToSkillTree()
        {
            var tree = new SkillTree(treeId, treeName, description);
            RegisterInto(tree);
            return tree;
        }

        /// <summary>
        /// Problems with the tree: missing id, no skills, empty slots, duplicate skill ids, each skill's own problems,
        /// prerequisites that are not in this tree, and prerequisite loops (A needs B needs A), which make every skill
        /// in or after the loop impossible to learn. An empty list means the tree is ready.
        /// </summary>
        public List<string> Validate()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(treeId))
                errors.Add("Tree ID cannot be empty");

            if (skills == null || skills.Count == 0)
            {
                errors.Add("Tree has no skills");
                return errors;
            }

            int emptySlots = skills.Count(s => s == null);
            if (emptySlots > 0)
                errors.Add($"{emptySlots} empty skill slot(s) in the tree");

            var present = skills.Where(s => s != null).ToList();

            var duplicateIds = present
                .Where(s => !string.IsNullOrWhiteSpace(s.skillId))
                .GroupBy(s => s.skillId)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key);
            foreach (var id in duplicateIds)
                errors.Add($"Duplicate skill ID: {id} (only the first one is used)");

            foreach (var skill in present.Distinct())
            {
                foreach (var error in skill.Validate())
                    errors.Add($"{Label(skill)}: {error}");
            }

            var ids = new HashSet<string>(present.Select(s => s.skillId).Where(id => !string.IsNullOrWhiteSpace(id)));
            foreach (var skill in present.Distinct())
            {
                if (skill.prerequisites == null)
                    continue;
                foreach (var entry in skill.prerequisites)
                {
                    if (entry == null || entry.skill == null || entry.skill == skill)
                        continue;
                    if (!ids.Contains(entry.skill.skillId))
                        errors.Add($"{Label(skill)}: prerequisite '{entry.skill.skillId}' is not in this tree, " +
                                   "so it can never be learned here");
                }
            }

            var depths = Depths(present);
            foreach (var skill in present.Distinct())
            {
                if (depths[skill] < 0)
                    errors.Add($"{Label(skill)}: its prerequisites lead into a loop (A needs B needs A), " +
                               "so it can never be learned");
            }

            return errors;
        }

        /// <summary>
        /// The tree's skills by tier: tier 0 holds the skills with no prerequisites in the tree, and each later tier
        /// holds skills whose prerequisites are all in earlier tiers. Skills caught in a prerequisite loop are left out.
        /// </summary>
        public List<List<SkillAsset>> GetTiers()
        {
            var tiers = new List<List<SkillAsset>>();
            if (skills == null)
                return tiers;

            var present = skills.Where(s => s != null).Distinct().ToList();
            var depths = Depths(present);
            foreach (var skill in present)
            {
                int depth = depths[skill];
                if (depth < 0)
                    continue;
                while (tiers.Count <= depth)
                    tiers.Add(new List<SkillAsset>());
                tiers[depth].Add(skill);
            }

            return tiers;
        }

        private static string Label(SkillAsset skill)
        {
            return string.IsNullOrWhiteSpace(skill.skillId) ? $"Skill '{skill.name}'" : $"Skill '{skill.skillId}'";
        }

        /// <summary>
        /// Each skill's tier (see <see cref="GetTiers"/>), or -1 when its prerequisites lead into a loop.
        /// Prerequisites are matched by id, as the runtime does; ones not in the tree are ignored here.
        /// </summary>
        private static Dictionary<SkillAsset, int> Depths(List<SkillAsset> present)
        {
            var byId = new Dictionary<string, SkillAsset>();
            foreach (var skill in present)
            {
                if (!string.IsNullOrWhiteSpace(skill.skillId) && !byId.ContainsKey(skill.skillId))
                    byId[skill.skillId] = skill;
            }

            var known = new Dictionary<SkillAsset, int>();
            foreach (var skill in present)
                Depth(skill, byId, known, new HashSet<SkillAsset>());
            return known;
        }

        /// <summary>
        /// The skill's tier, walking each skill once (results are kept in <paramref name="known"/>); -1 when following
        /// its prerequisites comes back to a skill already on the path.
        /// </summary>
        private static int Depth(SkillAsset skill, Dictionary<string, SkillAsset> byId,
            Dictionary<SkillAsset, int> known, HashSet<SkillAsset> onPath)
        {
            if (known.TryGetValue(skill, out int result))
                return result;
            if (!onPath.Add(skill))
                return -1;

            result = 0;
            if (skill.prerequisites != null)
            {
                foreach (var entry in skill.prerequisites)
                {
                    if (entry == null || entry.skill == null)
                        continue;

                    // A skill pointing at itself (by reference or id) is a loop of one
                    SkillAsset prerequisite = entry.skill == skill ? skill : null;
                    if (prerequisite == null && !string.IsNullOrWhiteSpace(entry.skill.skillId))
                        byId.TryGetValue(entry.skill.skillId, out prerequisite);
                    if (prerequisite == null)
                        continue;

                    int depth = Depth(prerequisite, byId, known, onPath);
                    if (depth < 0)
                    {
                        result = -1;
                        break;
                    }
                    result = Mathf.Max(result, depth + 1);
                }
            }

            onPath.Remove(skill);
            known[skill] = result;
            return result;
        }
    }
}
