using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.ServerMods;
using Vintagestory.GameContent;
using static ArtOfCooking.Systems.ArtOfCookingRecipeNames.ArtOfCookingRecipeLoader;

namespace ArtOfCooking.Systems
{
    public class ArtOfCookingRecipeNames : ICookingRecipeNamingHelper
    {
        public string GetNameForIngredients(IWorldAccessor worldForResolve, string recipeCode, ItemStack[] stacks)
        {
            return "Cheat Code";
        }
        public class ArtOfCookingRecipeRegistry
        {
            private static ArtOfCookingRecipeRegistry loaded;
            private List<ColdCookingRecipe> coldCookingRecipe = new();

            public List<ColdCookingRecipe> ColdCookingRecipes
            {
                get
                {
                    return coldCookingRecipe;
                }
                set
                {
                    coldCookingRecipe = value;
                }
            }
            public static ArtOfCookingRecipeRegistry Create()
            {
                loaded ??= new ArtOfCookingRecipeRegistry();
                return Loaded;
            }

            public static ArtOfCookingRecipeRegistry Loaded
            {
                get
                {
                    loaded ??= new ArtOfCookingRecipeRegistry();
                    return loaded;
                }
            }

            public static void Dispose()
            {
                if (loaded == null) return;
                loaded = null;
            }
        }

        public class ArtOfCookingRecipeLoader : RecipeLoader
        {
            public ICoreServerAPI api;

            public override double ExecuteOrder()
            {
                return 100;
            }

            public override void AssetsFinalize(ICoreAPI capi)
            {
                ArtOfCookingRecipeRegistry.Create();
                LoadArtOfCookingRecipes();
                base.AssetsFinalize(api);
            }

            public override void AssetsLoaded(ICoreAPI api)
            {
                //override to prevent double loading
                if (api is not ICoreServerAPI sapi) return;
                this.api = sapi;
            }

            public override void Dispose()
            {
                base.Dispose();
                ArtOfCookingRecipeRegistry.Dispose();
            }

            public void LoadArtOfCookingRecipes()
            {
                api.World.Logger.StoryEvent(Lang.Get("artofcooking:Pieces of food..."));
                LoadColdCookingRecipes();
            }

            #region Cold Cooking Recipes
            public void LoadColdCookingRecipes()
            {
                Dictionary<AssetLocation, JToken> files = api.Assets.GetMany<JToken>(api.Server.Logger, "recipes/coldcooking");
                int recipeQuantity = 0;
                int ignored = 0;

                foreach (var val in files)
                {
                    if (val.Value is JObject)
                    {
                        ColdCookingRecipe rec = val.Value.ToObject<ColdCookingRecipe>();
                        if (!rec.Enabled) continue;

                        LoadColdCookingRecipe(val.Key, rec, ref recipeQuantity, ref ignored);
                    }
                    if (val.Value is JArray)
                    {
                        foreach (var token in val.Value as JArray)
                        {
                            ColdCookingRecipe rec = token.ToObject<ColdCookingRecipe>();
                            if (!rec.Enabled) continue;

                            LoadColdCookingRecipe(val.Key, rec, ref recipeQuantity, ref ignored);
                        }
                    }
                }

                api.World.Logger.Event("{0} cold cooking recipes loaded", recipeQuantity);
                api.World.Logger.StoryEvent(Lang.Get("artofcooking:...are cut into meal"));
            }

