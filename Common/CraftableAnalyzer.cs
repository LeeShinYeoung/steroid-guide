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

    public class RecipeTreeNode
    {
        public int ItemId;
        public int RequiredCount;
        public NodeStatus Status;
        public int OwnedCount;
        public bool IgnoreOwnedForCraftability;
        public Recipe UsedRecipe;
        public List<RecipeTreeNode> Children = new();
    }

    public class AnalysisResult
    {
        public HashSet<int> AllCraftable = new();
        public List<int> TopTierItems = new();
    }

    public static class CraftableAnalyzer
    {
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
            var noRecipeCache = new HashSet<int>();

            var original = new DictSnapshot(available);
            try
            {
                var working = new Dictionary<int, int>(available);

                foreach (var itemId in graph.RecipesByResult.Keys)
                {
                    ct.ThrowIfCancellationRequested();

                    var visiting = new HashSet<int>();
                    original.Restore(working);

                    var node = TraverseRecipes(itemId, 1, graph, working, visiting,
                        noRecipeCache, consumeAvailable: true, ct, ignoreOwnedForCurrentNode: true);
                    if (node.Status != NodeStatus.Missing)
                    {
                        result.AllCraftable.Add(itemId);
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
            bool ignoreOwnedForCurrentNode = false)
        {
            visiting ??= new HashSet<int>();
            return TraverseRecipes(itemId, needed, graph, available, visiting,
                noRecipeCache: null, consumeAvailable: false, ct: default, ignoreOwnedForCurrentNode);
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
            bool ignoreOwnedForCurrentNode = false)
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

            int usableOwned = ignoreOwnedForCurrentNode ? 0 : ownedCount;

            if (usableOwned >= needed)
            {
                node.Status = NodeStatus.Owned;
                if (consumeAvailable)
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

            visiting.Add(itemId);
            int remaining = needed - usableOwned;
            bool foundViable = false;

            foreach (var recipe in recipes)
            {
                // Save state for rollback in analysis mode
                DictSnapshot? saved = consumeAvailable ? new DictSnapshot(available) : null;
                bool snapshotReturned = false;
                try
                {
                    if (consumeAvailable && usableOwned > 0)
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
                            available, visiting, noRecipeCache, consumeAvailable, ct);
                        children.Add(child);
                        if (child.Status == NodeStatus.Missing)
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
                            available, visiting, noRecipeCache, consumeAvailable, ct));
                    }
                }
            }

            visiting.Remove(itemId);
            return node;
        }
    }
}
