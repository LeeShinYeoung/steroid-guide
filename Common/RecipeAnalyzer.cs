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
                if (CanCraftRootItem(itemId, 1, graph, availableCopy, visiting, noRecipeCache))
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

        private static bool CanCraftRootItem(int itemId, int needed, RecipeGraphData graph,
            Dictionary<int, int> available, HashSet<int> visiting, HashSet<int> noRecipeCache)
        {
            return CanCraft(itemId, needed, graph, available, visiting, noRecipeCache, ignoreOwnedForCurrentNode: true);
        }

        private static bool CanCraft(int itemId, int needed, RecipeGraphData graph,
            Dictionary<int, int> available, HashSet<int> visiting, HashSet<int> noRecipeCache,
            bool ignoreOwnedForCurrentNode = false)
        {
            available.TryGetValue(itemId, out int owned);
            int usableOwned = ignoreOwnedForCurrentNode ? 0 : owned;

            if (usableOwned >= needed)
            {
                // Consume from available to prevent double-counting
                available[itemId] = owned - needed;
                return true;
            }

            // Memoization: skip items known to have no recipe
            if (noRecipeCache.Contains(itemId))
                return false;

            if (visiting.Contains(itemId))
                return false;

            if (!graph.RecipesByResult.TryGetValue(itemId, out var recipes))
            {
                noRecipeCache.Add(itemId);
                return false;
            }

            visiting.Add(itemId);
            bool result = false;
            int remaining = needed - usableOwned;

            foreach (var recipe in recipes)
            {
                // Save state for rollback if this recipe path fails
                var saved = new Dictionary<int, int>(available);

                // Consume the owned portion first
                if (usableOwned > 0)
                    available[itemId] = 0;

                int batchSize = Math.Max(1, recipe.createItem.stack);
                int batches = (remaining + batchSize - 1) / batchSize;

                bool allIngredients = true;
                foreach (var ingredient in recipe.requiredItem)
                {
                    if (ingredient.type <= ItemID.None)
                        continue;
                    int ingredientNeeded = ingredient.stack * batches;
                    if (!CanCraft(ingredient.type, ingredientNeeded, graph, available, visiting, noRecipeCache))
                    {
                        allIngredients = false;
                        break;
                    }
                }

                if (allIngredients)
                {
                    result = true;
                    break;
                }
                else
                {
                    // Rollback: restore available to saved state
                    available.Clear();
                    foreach (var kv in saved)
                        available[kv.Key] = kv.Value;
                }
            }

            visiting.Remove(itemId);
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

            available.TryGetValue(itemId, out int ownedCount);
            var node = new RecipeTreeNode
            {
                ItemId = itemId,
                RequiredCount = needed,
                OwnedCount = ownedCount,
                IgnoreOwnedForCraftability = ignoreOwnedForCurrentNode
            };

            int usableOwnedCount = ignoreOwnedForCurrentNode ? 0 : ownedCount;

            if (usableOwnedCount >= needed)
            {
                node.Status = NodeStatus.Owned;
                return node;
            }

            if (visiting.Contains(itemId))
            {
                node.Status = NodeStatus.Missing;
                return node;
            }

            visiting.Add(itemId);

            if (graph.RecipesByResult.TryGetValue(itemId, out var recipes))
            {
                int remaining = needed - usableOwnedCount;
                bool foundViable = false;

                foreach (var recipe in recipes)
                {
                    int batchSize = Math.Max(1, recipe.createItem.stack);
                    int batches = (remaining + batchSize - 1) / batchSize;

                    var children = new List<RecipeTreeNode>();
                    bool canMake = true;

                    foreach (var ingredient in recipe.requiredItem)
                    {
                        if (ingredient.type <= ItemID.None)
                            continue;
                        int ingredientNeeded = ingredient.stack * batches;
                        var child = BuildRecipeTree(ingredient.type, ingredientNeeded, graph, available, visiting);
                        children.Add(child);
                        if (child.Status == NodeStatus.Missing)
                            canMake = false;
                    }

                    if (canMake && !foundViable)
                    {
                        node.Status = NodeStatus.Craftable;
                        node.UsedRecipe = recipe;
                        node.Children = children;
                        foundViable = true;
                    }
                }

                if (!foundViable && recipes.Count > 0)
                {
                    // No viable recipe — show first recipe for display
                    var fallback = recipes[0];
                    int batchSize = Math.Max(1, fallback.createItem.stack);
                    int batches = (remaining + batchSize - 1) / batchSize;

                    node.Status = NodeStatus.Missing;
                    node.UsedRecipe = fallback;
                    node.Children.Clear();
                    foreach (var ingredient in fallback.requiredItem)
                    {
                        if (ingredient.type <= ItemID.None)
                            continue;
                        int ingredientNeeded = ingredient.stack * batches;
                        node.Children.Add(BuildRecipeTree(ingredient.type, ingredientNeeded, graph, available, visiting));
                    }
                }
            }
            else if (ownedCount < needed)
            {
                node.Status = NodeStatus.Missing;
            }

            visiting.Remove(itemId);
            return node;
        }
    }
}