            public void LoadColdCookingRecipe(AssetLocation path, ColdCookingRecipe recipe, ref int quantityRegistered, ref int quantityIgnored)
            {
                if (!recipe.Enabled) return;
                if (recipe.Name == null) recipe.Name = path;
                string className = "cold cooking recipe";


                Dictionary<string, string[]> nameToCodeMapping = recipe.GetNameToCodeMapping(api.World);

                if (nameToCodeMapping.Count > 0)
                {
                    List<ColdCookingRecipe> subRecipes = new();

                    int qCombs = 0;
                    bool first = true;
                    foreach (var val2 in nameToCodeMapping)
                    {
                        if (first) qCombs = val2.Value.Length;
                        else qCombs *= val2.Value.Length;
                        first = false;
                    }

                    first = true;
                    foreach (var val2 in nameToCodeMapping)
                    {
                        string variantCode = val2.Key;
                        string[] variants = val2.Value;

                        for (int i = 0; i < qCombs; i++)
                        {
                            ColdCookingRecipe rec;

                            if (first) subRecipes.Add(rec = recipe.Clone());
                            else rec = subRecipes[i];

                            if (rec.Ingredients != null)
                            {
                                foreach (var ingreds in rec.Ingredients)
                                {
                                    if (ingreds.Inputs.Length <= 0) continue;
                                    CraftingRecipeIngredient ingred = ingreds.Inputs[0];

                                    if (ingred.Name == variantCode)
                                    {
                                        ingred.Code = ingred.Code.CopyWithPath(ingred.Code.Path.Replace("*", variants[i % variants.Length]));
                                    }
                                }
                            }

                            rec.ReturnStack.FillPlaceHolder(val2.Key, variants[i % variants.Length]);
                            rec.Output.FillPlaceHolder(val2.Key, variants[i % variants.Length]);
                        }

                        first = false;
                    }

                    if (subRecipes.Count == 0)
                    {
                        api.World.Logger.Warning("{1} file {0} make uses of wildcards, but no blocks or item matching those wildcards were found.", path, className);
                    }

                    foreach (ColdCookingRecipe subRecipe in subRecipes)
                    {
                        if (!subRecipe.Resolve(api.World, className + " " + path))
                        {
                            quantityIgnored++;
                            continue;
                        }
                        ArtOfCookingRecipeRegistry.Loaded.ColdCookingRecipes.Add(subRecipe);
                        quantityRegistered++;
                    }

                }
                else
                {
                    if (!recipe.Resolve(api.World, className + " " + path))
                    {
                        quantityIgnored++;
                        return;
                    }

                    ArtOfCookingRecipeRegistry.Loaded.ColdCookingRecipes.Add(recipe);
                    quantityRegistered++;
                }
            }

            public class ColdCookingIngredient : IByteSerializable
            {
                public CraftingRecipeIngredient[] Inputs;

                public CraftingRecipeIngredient GetMatch(ItemStack stack)
                {
                    if (stack == null) return null;

                    for (int i = 0; i < Inputs.Length; i++)
                    {
                        if (Inputs[i].SatisfiesAsIngredient(stack)) return Inputs[i];
                    }

                    return null;
                }

                public bool Resolve(IWorldAccessor world, string debug)
                {
                    bool ok = true;

                    for (int i = 0; i < Inputs.Length; i++)
                    {
                        ok &= Inputs[i].Resolve(world, debug);
                    }

                    return ok;
                }

                public void FromBytes(BinaryReader reader, IWorldAccessor resolver)
                {
                    Inputs = new CraftingRecipeIngredient[reader.ReadInt32()];

                    for (int i = 0; i < Inputs.Length; i++)
                    {
                        Inputs[i] = new CraftingRecipeIngredient();
                        Inputs[i].FromBytes(reader, resolver);
                        Inputs[i].Resolve(resolver, "Ground Ingredient (FromBytes)");
                    }
                }

                public void ToBytes(BinaryWriter writer)
                {
                    writer.Write(Inputs.Length);
                    for (int i = 0; i < Inputs.Length; i++)
                    {
                        Inputs[i].ToBytes(writer);
                    }
                }

                public ColdCookingIngredient Clone()
                {
                    CraftingRecipeIngredient[] newings = new CraftingRecipeIngredient[Inputs.Length];

                    for (int i = 0; i < Inputs.Length; i++)
                    {
                        newings[i] = Inputs[i].Clone();
                    }

                    return new ColdCookingIngredient()
                    {
                        Inputs = newings
                    };
                }
            }
            #endregion


        }

        public class ColdCookingRecipe : IByteSerializable
        {
            public string Code = "ColdCookingRecipe";

            public AssetLocation Name { get; set; }
            public bool Enabled { get; set; } = true;
            public int IngredientMaterial { get; set; } = 4;
            public double IngredientResistance { get; set; } = 4.0;

