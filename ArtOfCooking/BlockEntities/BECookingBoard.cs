using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using static ArtOfCooking.Systems.ArtOfCookingRecipeNames;

namespace ArtOfCooking.BlockEntities
{
    class BECookingBoard : BlockEntityDisplay
    {
        public override InventoryBase Inventory { get; }
        public override string InventoryClassName => Block.Attributes["inventoryclass"].AsString();
        public override string AttributeTransformCode => "cookingBoardTransform";

        public BECookingBoard()
        {
            Inventory = new InventoryDisplayed(this, 1, "cookingboard-slot", null, null);
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            this.capi = (api as ICoreClientAPI);
        }

        public ItemSlot InputSlot
        {
            get { return Inventory[0]; }
        }

        internal bool OnInteract(IPlayer byPlayer)
        {
            ItemSlot activeHotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;

            //If The Players Hand Is Empty
            if (activeHotbarSlot.Empty)
            {
                return this.TryTake(byPlayer);
            }

            CollectibleObject collectible = activeHotbarSlot.Itemstack.Collectible;

            ItemStack itemstack = activeHotbarSlot.Itemstack;
            AssetLocation assetLocation;
            if (activeHotbarSlot.Empty)
            {
                assetLocation = null;
            }
            else
            {
                Block block = activeHotbarSlot.Itemstack.Block;
                if (block == null)
                {
                    assetLocation = null;
                }
                else
                {
                    BlockSounds sounds = block.Sounds;
                    assetLocation = (sounds?.Place);
                }
            }

            if (DoesSlotMatchRecipe(Api.World, activeHotbarSlot) && this.TryPut(activeHotbarSlot))
            {
                this.Api.World.PlaySoundAt(assetLocation ?? new AssetLocation("sounds/player/build"), byPlayer.Entity, byPlayer, true, 16f, 1f);
                this.updateMeshes();
                base.MarkDirty(true, null);
            }
            return true;
        }

        public void ReturnStackPut(ItemStack stack)
        {
            if (this.Inventory[0].Empty)
            {
                this.Inventory[0].Itemstack = stack;
                base.MarkDirty(true, null);
                this.updateMeshes();

            }
        }

        public bool TryPut(ItemSlot slot)
        {
            for (int i = 0; i < Inventory.Count; i++)
            {
                if (this.Inventory[i].Empty)
                {
                    int num3 = slot.TryPutInto(this.Api.World, this.Inventory[i], 1);
                    this.updateMeshes();
                    base.MarkDirty(true, null);
                    return num3 > 0;
                }
            }
            this.updateMeshes();
            base.MarkDirty(true, null);
            return false;
        }

        private bool TryTake(IPlayer byPlayer)
        {
            for (int i = 0; i < Inventory.Count; i++)
            {
                if (!this.Inventory[i].Empty)
                {
                    ItemStack itemStack = this.Inventory[i].TakeOut(1);
                    if (byPlayer.InventoryManager.TryGiveItemstack(itemStack, false))
                    {
                        Block block = itemStack.Block;
                        AssetLocation assetLocation;
                        if (block == null)
                        {
                            assetLocation = null;
                        }
                        else
                        {
                            BlockSounds sounds = block.Sounds;
                            assetLocation = (sounds?.Place);
                        }
                        AssetLocation assetLocation2 = assetLocation;
                        this.Api.World.PlaySoundAt(assetLocation2 ?? new AssetLocation("sounds/player/build"), byPlayer.Entity, byPlayer, true, 16f, 1f);
                    }
                    if (itemStack.StackSize > 0)
                    {
                        this.Api.World.SpawnItemEntity(itemStack, this.Pos.ToVec3d().Add(0.5, 0.5, 0.5), null);
                    }
                    this.updateMeshes();
                    base.MarkDirty(true, null);
                    return true;
                }
            }
            this.updateMeshes();
            base.MarkDirty(true, null);
            return false;
        }



        #region ProcessTransform
        /// <summary>
        /// Processes the transform.
        /// </summary>
        /// <param name="transform">The transform.</param>
        /// <param name="side">The side.</param>
        /// <returns></returns>
        /// 

        public ModelTransform GenTransform(ItemStack stack)
        {
            MeshData meshData;
            String side = Block.Variant["side"];
            if (stack != null && stack.Collectible is IContainedMeshSource containedMeshSource)
            {

                meshData = containedMeshSource.GenMesh(stack, this.capi.BlockTextureAtlas, this.Pos);
                meshData.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0f, base.Block.Shape.rotateY * 0.017453292f, 0f);
            }
            else if (capi != null)
            {
                this.nowTesselatingObj = stack.Collectible;
                this.nowTesselatingShape = capi.TesselatorManager.GetCachedShape(stack.Collectible is Block ? (stack.Block.ShapeInventory?.Base == null ? stack.Block.Shape.Base : stack.Block.ShapeInventory.Base) : stack.Item.Shape.Base);
                if (stack.Collectible is Block)
                {
                    capi.Tesselator.TesselateShape(stack.Collectible, nowTesselatingShape, out meshData, null, null, null);
                }
                else
                {
                    capi.Tesselator.TesselateItem(stack.Item, out meshData, this);
                }

            }

