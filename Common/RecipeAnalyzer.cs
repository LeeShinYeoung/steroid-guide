using System;
using System.Collections.Generic;
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

    public static class RecipeAnalyzer
    {
        public static AnalysisResult Analyze(RecipeGraphData graph, Dictionary<int, int> available)
        {
            var result = new AnalysisResult();
            var noRecipeCache = new HashSet<int>();

            foreach (var itemId in graph.RecipesByResult.Keys)
            {
                var visiting = new HashSet<int>();
                // Each top-level item gets its own copy so items are independently evaluated
                var availableCopy = new Dictionary<int, int>(available);
                var node = TraverseRecipes(itemId, 1, graph, availableCopy, visiting,
                    noRecipeCache, consumeAvailable: true, ignoreOwnedForCurrentNode: true);
                if (node.Status != NodeStatus.Missing)
                {
                    result.AllCraftable.Add(itemId);
                }
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

        public static int GetRecipeDepth(int itemId, RecipeGraphData graph, HashSet<int> visiting = null)
        {
            visiting ??= new HashSet<int>();

            if (visiting.Contains(itemId))
                return 0;

            if (!graph.RecipesByResult.TryGetValue(itemId, out var recipes))
                return 0;

            visiting.Add(itemId);
            int maxDepth = 0;

            foreach (var recipe in recipes)
            {
                int recipeMax = 0;
                foreach (var ingredient in recipe.requiredItem)
                {
                    if (ingredient.type <= ItemID.None)
                        continue;
                    int childDepth = GetRecipeDepth(ingredient.type, graph, visiting);
                    if (childDepth > recipeMax)
                        recipeMax = childDepth;
                }
                if (recipeMax > maxDepth)
                    maxDepth = recipeMax;
            }

            visiting.Remove(itemId);
            return maxDepth + 1;
        }

        public static RecipeTreeNode BuildRecipeTree(int itemId, int needed,
            RecipeGraphData graph, Dictionary<int, int> available, HashSet<int> visiting = null,
            bool ignoreOwnedForCurrentNode = false)
        {
            visiting ??= new HashSet<int>();
            return TraverseRecipes(itemId, needed, graph, available, visiting,
                noRecipeCache: null, consumeAvailable: false, ignoreOwnedForCurrentNode);
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
            bool ignoreOwnedForCurrentNode = false)
        {
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
                var saved = consumeAvailable ? new Dictionary<int, int>(available) : null;

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
                        available, visiting, noRecipeCache, consumeAvailable);
                    children.Add(child);
                    if (child.Status == NodeStatus.Missing)
                    {
                        canMake = false;
                        if (consumeAvailable) break;
                    }
                }

                if (canMake)
                {
                    node.Status = NodeStatus.Craftable;
                    node.UsedRecipe = recipe;
                    node.Children = children;
                    foundViable = true;
                    break;
                }
                else if (consumeAvailable && saved != null)
                {
                    // Rollback: restore available to saved state
                    available.Clear();
                    foreach (var kv in saved)
                        available[kv.Key] = kv.Value;
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
                            available, visiting, noRecipeCache, consumeAvailable));
                    }
                }
            }

            visiting.Remove(itemId);
            return node;
        }
    }
}
