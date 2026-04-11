using StardewValley;
using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using Microsoft.Xna.Framework;
using StardewValley.Buildings;
using StardewValley.Objects;
using StardewValley.GameData.Buildings;

namespace PrimeCoopAndBarn
{
    public class ModEntry : Mod
    {
        public static ModEntry modInstance;
        public static IContentPack cpPack;
        public override void Entry(IModHelper helper)
        {
            modInstance = this;

            I18n.Init(helper.Translation);

            var mi = Helper.ModRegistry.Get("bobkalonger.primecoopnbarnCP");
            cpPack = mi.GetType().GetProperty("ContentPack")?.GetValue(mi) as IContentPack;
        }
    }
}