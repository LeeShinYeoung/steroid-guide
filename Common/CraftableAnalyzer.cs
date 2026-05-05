using System;
using System.Buffers;
using System.Collections.Generic;
using System.Threading;
using Terraria;
using Terraria.ID;

namespace SteroidGuide.Common
{
    public enum NodeStatus
    {
        Owned,
        Craftable,
        Missing
    }

    public enum RecipeEvaluationMode
    {
        Strict,
        Reachable
    }

    public class RecipeTreeNode
    {
        public int ItemId;
        public int RequiredCount;
        public NodeStatus Status;
        public int OwnedCount;
        public bool IgnoreOwnedForCraftability;
        public Recipe UsedRecipe;
        public List<RecipeTreeNode> Children = new();
        public bool IsDepthLimited;
    }

    public class AnalysisResult
    {
        public HashSet<int> AllCraftable = new();
        public List<int> TopTierItems = new();
        public Dictionary<int, HashSet<int>> VisibleIngredients = new();
        // Items whose ingredient *types* are all available, but quantities fall short of any recipe.
        // ReachableCraftable = relaxed_craftable \ AllCraftable.
        public HashSet<int> ReachableCraftable = new();
        public List<int> ReachableTopTierItems = new();
    }

    public static class CraftableAnalyzer
    {
        public const int InitialDisplayTreeDepthLimit = 10;

        private struct DictSnapshot
        {
            public (int Key, int Value)[] Entries;
            public int Count;

            public DictSnapshot(Dictionary<int, int> dict)
            {
                Entries = ArrayPool<(int, int)>.Shared.Rent(dict.Count);
                Count = 0;
                foreach (var kv in dict)
                    Entries[Count++] = (kv.Key, kv.Value);
            }

            public void Restore(Dictionary<int, int> dict)
            {
                dict.Clear();
                for (int i = 0; i < Count; i++)
                    dict[Entries[i].Key] = Entries[i].Value;
            }

            public void Return()
            {
                if (Entries != null)
                {
                    ArrayPool<(int, int)>.Shared.Return(Entries);
                    Entries = null;
                }
            }
        }

        public static AnalysisResult Analyze(RecipeGraphData graph, Dictionary<int, int> available, CancellationToken ct = default)
        {
            var result = new AnalysisResult();
            // "no recipe" is a graph property independent of quantity, so the cache is shared
            // across the strict and relaxed passes.
            var noRecipeCache = new HashSet<int>();

            var original = new DictSnapshot(available);
            try
            {
                var working = new Dictionary<int, int>(available);

                // Strict pass: builds AllCraftable using exact owned counts.
                foreach (var itemId in graph.RecipesByResult.Keys)
                {
                    ct.ThrowIfCancellationRequested();

                    var visiting = new HashSet<int>();
                    original.Restore(working);

                    var node = TraverseRecipes(itemId, 1, graph, working, visiting,
                        noRecipeCache, consumeAvailable: true, ct, depth: 0,
                        ignoreOwnedForCurrentNode: true);
                    if (node.Status != NodeStatus.Missing)
                        result.AllCraftable.Add(itemId);
                }

                // Relaxed pass: every owned ingredient *type* is treated as infinite supply.
                // Skip items already known strictly-craftable (they cannot be in ReachableCraftable).
                var relaxed = new HashSet<int>();
                foreach (var itemId in graph.RecipesByResult.Keys)
                {
                    ct.ThrowIfCancellationRequested();
                    if (result.AllCraftable.Contains(itemId)) continue;

                    var visiting = new HashSet<int>();
                    original.Restore(working);

                    var node = TraverseRecipes(itemId, 1, graph, working, visiting,
                        noRecipeCache, consumeAvailable: true, ct, depth: 0,
                        ignoreOwnedForCurrentNode: true, ignoreQuantity: true);
                    if (node.Status != NodeStatus.Missing)
                        relaxed.Add(itemId);
                }

                // ReachableCraftable = relaxed \ AllCraftable (already disjoint due to short-circuit).
                foreach (var id in relaxed)
                {
                    result.ReachableCraftable.Add(id);
                }

                // Top-tier filtering universe for partial items: AllCraftable ∪ ReachableCraftable.
                // Rationale: a partial item that is an ingredient of a *strict* craftable should still
                // be hidden, because the user will see the strict parent in All Craftable.
                foreach (var itemId in result.ReachableCraftable)
                {
                    bool isIngredient = false;
                    if (graph.ItemUsedInResults.TryGetValue(itemId, out var resultItems))
                    {
                        foreach (var resultItemId in resultItems)
                        {
                            if (result.AllCraftable.Contains(resultItemId)
                                || result.ReachableCraftable.Contains(resultItemId))
                            {
                                isIngredient = true;
                                break;
                            }
                        }
                    }
                    if (!isIngredient)
                    {
                        result.ReachableTopTierItems.Add(itemId);
                    }
                }
            }
            finally
            {
                original.Return();
            }

            // Filter to top-tier: craftable items not used as ingredient for another craftable item
            foreach (var itemId in result.AllCraftable)
            {
                bool isIngredient = false;
                if (graph.ItemUsedInResults.TryGetValue(itemId, out var resultItems))
                {
                    foreach (var resultItemId in resultItems)
                    {
                        if (result.AllCraftable.Contains(resultItemId))
                        {
                            isIngredient = true;
                            break;
                        }
                    }
                }
                if (!isIngredient)
                {
                    result.TopTierItems.Add(itemId);
                }
            }

            return result;
        }

