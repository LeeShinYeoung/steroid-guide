// TEMPORARY (feature/analysis-speed): A/B check of CraftableAnalyzer against the v1.1.6
// reference copy. Delete this folder before merging.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace SteroidGuide.Common.Dev
{
    public class VerifyAnalyzerCommand : ModCommand
    {
        private static CancellationTokenSource _cts;

        public override CommandType Type => CommandType.Chat;
        public override string Command => "sgverify";
        public override string Usage => "/sgverify [fuzzCount=20] [seed=1] | /sgverify stop";
        public override string Description => "Steroid Guide dev: compare the craftable analyzer with the v1.1.6 reference";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            if (args.Length > 0 && args[0] == "stop")
            {
                _cts?.Cancel();
                caller.Reply("[sgverify] stop requested", Color.Orange);
                return;
            }
            if (_cts != null)
            {
                caller.Reply("[sgverify] already running (use /sgverify stop)", Color.Orange);
                return;
            }

            int fuzzCount = args.Length > 0 && int.TryParse(args[0], out int f) ? Math.Max(0, f) : 20;
            int seed = args.Length > 1 && int.TryParse(args[1], out int s) ? s : 1;

            var graph = RecipeGraphSystem.Graph;
            if (graph == null || Main.LocalPlayer == null)
            {
                caller.Reply("[sgverify] recipe graph or player not ready", Color.Red);
                return;
            }

            // Scan on the main thread; everything after this works on private copies.
            var baseItems = new Dictionary<int, int>(ItemScanner.ScanAvailableItems(Main.LocalPlayer).Items);
            var cts = new CancellationTokenSource();
            _cts = cts;
            caller.Reply($"[sgverify] started: {baseItems.Count} item types, fuzz={fuzzCount}, seed={seed}", Color.LightBlue);

            Task.Run(() =>
            {
                string summary;
                try
                {
                    summary = Run(graph, baseItems, fuzzCount, seed, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    summary = "[sgverify] cancelled";
                }
                catch (Exception e)
                {
                    Log.Error("[sgverify] crashed", e);
                    summary = "[sgverify] crashed: " + e.GetType().Name + ": " + e.Message;
                }
                finally
                {
                    _cts = null;
                    cts.Dispose();
                }
                Main.QueueMainThreadAction(() => Main.NewText(summary, Color.LightBlue));
            });
        }

        private static log4net.ILog Log => ModContent.GetInstance<SteroidGuideMod>().Logger;

        private static string Run(RecipeGraphData graph, Dictionary<int, int> baseItems, int fuzzCount, int seed, CancellationToken ct)
        {
            LogGraphStats(graph);
            Log.Info($"[sgverify] base inventory: {baseItems.Count} types, hash={InventoryHash(baseItems):X8}");

            // JIT warm-up so the first timed case is not penalised.
            LegacyCraftableAnalyzer.Analyze(graph, new Dictionary<int, int>(baseItems), new LegacyCounters(), ct);
            CraftableAnalyzer.Analyze(graph, new Dictionary<int, int>(baseItems), ct);

            int failures = 0;
            long legacyTotal = 0, newTotal = 0;
            var counters = new LegacyCounters();

            for (int i = -1; i < fuzzCount; i++)
            {
                ct.ThrowIfCancellationRequested();
                string label;
                Dictionary<int, int> items;
                if (i < 0)
                {
                    label = "base";
                    items = new Dictionary<int, int>(baseItems);
                }
                else
                {
                    var rng = new Random(unchecked(seed * 7919 + i));
                    (label, items) = MakeFuzzCase(i, rng, baseItems, graph);
                    label = $"fuzz#{i} {label} seed={seed}";
                }

                var reference = new Dictionary<int, int>(items);

                var legacyInput = new Dictionary<int, int>(items);
                var sw = Stopwatch.StartNew();
                var expected = LegacyCraftableAnalyzer.Analyze(graph, legacyInput, counters, ct);
                long legacyMs = sw.ElapsedMilliseconds;

                var newInput = new Dictionary<int, int>(items);
                sw.Restart();
                var actual = CraftableAnalyzer.Analyze(graph, newInput, ct);
                long newMs = sw.ElapsedMilliseconds;

                legacyTotal += legacyMs;
                newTotal += newMs;

                var problems = new List<string>();
                Compare("AllCraftable", expected.AllCraftable, actual.AllCraftable, problems);
                Compare("TopTierItems", expected.TopTierItems, actual.TopTierItems, problems);
                Compare("ReachableCraftable", expected.ReachableCraftable, actual.ReachableCraftable, problems);
                Compare("ReachableTopTierItems", expected.ReachableTopTierItems, actual.ReachableTopTierItems, problems);
                if (!SameDict(reference, newInput)) problems.Add("new analyzer mutated its input");
                if (!SameDict(reference, legacyInput)) problems.Add("legacy analyzer mutated its input");

                if (i < 0)
                {
                    // Determinism: a second run on the same input must match the first.
                    var again = CraftableAnalyzer.Analyze(graph, new Dictionary<int, int>(items), ct);
                    Compare("determinism.AllCraftable", actual.AllCraftable, again.AllCraftable, problems);
                    Compare("determinism.ReachableCraftable", actual.ReachableCraftable, again.ReachableCraftable, problems);
                }

                string line = $"[sgverify] {label}: types={items.Count} legacy={legacyMs}ms new={newMs}ms " +
                    $"all={actual.AllCraftable.Count} top={actual.TopTierItems.Count} " +
                    $"reach={actual.ReachableCraftable.Count} reachTop={actual.ReachableTopTierItems.Count}";
                if (problems.Count == 0)
                {
                    Log.Info(line + " OK");
                }
                else
                {
                    failures++;
                    Log.Warn(line + " MISMATCH\n  " + string.Join("\n  ", problems));
                }
            }

            Log.Info($"[sgverify] legacy needs: max={counters.MaxNeed} negative={counters.NegativeNeedCount} overflow={counters.OverflowCount}");
            string verdict = failures == 0 ? "ALL OK" : $"{failures} MISMATCH(ES) - see client.log";
            string summary = $"[sgverify] {verdict}: {fuzzCount + 1} cases, legacy={legacyTotal}ms new={newTotal}ms";
            Log.Info(summary);
            return summary;
        }

        private static (string, Dictionary<int, int>) MakeFuzzCase(int i, Random rng, Dictionary<int, int> baseItems, RecipeGraphData graph)
        {
            var keys = baseItems.Keys.ToList();
            var items = new Dictionary<int, int>();
            switch (i % 6)
            {
                case 0:
                    foreach (var (k, v) in baseItems)
                        if (rng.NextDouble() >= 0.3)
                            items[k] = Math.Max(1, (int)Math.Ceiling(v * rng.NextDouble()));
                    return ("drop30+scale", items);
                case 1:
                    foreach (var k in keys) items[k] = 1;
                    return ("all-ones", items);
                case 2:
                    foreach (var (k, v) in baseItems) items[k] = v;
                    foreach (var k in Pick(rng, graph.ItemUsedInResults.Keys.Where(x => !baseItems.ContainsKey(x)).ToList(), 50))
                        items[k] = rng.Next(1, 51);
                    return ("add-unowned-ingredients", items);
                case 3:
                    foreach (var (k, v) in baseItems) items[k] = v;
                    foreach (var k in Pick(rng, graph.RecipesByResult.Keys.ToList(), 50))
                        items[k] = rng.Next(1, 21);
                    return ("add-intermediates", items);
                case 4:
                    foreach (var k in Pick(rng, keys, 10)) items[k] = baseItems[k];
                    return ("tiny", items);
                default:
                    return ("empty", items);
            }
        }

        private static IEnumerable<int> Pick(Random rng, List<int> source, int count)
        {
            for (int n = 0; n < count && source.Count > 0; n++)
            {
                int idx = rng.Next(source.Count);
                yield return source[idx];
                source.RemoveAt(idx);
            }
        }

        // Compares element order too: AllCraftable/ReachableCraftable enumeration order drives the
        // TopTier list order, which the UI shows before sorting ties.
        private static void Compare(string name, IEnumerable<int> expected, IEnumerable<int> actual, List<string> problems)
        {
            var e = expected.ToList();
            var a = actual.ToList();
            if (e.SequenceEqual(a)) return;

            var es = new HashSet<int>(e);
            var asSet = new HashSet<int>(a);
            var missing = e.Where(x => !asSet.Contains(x)).ToList();
            var extra = a.Where(x => !es.Contains(x)).ToList();
            if (missing.Count == 0 && extra.Count == 0)
            {
                problems.Add($"{name}: same set, different order (count={e.Count})");
                return;
            }
            problems.Add($"{name}: missing {missing.Count} [{Names(missing)}] extra {extra.Count} [{Names(extra)}]");
        }

        private static string Names(List<int> ids)
        {
            var sb = new StringBuilder();
            foreach (int id in ids.Take(25))
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(id).Append(':').Append(Lang.GetItemNameValue(id));
            }
            if (ids.Count > 25) sb.Append(", ...");
            return sb.ToString();
        }

        private static bool SameDict(Dictionary<int, int> a, Dictionary<int, int> b)
        {
            if (a.Count != b.Count) return false;
            foreach (var (k, v) in a)
                if (!b.TryGetValue(k, out int bv) || bv != v) return false;
            return true;
        }

        private static uint InventoryHash(Dictionary<int, int> items)
        {
            uint h = 2166136261;
            foreach (var (k, v) in items.OrderBy(kv => kv.Key))
            {
                h = (h ^ (uint)k) * 16777619;
                h = (h ^ (uint)v) * 16777619;
            }
            return h;
        }

        private static void LogGraphStats(RecipeGraphData graph)
        {
            int recipes = 0, zeroOrNegativeStack = 0, negativeStack = 0, badCreateStack = 0, noIngredients = 0, selfIngredient = 0;
            foreach (var (resultId, list) in graph.RecipesByResult)
            {
                foreach (var recipe in list)
                {
                    recipes++;
                    if (recipe.createItem.stack <= 0) badCreateStack++;
                    bool any = false;
                    foreach (var ing in recipe.requiredItem)
                    {
                        if (ing.type <= Terraria.ID.ItemID.None) continue;
                        any = true;
                        if (ing.stack <= 0) zeroOrNegativeStack++;
                        if (ing.stack < 0) negativeStack++;
                        if (ing.type == resultId) selfIngredient++;
                    }
                    if (!any) noIngredients++;
                }
            }
            Log.Info($"[sgverify] graph: results={graph.RecipesByResult.Count} recipes={recipes} " +
                $"ingredientStack<=0={zeroOrNegativeStack} ingredientStack<0={negativeStack} createStack<=0={badCreateStack} " +
                $"noIngredients={noIngredients} selfIngredient={selfIngredient}");
        }
    }
}
