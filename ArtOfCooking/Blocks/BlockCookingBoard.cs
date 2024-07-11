using ArtOfCooking.BlockEntities;
using ArtOfCooking.Systems;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using static ArtOfCooking.Systems.ArtOfCookingRecipeNames;

namespace ArtOfCooking.Blocks
{
    class BlockCookingBoard : Block
    {


        ColdCookingRecipe recipe;
        public override string GetHeldItemName(ItemStack stack) => GetName();
        public override string GetPlacedBlockName(IWorldAccessor world, BlockPos pos) => GetName();

        public string GetName()
        {
            var material = Variant["wood"];

            var part = Lang.Get("material-" + $"{material}");
            part = $"{part[0].ToString().ToUpper()}{part.Substring(1)}";
            return string.Format($"{part} {Lang.Get("artofcooking:block-cookingboard")}");
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            string curTMode = "";
            ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;
            ItemStack stack = byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack;
            CollectibleObject collObj = byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack?.Collectible;

            //Check to see if block entity exists
            if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is not BECookingBoard becookingboard) return base.OnBlockInteractStart(world, byPlayer, blockSel);

            if (collObj != null)

            if (!becookingboard.Inventory.Empty)
            {
                recipe = becookingboard.GetMatchingColdCookingRecipe(world, becookingboard.InputSlot, curTMode);
                if (recipe != null)
                {
                    resistance = (becookingboard.Inventory[0].Itemstack.Collectible is Block ? becookingboard.Inventory[0].Itemstack.Block.Resistance : becookingboard.Inventory[0].Itemstack.Collectible.Attributes["resistance"].AsFloat());          
                    return true;
                }
                return false;
                
            }

            return becookingboard.OnInteract(byPlayer);
        }

        public override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            CollectibleObject cutTool = byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack?.Collectible;
            BECookingBoard becookingboard = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BECookingBoard;
            BlockPos pos = blockSel.Position;

            if (cutTool != null && !becookingboard.Inventory.Empty)
            {
                if (playNextSound < secondsUsed)
                {
                    api.World.PlaySoundAt(new AssetLocation("sounds/block/chop2"), pos.X, pos.Y, pos.Z, byPlayer, true, 32, 1f);
                    playNextSound += .7f;
                }

                if (becookingboard.Inventory[0].Itemstack.Collectible is Block)
                {
                    curDmgFromMiningSpeed += (cutTool.GetMiningSpeed(byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack, blockSel, becookingboard.Inventory[0].Itemstack.Block, byPlayer)) * (secondsUsed - lastSecondsUsed);
                }
                else
                {
                    curDmgFromMiningSpeed += (cutTool.MiningSpeed[(EnumBlockMaterial)recipe.IngredientMaterial]) * (secondsUsed - lastSecondsUsed);
                }

                lastSecondsUsed = secondsUsed;
                float curMiningProgress = (secondsUsed + (curDmgFromMiningSpeed));
                float curResistance = resistance;
                api.Logger.Debug("Resistance of item on block is: " + resistance + ". Resistance after multiplier is " + curResistance + ".");
                if (curMiningProgress >= curResistance)
                {
                    if (api.Side == EnumAppSide.Server)
                    {
                        becookingboard.SpawnOutput(recipe, byPlayer.Entity, blockSel.Position);

                        EntityPlayer playerEntity = byPlayer.Entity;

                        cutTool.DamageItem(api.World, playerEntity, playerEntity.RightHandItemSlot);

                        if (recipe.ReturnStack.ResolvedItemstack.Collectible.FirstCodePart() == "air")
                        {
                            becookingboard.Inventory.Clear();
                        }
                        else
                        {
                            becookingboard.Inventory.Clear();
                            becookingboard.ReturnStackPut(recipe.ReturnStack.ResolvedItemstack.Clone());
                        }
                    }
                    return false;
                }
                return !becookingboard.Inventory.Empty;
            }
            return false;
        }



        public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            resistance = 0;
            lastSecondsUsed = 0;
            curDmgFromMiningSpeed = 0;
            playNextSound = 0.7f;
            BECookingBoard becookingboard = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BECookingBoard;
            becookingboard.MarkDirty(true);
            becookingboard.updateMeshes();

        }

        private float playNextSound;
        private float resistance;
        private float lastSecondsUsed;
        private float curDmgFromMiningSpeed;
    }

}
