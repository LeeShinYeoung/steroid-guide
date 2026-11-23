using System;
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

    // Counters for the [Perf-Analyze] log line. Purely observational: nothing in the analysis
    // reads them back.
    public sealed class AnalyzeStats
    {
        public long StrictMs, RelaxedMs, TopTierMs;
        public int Roots;
        public long StrictNodes;
        public long StrictAttempts;
        public long StrictFailedAttempts;
        public int UndoMax;

        public override string ToString() =>
            $"strict={StrictMs}ms, relaxed={RelaxedMs}ms, topTier={TopTierMs}ms, roots={Roots}, " +
            $"strictNodes={StrictNodes}, attempts={StrictAttempts}, failedAttempts={StrictFailedAttempts}, " +
            $"undoMax={UndoMax}";
    }

    public static class CraftableAnalyzer
    {
        public const int InitialDisplayTreeDepthLimit = 10;

        // Write-ahead record of every change the strict pass makes to its working inventory.
        // Rolling back to a mark restores the dictionary exactly (value and key presence), in
        // reverse order, so a failed recipe attempt costs only as much as the writes it made
        // instead of copying the whole inventory before every attempt.
        private sealed class UndoLog
        {
            private readonly List<(int Key, bool Existed, int Old)> _entries = new();

            public int Count => _entries.Count;
            public int Max { get; private set; }

            public void Set(Dictionary<int, int> dict, int key, int value)
            {
                bool existed = dict.TryGetValue(key, out int old);
                _entries.Add((key, existed, old));
                if (_entries.Count > Max) Max = _entries.Count;
                dict[key] = value;
            }

            public void Rollback(Dictionary<int, int> dict, int mark)
            {
                for (int i = _entries.Count - 1; i >= mark; i--)
                {
                    var e = _entries[i];
                    if (e.Existed)
                        dict[e.Key] = e.Old;
                    else
                        dict.Remove(e.Key);
                }
                _entries.RemoveRange(mark, _entries.Count - mark);
            }
        }

        public static AnalysisResult Analyze(RecipeGraphData graph, Dictionary<int, int> available,
            CancellationToken ct = default, AnalyzeStats stats = null)
        {
            stats ??= new AnalyzeStats();
            var result = new AnalysisResult();
            // "no recipe" is a graph property independent of quantity, so the cache is shared
            // across the strict and relaxed passes.
            var noRecipeCache = new HashSet<int>();
            var phaseSw = new Stopwatch();
            stats.Roots = graph.RecipesByResult.Count;

            // Private copy: `available` is also read by the main thread while this task runs.
            var working = new Dictionary<int, int>(available);
            var undo = new UndoLog();
            // Every Add in TraverseRecipes is paired with a Remove before it returns, so the set
            // is empty again after each root and can be reused.
            var visiting = new HashSet<int>();

            // Strict pass: builds AllCraftable using exact owned counts. Each root starts from
            // the original inventory: the undo log is rolled back to empty after every root.
            phaseSw.Restart();
            foreach (var itemId in graph.RecipesByResult.Keys)
            {
                ct.ThrowIfCancellationRequested();

                var node = TraverseRecipes(itemId, 1, graph, working, visiting,
                    noRecipeCache, consumeAvailable: true, ct, depth: 0,
                    out _,
                    ignoreOwnedForCurrentNode: true,
                    undo: undo, stats: stats);
                undo.Rollback(working, 0);
                if (node.Status != NodeStatus.Missing)
                    result.AllCraftable.Add(itemId);
            }
            stats.StrictMs = phaseSw.ElapsedMilliseconds;
            stats.UndoMax = undo.Max;

            // Relaxed pass: every owned ingredient *type* is treated as infinite supply.
            // Skip items already known strictly-craftable (they cannot be in ReachableCraftable).
            // `working` equals the original inventory here (strict pass rolled everything back)
            // and the relaxed pass never writes to it.
            //
            // Relaxed memoization (Tier 1): the relaxed pass never mutates `working` (writes are
            // gated on `!ignoreQuantity`). With no in-call mutation the function becomes a pure
            // mapping (itemId, ignoreOwnedForCurrentNode) → craftable-or-not *for this Analyze
            // invocation*. We therefore memoize relaxed results in two dictionaries — one for root
            // entries (ignoreOwnedForCurrentNode=true, owned self-supply forced to 0) and one for
            // recursive child entries (false).
            //
            // Caches are scoped to this method — they are GC'd when Analyze returns. Strict pass
            // passes null (caching strict results would be unsound: strict pass DOES mutate
            // `working` mid-recursion). Cycle-hit (visiting) results are NEVER stored because they
            // depend on the live ancestor set, not the item alone.
            var relaxedCacheRoot = new Dictionary<int, bool>();
            var relaxedCacheChild = new Dictionary<int, bool>();

            phaseSw.Restart();
            var relaxed = new HashSet<int>();
            foreach (var itemId in graph.RecipesByResult.Keys)
            {
                ct.ThrowIfCancellationRequested();
                if (result.AllCraftable.Contains(itemId)) continue;

                var node = TraverseRecipes(itemId, 1, graph, working, visiting,
                    noRecipeCache, consumeAvailable: true, ct, depth: 0,
                    out _,
                    ignoreOwnedForCurrentNode: true, ignoreQuantity: true,
                    relaxedCacheRoot: relaxedCacheRoot,
                    relaxedCacheChild: relaxedCacheChild);
                if (node.Status != NodeStatus.Missing)
                    relaxed.Add(itemId);
            }
            stats.RelaxedMs = phaseSw.ElapsedMilliseconds;

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
            stats.TopTierMs = phaseSw.ElapsedMilliseconds;

            ModContent.GetInstance<SteroidGuideMod>()?.Logger.Debug($"[Perf-Analyze] {stats}");

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
        ///
        /// undo: required in strict analysis (consumeAvailable && !ignoreQuantity), the only mode that
        /// writes to `available`. Every write goes through it so a failed recipe attempt can be rolled
        /// back to the mark taken before the attempt.
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
            Dictionary<int, bool> relaxedCacheChild = null,
            UndoLog undo = null,
            AnalyzeStats stats = null)
        {
            cycleTainted = false;
            ct.ThrowIfCancellationRequested();

            // Strict analysis is the only mode that consumes the inventory.
            bool strictAnalysis = consumeAvailable && !ignoreQuantity;
            if (strictAnalysis && stats != null) stats.StrictNodes++;

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
                if (strictAnalysis)
                    undo.Set(available, itemId, ownedCount - needed);
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
                // Mark before this attempt's first write so a failure rolls back exactly what
                // the attempt (and its successful sub-crafts) consumed.
                int mark = strictAnalysis ? undo.Count : 0;
                if (strictAnalysis && stats != null) stats.StrictAttempts++;

                // Skip the consume-zero write in relaxed mode — the dict's "type present"
                // semantics must remain stable across siblings.
                if (strictAnalysis && usableOwned > 0)
                    undo.Set(available, itemId, 0);

                int batchSize = Math.Max(1, recipe.createItem.stack);
                int batches = (remaining + batchSize - 1) / batchSize;

                // Analysis callers only read Status, so the child list is built for display only.
                var children = consumeAvailable ? null : new List<RecipeTreeNode>();
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
                        relaxedCacheChild: relaxedCacheChild,
                        undo: undo, stats: stats);
                    if (childCycleTaint) descendantCycleTaint = true;
                    children?.Add(child);
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
                    node.Status = NodeStatus.Craftable;
                    node.UsedRecipe = recipe;
                    if (children != null)
                        node.Children = children;
                    foundViable = true;
                    break;
                }

                if (strictAnalysis)
                {
                    undo.Rollback(available, mark);
                    if (stats != null) stats.StrictFailedAttempts++;
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