            ModelTransform transform;
            if ((bool)stack?.Collectible.Attributes?["workStationTransforms"]?.Exists)
            {
                transform = stack.Collectible.Attributes["workStationTransforms"]["cookingBoardProps"]["cookingBoardTransform"].Exists ? stack.Collectible.Attributes["workStationTransforms"]["cookingBoardProps"]["cookingBoardTransform"].AsObject<ModelTransform>() : null;
                transform.Scale = 2f;
            }
            else
            {
                transform = null;
            }

            if (transform == null)
            {
                transform = new ModelTransform
                {
                    Translation = new Vec3f(),
                    Rotation = new Vec3f(0f, -45f, 0f),
                    Origin = new Vec3f(0.5f, 0f, 0.5f),
                    Scale = 0.95f
                };
            }

            transform.EnsureDefaultValues();

            if (stack != null) transform = ProcessTransform(transform, side);
            return transform;
        }

        private ModelTransform ProcessTransform(ModelTransform transform, String side)
        {

            transform.Rotation.X += AddRotate(side + "x");
            transform.Rotation.Y = (Block.Shape.rotateY) + AddRotate(side + "y");
            transform.Rotation.Z += AddRotate(side + "z");
            transform.Translation.X += AddTranslate(side + "x");
            transform.Translation.Y += AddTranslate(side + "y");
            transform.Translation.Z += AddTranslate(side + "z");
            transform.Scale = AddScale(side);
            return transform;
        }

        public float AddRotate(string sideAxis)
        {
            JsonObject transforms = this.Inventory[0].Itemstack.Collectible.Attributes["workStationTransforms"]["cookingBoardProps"]["cookingBoardTransform"];
            return transforms["rotation"][sideAxis].Exists ? transforms["rotation"][sideAxis].AsFloat() : 0f;
        }

        public float AddTranslate(string sideAxis)
        {
            JsonObject transforms = this.Inventory[0].Itemstack.Collectible.Attributes["workStationTransforms"]["cookingBoardProps"]["cookingBoardTransform"];
            return transforms["translation"][sideAxis].Exists ? transforms["translation"][sideAxis].AsFloat() : 0f;
        }

        public float AddScale(string side)
        {
            JsonObject transforms = this.Inventory[0].Itemstack.Collectible.Attributes["workStationTransforms"]["cookingBoardProps"]["cookingBoardTransform"];
            return transforms["scale"][side].Exists ? transforms["scale"][side].AsFloat() : 0.95f;
        }
        #endregion

        public bool DoesSlotMatchRecipe(IWorldAccessor world, ItemSlot slots)
        {
            List<ColdCookingRecipe> recipes = ArtOfCookingRecipeRegistry.Loaded.ColdCookingRecipes;
            if (recipes == null) return false;

            for (int j = 0; j < recipes.Count; j++)
            {
                if (recipes[j].Matches(Api.World, slots))
                {
                    return true;
                }
            }

            return false;
        }

        public ColdCookingRecipe GetMatchingColdCookingRecipe(IWorldAccessor world, ItemSlot slots, string toolmode)
        {
            List<ColdCookingRecipe> recipes = ArtOfCookingRecipeRegistry.Loaded.ColdCookingRecipes;
            if (recipes == null) return null;

            for (int j = 0; j < recipes.Count; j++)
            {
                if (recipes[j].Matches(Api.World, slots))
                {
                    return recipes[j];
                }
            }

            return null;
        }

        public void SpawnOutput(ColdCookingRecipe recipe, EntityAgent byEntity, BlockPos pos)
        {
            int j = recipe.Output.StackSize;
            for (int i = j; i > 0; i--)
            {
                Api.World.SpawnItemEntity(new ItemStack(recipe.Output.ResolvedItemstack.Collectible, 1), pos.ToVec3d(), new Vec3d(0.05f, 0.1f, 0.05f));
            }
            updateMeshes();
            base.MarkDirty(true, null);
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            //Alter this code to produce an output based on the recipe that results from the held tool and its current mode.
            //If no tool is held, return only contents
        }

        protected override float[][] genTransformationMatrices()
        {
            float[][] tfMatrices = new float[1][];
            for (int index = 0; index < 1; index++)
            {

                ItemSlot itemSlot = this.Inventory[index];
                JsonObject jsonObject;
                if (itemSlot == null)
                {
                    jsonObject = null;
                }
                else
                {
                    ItemStack itemstack = itemSlot.Itemstack;
                    if (itemstack == null)
                    {
                        jsonObject = null;
                    }
                    else
                    {
                        CollectibleObject collectible = itemstack.Collectible;
                        jsonObject = ((collectible != null) ? collectible.Attributes : null);
                        tfMatrices[index] = new Matrixf().Set(GenTransform(itemstack).AsMatrix).Values;
                    }
                }

            }
            return tfMatrices;
        }
    }
}