            public ColdCookingIngredient[] Ingredients;

            public JsonItemStack Output;

            public JsonItemStack ReturnStack = new() { Code = new AssetLocation("air"), Type = EnumItemClass.Block };

            public ItemStack TryCraftNow(ICoreAPI api, ItemSlot inputslots)
            {

                var matched = PairInput(inputslots);

                ItemStack mixedStack = Output.ResolvedItemstack.Clone();
                mixedStack.StackSize = GetOutputSize(matched);

                if (mixedStack.StackSize <= 0) return null;


                foreach (var val in matched)
                {
                    val.Key.TakeOut(val.Value.Quantity * (mixedStack.StackSize / Output.StackSize));
                    val.Key.MarkDirty();
                }

                return mixedStack;
            }

            public bool Matches(IWorldAccessor worldForResolve, ItemSlot inputSlots)
            {
                int outputStackSize = 0;

                List<KeyValuePair<ItemSlot, CraftingRecipeIngredient>> matched = PairInput(inputSlots);
                if (matched == null) return false;

                outputStackSize = GetOutputSize(matched);

                return outputStackSize >= 0;
            }

            List<KeyValuePair<ItemSlot, CraftingRecipeIngredient>> PairInput(ItemSlot inputStacks)
            {
                List<int> alreadyFound = new();

                Queue<ItemSlot> inputSlotsList = new();
                if (!inputStacks.Empty) inputSlotsList.Enqueue(inputStacks);

                if (inputSlotsList.Count != Ingredients.Length) return null;

                List<KeyValuePair<ItemSlot, CraftingRecipeIngredient>> matched = new();

                while (inputSlotsList.Count > 0)
                {
                    ItemSlot inputSlot = inputSlotsList.Dequeue();
                    bool found = false;

                    for (int i = 0; i < Ingredients.Length; i++)
                    {
                        CraftingRecipeIngredient ingred = Ingredients[i].GetMatch(inputSlot.Itemstack);

                        if (ingred != null && !alreadyFound.Contains(i))
                        {
                            matched.Add(new KeyValuePair<ItemSlot, CraftingRecipeIngredient>(inputSlot, ingred));
                            alreadyFound.Add(i);
                            found = true;
                            break;
                        }
                    }

                    if (!found) return null;
                }

                // We're missing ingredients
                if (matched.Count != Ingredients.Length)
                {
                    return null;
                }

                return matched;
            }


            int GetOutputSize(List<KeyValuePair<ItemSlot, CraftingRecipeIngredient>> matched)
            {
                int outQuantityMul = -1;

                foreach (var val in matched)
                {
                    ItemSlot inputSlot = val.Key;
                    CraftingRecipeIngredient ingred = val.Value;
                    int posChange = inputSlot.StackSize / ingred.Quantity;

                    if (posChange < outQuantityMul || outQuantityMul == -1) outQuantityMul = posChange;
                }

                if (outQuantityMul == -1)
                {
                    return -1;
                }


                foreach (var val in matched)
                {
                    ItemSlot inputSlot = val.Key;
                    CraftingRecipeIngredient ingred = val.Value;


                    // Must have same or more than the total crafted amount
                    if (inputSlot.StackSize < ingred.Quantity * outQuantityMul) return -1;

                }

                outQuantityMul = 1;
                return Output.StackSize * outQuantityMul;
            }

            public string GetOutputName()
            {
                return Lang.Get("artofcooking:Will make {0}", Output.ResolvedItemstack.GetName());
            }

            public bool Resolve(IWorldAccessor world, string sourceForErrorLogging)
            {
                bool ok = true;

                for (int i = 0; i < Ingredients.Length; i++)
                {
                    ok &= Ingredients[i].Resolve(world, sourceForErrorLogging);
                }

                ok &= Output.Resolve(world, sourceForErrorLogging);

                ok &= ReturnStack.Resolve(world, sourceForErrorLogging);


                return ok;
            }

