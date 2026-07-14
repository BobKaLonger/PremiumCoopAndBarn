using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Objects;
using StardewValley.GameData.Buildings;
using System.Reflection;

namespace PremiumCoopAndBarn
{
    public interface IContentPatcherAPI
    {
        bool IsConditionsApiReady { get; }
        void RegisterToken(IManifest mod, string name, Func<IEnumerable<string>> getValue);
    }
    public class ModEntry : Mod
    {
        public static ModEntry? modInstance;
        internal const string PremiumCP = "bobkalonger.PremiumCoopAndBarnCP_";
        internal const string PremiumBarn = $"{PremiumCP}PremiumBarn";
        internal const string PremiumCoop = $"{PremiumCP}PremiumCoop";
        internal const string DeluxePlusBarn = $"{PremiumCP}DeluxePlusBarn";
        internal const string DeluxePlusCoop = $"{PremiumCP}DeluxePlusCoop";
        public override void Entry(IModHelper helper)
        {
            if (helper.ModRegistry.IsLoaded("FlashShifter.StardewValleyExpandedCP"))
            {
                Monitor.Log("SVE is loaded, Premium Coop and Barn (Standalone) is disabled.", LogLevel.Info);
                return;
            }

            modInstance = this;

            helper.Events.Player.Warped += PlayerOnWarped;
            helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            helper.Events.GameLoop.DayStarted += OnDayStarted;

            var harmony = new Harmony(this.ModManifest.UniqueID);

            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        private void PlayerOnWarped(object? sender, WarpedEventArgs e)
        {
            RemoveCustomlights(e.OldLocation);

            foreach (var b in e.NewLocation.buildings)
            {
                if (b.buildingType.Value == PremiumBarn)
                {
                    var premiumLightBL = new Point(b.tileX.Value + 3, b.tileY.Value + 3);
                    var ll = new LightSource($"{PremiumCP}BarnLight_{b.tileX.Value}_{b.tileY.Value}_L", 4, premiumLightBL.ToVector2() * Game1.tileSize, 1f, Color.Black, LightSource.LightContext.None);
                    Game1.currentLightSources.Add(ll.Id, ll);

                    var premiumLightBR = new Point(b.tileX.Value + 8, b.tileY.Value + 3);
                    var lr = new LightSource($"{PremiumCP}BarnLight_{b.tileX.Value}_{b.tileY.Value}_R", 4, premiumLightBR.ToVector2() * Game1.tileSize, 1f, Color.Black, LightSource.LightContext.None);
                    Game1.currentLightSources.Add(lr.Id, lr);
                }

                if (b.buildingType.Value == PremiumCoop)
                {
                    var premiumLightC = new Point(b.tileX.Value + 6, b.tileY.Value + 2);
                    var lc = new LightSource($"{PremiumCP}CoopLight_{b.tileX.Value}_{b.tileY.Value}", 4, premiumLightC.ToVector2() * Game1.tileSize, 1f, Color.Black, LightSource.LightContext.None);
                    Game1.currentLightSources.Add(lc.Id, lc);
                }
            }
        }

        private static void RemoveCustomlights(GameLocation location)
        {
            if (location == null || !Context.IsWorldReady)
                return;

            var toRemove = Game1.currentLightSources.Keys
                .Where(k => k.StartsWith(PremiumCP))
                .ToList();
            foreach (var key in toRemove)
                Game1.currentLightSources.Remove(key);
        }

        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            Utility.ForEachBuilding(building =>
            {
                if (building.buildingType.Value is PremiumBarn or PremiumCoop or DeluxePlusBarn or DeluxePlusCoop)
                {
                    var interior = building.GetIndoors();

                    foreach (var animal in ((AnimalHouse)interior).animals.Values)
                    {
                        if (animal.currentLocation != interior)
                            animal.currentLocation = interior;
                    }
                }
                return true;
            });
        }

        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            Utility.ForEachBuilding(building =>
            {
                if (building.buildingType.Value is PremiumBarn or PremiumCoop or DeluxePlusBarn or DeluxePlusCoop)
                {
                    var interior = building.GetIndoors();

                    foreach (var animal in ((AnimalHouse)interior).animals.Values)
                    {
                        if (animal.currentLocation != interior)
                            animal.currentLocation = interior;
                    }
                }
                return true;
            });