        public static RecipeTreeNode BuildRecipeTree(int itemId, int needed,
            RecipeGraphData graph, Dictionary<int, int> available, HashSet<int> visiting = null,
            bool ignoreOwnedForCurrentNode = false, CancellationToken ct = default,
            int? maxDisplayDepth = null,
            RecipeEvaluationMode mode = RecipeEvaluationMode.Strict)
        {
            visiting ??= new HashSet<int>();
            return TraverseRecipes(itemId, needed, graph, available, visiting,
                noRecipeCache: null, consumeAvailable: false, ct, depth: 0,
                ignoreOwnedForCurrentNode,
                ignoreQuantity: mode == RecipeEvaluationMode.Reachable,
                maxDisplayDepth: maxDisplayDepth);
        }

        /// <summary>
        /// Unified recursive recipe traversal.
        /// consumeAvailable=true: analysis mode (mutates available, uses noRecipeCache, breaks early on missing).
        /// consumeAvailable=false: display mode (read-only, builds full tree with fallback).
        /// </summary>
        private static RecipeTreeNode TraverseRecipes(
            int itemId, int needed, RecipeGraphData graph,
            Dictionary<int, int> available, HashSet<int> visiting,
            HashSet<int> noRecipeCache,
            bool consumeAvailable,
            CancellationToken ct,
            int depth,
            bool ignoreOwnedForCurrentNode = false,
            bool ignoreQuantity = false,
            int? maxDisplayDepth = null)
        {
            ct.ThrowIfCancellationRequested();

            available.TryGetValue(itemId, out int ownedCount);
            var node = new RecipeTreeNode
            {
                ItemId = itemId,
                RequiredCount = needed,
                OwnedCount = ownedCount,
                IgnoreOwnedForCraftability = ignoreOwnedForCurrentNode
            };

            // Three-way switch:
            //   - Root self-exclusion (analyze a craftable as its own goal): force 0.
            //   - Relaxed: any owned > 0 satisfies the requirement (type-only, infinite quantity).
            //   - Strict: real owned count.
            int usableOwned;
            if (ignoreOwnedForCurrentNode)
                usableOwned = 0;
            else if (ignoreQuantity)
                usableOwned = ownedCount > 0 ? needed : 0;
            else
                usableOwned = ownedCount;

            if (usableOwned >= needed)
            {
                node.Status = NodeStatus.Owned;
                // In relaxed mode the dict represents type-presence with infinite supply, so we
                // must not deduct: future siblings still rely on "type present" semantics.
                if (consumeAvailable && !ignoreQuantity)
                    available[itemId] = ownedCount - needed;
                return node;
            }

            if (noRecipeCache != null && noRecipeCache.Contains(itemId))
            {
                node.Status = NodeStatus.Missing;
                return node;
            }

            if (visiting.Contains(itemId))
            {
                node.Status = NodeStatus.Missing;
                return node;
            }

            if (!graph.RecipesByResult.TryGetValue(itemId, out var recipes))
            {
                noRecipeCache?.Add(itemId);
                node.Status = NodeStatus.Missing;
                return node;
            }

            if (!consumeAvailable && maxDisplayDepth.HasValue && depth >= maxDisplayDepth.Value)
            {
                // Display-only depth cut: do NOT force Status=Missing. A Missing leaf at the
                // depth boundary would propagate up through the parent's success search (which
                // treats any Missing child as a failure) and falsely demote a craftable parent
                // to Missing — corrupting Reachable/Strict consistency past depth 10.
                // Mark via IsDepthLimited only; the parent's success-search must explicitly
                // ignore depth-limited children. Analysis pass (consumeAvailable=true) is
                // guarded above so it never reaches this branch.
                node.Status = NodeStatus.Craftable;
                node.UsedRecipe = recipes.Count > 0 ? recipes[0] : null;
                node.IsDepthLimited = node.UsedRecipe != null;
                return node;
            }

            visiting.Add(itemId);
            int remaining = needed - usableOwned;
            bool foundViable = false;

            foreach (var recipe in recipes)
            {
                // Save state for rollback in analysis mode. Skip under ignoreQuantity:
                // dict mutations are skipped in relaxed mode, so there is nothing to roll back.
                DictSnapshot? saved = consumeAvailable && !ignoreQuantity ? new DictSnapshot(available) : null;
                bool snapshotReturned = false;
                try
                {
                    // Skip the consume-zero write in relaxed mode — the dict's "type present"
                    // semantics must remain stable across siblings.
                    if (consumeAvailable && !ignoreQuantity && usableOwned > 0)
                        available[itemId] = 0;

                    int batchSize = Math.Max(1, recipe.createItem.stack);
                    int batches = (remaining + batchSize - 1) / batchSize;

                    var children = new List<RecipeTreeNode>();
                    bool canMake = true;

                    foreach (var ingredient in recipe.requiredItem)
                    {
                        if (ingredient.type <= ItemID.None)
                            continue;
                        int ingredientNeeded = ingredient.stack * batches;
                        var child = TraverseRecipes(ingredient.type, ingredientNeeded, graph,
                            available, visiting, noRecipeCache, consumeAvailable, ct,
                            depth + 1, ignoreOwnedForCurrentNode: false, ignoreQuantity: ignoreQuantity,
                            maxDisplayDepth: maxDisplayDepth);
                        children.Add(child);
                        // Treat depth-limited leaves as still-viable in display mode: they
                        // represent "we ran out of display depth", not "missing". Without this,
                        // a depth-cut grandchild would falsely demote its parent to Missing
                        // and force the fallback recipe path. Analysis pass is guarded against
                        // depth cuts so IsDepthLimited never appears with consumeAvailable=true.
                        if (child.Status == NodeStatus.Missing && !child.IsDepthLimited)
                        {
                            canMake = false;
                            if (consumeAvailable) break;
                        }
                    }

                    if (canMake)
                    {
                        saved?.Return();
                        snapshotReturned = true;
                        node.Status = NodeStatus.Craftable;
                        node.UsedRecipe = recipe;
                        node.Children = children;
                        foundViable = true;
                        break;
                    }
                    else if (consumeAvailable && saved.HasValue)
                    {
                        saved.Value.Restore(available);
                        saved.Value.Return();
                        snapshotReturned = true;
                    }
                }
                finally
                {
                    if (!snapshotReturned && saved.HasValue)
                    {
                        saved.Value.Return();
                    }
                }
            }

            if (!foundViable)
            {
                node.Status = NodeStatus.Missing;

                // Display mode: show first recipe as fallback
                if (!consumeAvailable && recipes.Count > 0)
                {
                    var fallback = recipes[0];
                    int batchSize = Math.Max(1, fallback.createItem.stack);
                    int batches = (remaining + batchSize - 1) / batchSize;

                    node.UsedRecipe = fallback;
                    node.Children.Clear();
                    foreach (var ingredient in fallback.requiredItem)
                    {
                        if (ingredient.type <= ItemID.None)
                            continue;
                        int ingredientNeeded = ingredient.stack * batches;
                        node.Children.Add(TraverseRecipes(ingredient.type, ingredientNeeded, graph,
                            available, visiting, noRecipeCache, consumeAvailable, ct,
                            depth + 1, ignoreOwnedForCurrentNode: false, ignoreQuantity: ignoreQuantity,
                            maxDisplayDepth: maxDisplayDepth));
                    }
                }
            }

            visiting.Remove(itemId);
            return node;
        }

    }
}