            public void ToBytes(BinaryWriter writer)
            {
                writer.Write(Code);
                writer.Write(IngredientMaterial);
                writer.Write(IngredientResistance);
                writer.Write(Ingredients.Length);
                for (int i = 0; i < Ingredients.Length; i++)
                {
                    Ingredients[i].ToBytes(writer);
                }

                Output.ToBytes(writer);
                ReturnStack.ToBytes(writer);
            }

            public void FromBytes(BinaryReader reader, IWorldAccessor resolver)
            {
                Code = reader.ReadString();
                IngredientMaterial = reader.ReadInt32();
                IngredientResistance = reader.ReadDouble();
                Ingredients = new ColdCookingIngredient[reader.ReadInt32()];

                for (int i = 0; i < Ingredients.Length; i++)
                {
                    Ingredients[i] = new ColdCookingIngredient();
                    Ingredients[i].FromBytes(reader, resolver);
                    Ingredients[i].Resolve(resolver, "Cold Cooking Recipe (FromBytes)");
                }

                Output = new JsonItemStack();
                Output.FromBytes(reader, resolver.ClassRegistry);
                Output.Resolve(resolver, "Cold Cooking Recipe (FromBytes)");
                ReturnStack = new JsonItemStack();
                ReturnStack.FromBytes(reader, resolver.ClassRegistry);
                ReturnStack.Resolve(resolver, "Cold Cooking Recipe (FromBytes)");
            }

            public ColdCookingRecipe Clone()
            {
                ColdCookingIngredient[] ingredients = new ColdCookingIngredient[Ingredients.Length];
                for (int i = 0; i < Ingredients.Length; i++)
                {
                    ingredients[i] = Ingredients[i].Clone();
                }

                return new ColdCookingRecipe()
                {

                    Output = Output.Clone(),
                    ReturnStack = ReturnStack.Clone(),
                    Code = Code,
                    IngredientMaterial = IngredientMaterial,
                    IngredientResistance = IngredientResistance,
                    Enabled = Enabled,
                    Name = Name,
                    Ingredients = ingredients
                };
            }

            public Dictionary<string, string[]> GetNameToCodeMapping(IWorldAccessor world)
            {
                Dictionary<string, string[]> mappings = new();

                if (Ingredients == null || Ingredients.Length == 0) return mappings;

                foreach (var ingreds in Ingredients)
                {
                    if (ingreds.Inputs.Length <= 0) continue;
                    CraftingRecipeIngredient ingred = ingreds.Inputs[0];
                    if (ingred == null || !ingred.Code.Path.Contains("*") || ingred.Name == null) continue;

                    int wildcardStartLen = ingred.Code.Path.IndexOf("*");
                    int wildcardEndLen = ingred.Code.Path.Length - wildcardStartLen - 1;

                    List<string> codes = new();

                    if (ingred.Type == EnumItemClass.Block)
                    {
                        for (int i = 0; i < world.Blocks.Count; i++)
                        {
                            if (world.Blocks[i].Code == null || world.Blocks[i].IsMissing) continue;

                            if (WildcardUtil.Match(ingred.Code, world.Blocks[i].Code))
                            {
                                string code = world.Blocks[i].Code.Path.Substring(wildcardStartLen);
                                string codepart = code.Substring(0, code.Length - wildcardEndLen);
                                if (ingred.AllowedVariants != null && !ingred.AllowedVariants.Contains(codepart)) continue;

                                codes.Add(codepart);

                            }
                        }
                    }
                    else
                    {
                        for (int i = 0; i < world.Items.Count; i++)
                        {
                            if (world.Items[i].Code == null || world.Items[i].IsMissing) continue;

                            if (WildcardUtil.Match(ingred.Code, world.Items[i].Code))
                            {
                                string code = world.Items[i].Code.Path.Substring(wildcardStartLen);
                                string codepart = code.Substring(0, code.Length - wildcardEndLen);
                                if (ingred.AllowedVariants != null && !ingred.AllowedVariants.Contains(codepart)) continue;

                                codes.Add(codepart);
                            }
                        }
                    }

                    mappings[ingred.Name] = codes.ToArray();
                }

                return mappings;
            }
        }
    }
}