            Utility.ForEachBuilding(building =>
            {
                if (building.buildingType.Value is not (PremiumBarn or PremiumCoop or DeluxePlusBarn or DeluxePlusCoop))
                    return true;

                var interior = building.GetIndoors();

                if (building.daysUntilUpgrade.Value > 0 || interior == null)
                    return true;

                string upgradeKey = $"{ModManifest.UniqueID}/buildingKey";
                string currentLevel = building.buildingType.Value;
                building.modData.TryGetValue(upgradeKey, out string lastMovedLevel);

                if (lastMovedLevel != currentLevel)
                {
                    if (building.buildingType.Value is PremiumBarn or DeluxePlusBarn)
                        BarnItemMoves(interior);
                    else if (building.buildingType.Value is PremiumCoop or DeluxePlusCoop)
                        CoopItemMoves(interior);

                    building.modData[upgradeKey] = currentLevel;
                }
                return true;
            });
        }

        private static List<(Vector2 tile, StardewValley.Object obj)> SpiralSearch(GameLocation location, string qualifiedID, Vector2 center, int maxRadius)
        {
            var results = new List<(Vector2, StardewValley.Object)>();

            for (int radius = 0; radius <= maxRadius; radius++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        if (Math.Abs(dx) != radius && Math.Abs(dy) != radius)
                            continue;

                        Vector2 tile = new Vector2(center.X + dx, center.Y + dy);
                        if (location.objects.TryGetValue(tile, out StardewValley.Object obj) && obj.QualifiedItemId == qualifiedID)
                        {
                            results.Add((tile, obj));
                        }
                    }
                }
            }

            return results;
        }

        private static Vector2 LandingPadRect(GameLocation location, Rectangle landingPad)
        {
            for (int y = landingPad.Top; y < landingPad.Bottom; y++)
            {
                for (int x = landingPad.Left; x < landingPad.Right; x++)
                {
                    Vector2 candidate = new Vector2(x, y);
                    if (!location.IsTileBlockedBy(candidate, CollisionMask.Objects | CollisionMask.Furniture))
                    {
                        return candidate;
                    }
                }
            }
            return Vector2.Zero;
        }

        private static void BarnItemMoves(GameLocation interior)
        {
            if (interior.map == null) return;

            var namedDestinations = new Dictionary<string, Vector2>
            {
                { "(BC)99",  new Vector2( 4,  3) },
                { "(BC)104", new Vector2(23,  3) },
                { "(BC)165", new Vector2(10, 16) },
                { "(BC)272", new Vector2(17, 16) }
            };

            //var spawnIfMissing = new HashSet<string> { "(BC)104", "(BC)165", "(BC)272" };
            //   **is it possible to gate this for only Premium buildings and exclude the spawn in DeluxePlus buildings???

            //var haySlots = new List<Rectangle>
            //   **this doesn't need to be a list anymore, it's just a single rectangle...
        }

        private static void CoopItemMoves(GameLocation interior)
        {
            if (interior.map == null) return;

            var namedDestinations = new Dictionary<string, Vector2>
            {
                { "(BC)99",  new Vector2(22,  3) },
                { "(BC)101", new Vector2( 3,  3) },
                { "(BC)104", new Vector2(23,  3) },
                { "(BC)165", new Vector2(10, 12) },
                { "(BC)272", new Vector2(15, 12) }
            };
        }

        [HarmonyPatch(typeof(Building), nameof(Building.doesTileHaveProperty))]
        public static class PremiumBarnDoorCursorPatch
        {
            public static void Postfix(Building __instance, int tile_x, int tile_y, string property_name, string layer_name, ref string property_value, ref bool __result)
            {
                if (__instance.buildingType.Value == PremiumBarn && __instance.daysOfConstructionLeft.Value <= 0)
                {
                    var interior = __instance.GetIndoors();
                    if (tile_x == __instance.tileX.Value + __instance.humanDoor.X + 8 &&
                        tile_y == __instance.tileY.Value + __instance.humanDoor.Y &&
                        interior != null)
                    {
                        if (property_name == "Action")
                        {
                            property_value = "woof";
                            __result = true;
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Building), nameof(Building.doAction))]
        public static class PremiumBarnDoorPatch
        {
            public static void Postfix(Building __instance, Vector2 tileLocation, Farmer who, ref bool __result)
            {
                if (who.ActiveObject != null && who.ActiveObject.IsFloorPathItem() && who.currentLocation != null && !who.currentLocation.terrainFeatures.ContainsKey(tileLocation))
                    return;

                if (__instance.buildingType.Value == PremiumBarn && __instance.daysOfConstructionLeft.Value <= 0)
                {
                    var interior = __instance.GetIndoors();
                    if (tileLocation.X == __instance.tileX.Value + __instance.humanDoor.X + 8 &&
                        tileLocation.Y == __instance.tileY.Value + __instance.humanDoor.Y &&
                        interior != null)
                    {
                        if (who.mount != null)
                        {
                            Game1.showRedMessage(Game1.content.LoadString("Strings\\Buildings:DismountBeforeEntering"));
                            __result = false;
                            return;
                        }
                        if (who.team.demolishLock.IsLocked())
                        {
                            Game1.showRedMessage(Game1.content.LoadString("Strings\\Buildings:CantEnter"));
                            __result = false;
                            return;
                        }
                        if (__instance.OnUseHumanDoor(who))
                        {
                            who?.currentLocation?.playSound("doorClose", tileLocation);
                            bool isStructure = __instance.indoors.Value != null;
                            Game1.warpFarmer(interior.NameOrUniqueName, interior.warps[1].X, interior.warps[1].Y - 1, Game1.player.FacingDirection, isStructure);
                        }

                        __result = true;
                        return;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Building), nameof(Building.updateInteriorWarps))]
        public static class PremiumBarnWarpPatch
        {
            public static void Postfix(Building __instance, GameLocation interior)
            {
                if (__instance.buildingType.Value != PremiumBarn)
                    return;
                if (interior == null || interior.warps.Count < 2)
                    return;

                var w = interior.warps[1];
                interior.warps[1] = new(w.X, w.Y, w.TargetName, w.TargetX + 8, w.TargetY, w.flipFarmer.Value, w.npcOnly.Value);
            }
        }

        [HarmonyPatch(typeof(Utility), "_HasBuildingOrUpgrade")]
        public static class UtilityHasCoopBarnPatch
        {
            public static void Postfix(GameLocation location, string buildingId, ref bool __result)
            {
                if (__result) return;

                if (buildingId == "Barn" || buildingId == "Big Barn" || buildingId == "Deluxe Barn")
                {
                    if (location.getNumberBuildingsConstructed(PremiumBarn) > 0 ||
                        location.getNumberBuildingsConstructed(DeluxePlusBarn) > 0)
                        __result = true;
                }
                else if (buildingId == "Coop" || buildingId == "Big Coop" || buildingId == "Deluxe Coop")
                {
                    if (location.getNumberBuildingsConstructed(PremiumCoop) > 0 ||
                        location.getNumberBuildingsConstructed(DeluxePlusCoop) > 0)
                        __result = true;
                }
            }
        }

        [HarmonyPatch(typeof(Building), nameof(Building.InitializeIndoor))]
        public static class BuildingAutoGrabberFix
        {
            public static void Postfix(Building __instance, BuildingData data, bool forConstruction, bool forUpgrade)
            {
                if (!forConstruction)
                    return;
                if (__instance.buildingType.Value != PremiumBarn &&
                    __instance.buildingType.Value != PremiumCoop &&
                    __instance.buildingType.Value != DeluxePlusBarn &&
                    __instance.buildingType.Value != DeluxePlusCoop)
                    return;

                foreach (var obj in __instance.indoors.Value.Objects.Values)
                {
                    if (obj.QualifiedItemId == "(BC)165" && obj.heldObject.Value == null)
                        obj.heldObject.Value = new Chest();
                }
            }
        }
    }
}