using ArtOfCooking.BlockEntities;
using ArtOfCooking.Blocks;
using Vintagestory.API.Common;
using System.Collections.Generic;
using System;
using ArtOfCooking.Systems;

namespace ArtOfCooking;

public class ArtOfCooking : ModSystem
{
    public override void Start(ICoreAPI api)
    {
        base.Start(api);
        api.RegisterBlockClass("blockcookingboard", typeof(BlockCookingBoard));
        api.RegisterBlockEntityClass("becookingboard", typeof(BECookingBoard));

    }
}
