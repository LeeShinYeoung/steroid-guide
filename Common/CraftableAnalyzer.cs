using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

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

            // Phase timers for [Perf-Analyze] breakdown. Local Stopwatch — never persisted.
            long strictMs = 0, relaxedMs = 0, topTierMs = 0;
            var phaseSw = new Stopwatch();

            var original = new DictSnapshot(available);
            try
            {
                var working = new Dictionary<int, int>(available);

                // Strict pass: builds AllCraftable using exact owned counts.
                phaseSw.Restart();
                foreach (var itemId in graph.RecipesByResult.Keys)
                {
                    ct.ThrowIfCancellationRequested();

                    var visiting = new HashSet<int>();
                    original.Restore(working);

                    var node = TraverseRecipes(itemId, 1, graph, working, visiting,
                        noRecipeCache, consumeAvailable: true, ct, depth: 0,
                        out _,
                        ignoreOwnedForCurrentNode: true);
                    if (node.Status != NodeStatus.Missing)
                        result.AllCraftable.Add(itemId);
                }
                strictMs = phaseSw.ElapsedMilliseconds;

                // Relaxed pass: every owned ingredient *type* is treated as infinite supply.
                // Skip items already known strictly-craftable (they cannot be in ReachableCraftable).
                //
                // Relaxed memoization (Tier 1): the relaxed pass never mutates `working` (line 242
                // gate on `!ignoreQuantity` and line 295 likewise). With `working` restored to
                // `original` before each top-level call AND no in-call mutation, the function
                // becomes a pure mapping (itemId, ignoreOwnedForCurrentNode) → craftable-or-not
                // *for this Analyze invocation*. We therefore memoize relaxed results in two
                // dictionaries — one for root entries (ignoreOwnedForCurrentNode=true, owned
                // self-supply forced to 0) and one for recursive child entries (false).
                //
                // Caches are scoped to this method — they are GC'd when Analyze returns. Strict
                // pass passes null (caching strict results would be unsound: strict pass DOES
                // mutate `working` mid-recursion). Cycle-hit (visiting) results are NEVER stored
                // because they depend on the live ancestor set, not the item alone.
                var relaxedCacheRoot = new Dictionary<int, bool>();
                var relaxedCacheChild = new Dictionary<int, bool>();

                phaseSw.Restart();
                var relaxed = new HashSet<int>();
                foreach (var itemId in graph.RecipesByResult.Keys)
                {
                    ct.ThrowIfCancellationRequested();
                    if (result.AllCraftable.Contains(itemId)) continue;

                    var visiting = new HashSet<int>();
                    original.Restore(working);

                    var node = TraverseRecipes(itemId, 1, graph, working, visiting,
                        noRecipeCache, consumeAvailable: true, ct, depth: 0,
                        out _,
                        ignoreOwnedForCurrentNode: true, ignoreQuantity: true,
                        relaxedCacheRoot: relaxedCacheRoot,
                        relaxedCacheChild: relaxedCacheChild);
                    if (node.Status != NodeStatus.Missing)
                        relaxed.Add(itemId);
                }
                relaxedMs = phaseSw.ElapsedMilliseconds;

                // ReachableCraftable = relaxed \ AllCraftable (already disjoint due to short-circuit).
                foreach (var id in relaxed)
                {
                    result.ReachableCraftable.Add(id);
                }

                // Top-tier filtering universe for partial items: AllCraftable ∪ ReachableCraftable.
                // Rationale: a partial item that is an ingredient of a *strict* craftable should still
                // be hidden, because the user will see the strict parent in All Craftable.
                phaseSw.Restart();
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

                // Filter to top-tier: craftable items not used as ingredient for another craftable item.
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
                topTierMs = phaseSw.ElapsedMilliseconds;

                ModContent.GetInstance<SteroidGuideMod>()?.Logger.Debug(
                    $"[Perf-Analyze] strict={strictMs}ms, relaxed={relaxedMs}ms, topTier={topTierMs}ms");
            }
            finally
            {
                original.Return();
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
                out _,
                ignoreOwnedForCurrentNode,
                ignoreQuantity: mode == RecipeEvaluationMode.Reachable,
                maxDisplayDepth: maxDisplayDepth);
        }

        /// <summary>
        /// Unified recursive recipe traversal.
        /// consumeAvailable=true: analysis mode (mutates available, uses noRecipeCache, breaks early on missing).
        /// consumeAvailable=false: display mode (read-only, builds full tree with fallback).
        /// </summary>
        /// <remarks>
        /// relaxedCacheRoot/Child: relaxed-pass-only memoization keyed by itemId.
        /// Two separate dicts because root entries force usableOwned=0 (ignoreOwnedForCurrentNode=true)
        /// while recursive entries honour owned-type presence — same itemId can produce different
        /// outcomes. Strict pass MUST pass null (it mutates `available` mid-recursion, breaking purity).
        /// </remarks>
        private static RecipeTreeNode TraverseRecipes(
            int itemId, int needed, RecipeGraphData graph,
            Dictionary<int, int> available, HashSet<int> visiting,
            HashSet<int> noRecipeCache,
            bool consumeAvailable,
            CancellationToken ct,
            int depth,
            out bool cycleTainted,
            bool ignoreOwnedForCurrentNode = false,
            bool ignoreQuantity = false,
            int? maxDisplayDepth = null,
            Dictionary<int, bool> relaxedCacheRoot = null,
            Dictionary<int, bool> relaxedCacheChild = null)
        {
            cycleTainted = false;
            ct.ThrowIfCancellationRequested();

            available.TryGetValue(itemId, out int ownedCount);
            var node = new RecipeTreeNode
            {
                ItemId = itemId,
                RequiredCount = needed,
                OwnedCount = ownedCount,
                IgnoreOwnedForCraftability = ignoreOwnedForCurrentNode
            };

            // Relaxed memoization lookup. Caller (Analyze relaxed pass) only inspects
            // node.Status != Missing — children are not consulted, so a hit can return a
            // bare node with Status set and no children populated.
            //
            // Lookup happens BEFORE the `visiting` cycle check so a stored success at this
            // itemId short-circuits recursion regardless of ancestor path. This is sound
            // only because cycle-hit outcomes are NEVER stored (see end of function).
            Dictionary<int, bool> relaxedCache = ignoreQuantity
                ? (ignoreOwnedForCurrentNode ? relaxedCacheRoot : relaxedCacheChild)
                : null;
            if (relaxedCache != null && relaxedCache.TryGetValue(itemId, out bool cachedCraftable))
            {
                node.Status = cachedCraftable ? NodeStatus.Craftable : NodeStatus.Missing;
                return node;
            }

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
                // Cache: Owned counts as craftable for the relaxed pass's caller check.
                // Safe because relaxed never mutates `available`, so the owned-presence test
                // is stable across the whole pass.
                relaxedCache?.TryAdd(itemId, true);
                return node;
            }

            if (noRecipeCache != null && noRecipeCache.Contains(itemId))
            {
                node.Status = NodeStatus.Missing;
                relaxedCache?.TryAdd(itemId, false);
                return node;
            }

            if (visiting.Contains(itemId))
            {
                // DO NOT cache: cycle-hit depends on the live ancestor set. The same itemId
                // visited from a disjoint path may resolve normally. Propagate cycleTainted=true
                // so any ancestor whose Missing decision rests on this cycle hit will also
                // skip caching (transitive cycle-fail correctness).
                node.Status = NodeStatus.Missing;
                cycleTainted = true;
                return node;
            }

            if (!graph.RecipesByResult.TryGetValue(itemId, out var recipes))
            {
                noRecipeCache?.Add(itemId);
                node.Status = NodeStatus.Missing;
                relaxedCache?.TryAdd(itemId, false);
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
            // Tracks whether any descendant returned a cycle-tainted Missing. If true at end,
            // we must NOT memoize this entry: its Missing might flip to Craftable when reached
            // from a path where the cycle does not fire.
            bool descendantCycleTaint = false;

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
                            depth + 1, out bool childCycleTaint,
                            ignoreOwnedForCurrentNode: false, ignoreQuantity: ignoreQuantity,
                            maxDisplayDepth: maxDisplayDepth,
                            relaxedCacheRoot: relaxedCacheRoot,
                            relaxedCacheChild: relaxedCacheChild);
                        if (childCycleTaint) descendantCycleTaint = true;
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
                            depth + 1, out _,
                            ignoreOwnedForCurrentNode: false, ignoreQuantity: ignoreQuantity,
                            maxDisplayDepth: maxDisplayDepth,
                            relaxedCacheRoot: relaxedCacheRoot,
                            relaxedCacheChild: relaxedCacheChild));
                    }
                }
            }

            // Memoize only when the result is NOT cycle-tainted. A success result with
            // descendantCycleTaint=true is still safe to cache because foundViable means
            // some recipe succeeded WITHOUT relying on a cycle-Missing child (that recipe's
            // children were all non-Missing). A Missing result with descendantCycleTaint=true
            // is unsafe: another path lacking the cycle could have flipped a cycle-tainted
            // child to Craftable, which would have made this entry succeed.
            //
            // Propagate cycleTainted up only when our own Missing decision rests on the taint;
            // a successful entry "absorbs" the taint (the picked recipe did not depend on it).
            if (relaxedCache != null)
            {
                if (node.Status != NodeStatus.Missing)
                {
                    relaxedCache.TryAdd(itemId, true);
                }
                else if (!descendantCycleTaint)
                {
                    relaxedCache.TryAdd(itemId, false);
                }
                else
                {
                    cycleTainted = true;
                }
            }
            else if (descendantCycleTaint && node.Status == NodeStatus.Missing)
            {
                // Even when we are not memoizing (display mode / strict pass), still propagate
                // taint to keep the out-parameter contract consistent for any future caller
                // that does memoize.
                cycleTainted = true;
            }

            visiting.Remove(itemId);
            return node;
        }

    }
}
