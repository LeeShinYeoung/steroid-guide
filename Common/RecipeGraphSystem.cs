using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace SteroidGuide.Common
{
    public class RecipeGraphData
    {
        public Dictionary<int, List<Recipe>> RecipesByResult = new();
        public Dictionary<int, HashSet<int>> ItemUsedInResults = new();
    }

    public class RecipeGraphSystem : ModSystem
    {
        public static RecipeGraphData Graph { get; private set; }

        public override void PostAddRecipes()
        {
            BuildGraph();
        }

        private void BuildGraph()
        {
            Graph = new RecipeGraphData();

            for (int i = 0; i < Recipe.numRecipes; i++)
            {
                var recipe = Main.recipe[i];
                if (recipe.createItem.type <= ItemID.None)
                    continue;

                int resultId = recipe.createItem.type;

                if (!Graph.RecipesByResult.ContainsKey(resultId))
                    Graph.RecipesByResult[resultId] = new List<Recipe>();
                Graph.RecipesByResult[resultId].Add(recipe);

                foreach (var ingredient in recipe.requiredItem)
                {
                    if (ingredient.type <= ItemID.None)
                        continue;

                    if (!Graph.ItemUsedInResults.ContainsKey(ingredient.type))
                        Graph.ItemUsedInResults[ingredient.type] = new HashSet<int>();
                    Graph.ItemUsedInResults[ingredient.type].Add(resultId);
                }
            }
        }

        public override void Unload()
        {
            Graph = null;
        }
    }
}
