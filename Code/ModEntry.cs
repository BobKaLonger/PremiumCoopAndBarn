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
        public static IContentPack? cpPack;
        internal const string PremiumCP = "bobkalonger.PremiumCoopAndBarnCP_";
        internal const string PremiumBarn = $"{PremiumCP}PremiumBarn";
        internal const string PremiumCoop = $"{PremiumCP}PremiumCoop";
        public override void Entry(IModHelper helper)
        {
            modInstance = this;

            var mi = Helper.ModRegistry.Get("bobkalonger.PremiumCoopAndBarnCP");
            if (mi != null)
                cpPack = mi.GetType().GetProperty("ContentPack")?.GetValue(mi) as IContentPack;

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
                    var ultimateLightBL = new Point(b.tileX.Value + 3, b.tileY.Value + 3);
                    var ll = new LightSource($"{PremiumCP}BarnLight_{b.tileX.Value}_{b.tileY.Value}_L", 4, ultimateLightBL.ToVector2() * Game1.tileSize, 1f, Color.Black, LightSource.LightContext.None);
                    Game1.currentLightSources.Add(ll.Id, ll);

                    var ultimateLightBR = new Point(b.tileX.Value + 8, b.tileY.Value + 3);
                    var lr = new LightSource($"{PremiumCP}BarnLight_{b.tileX.Value}_{b.tileY.Value}_R", 4, ultimateLightBR.ToVector2() * Game1.tileSize, 1f, Color.Black, LightSource.LightContext.None);
                    Game1.currentLightSources.Add(lr.Id, lr);
                }

                if (b.buildingType.Value == PremiumCoop)
                {
                    var ultimateLightC = new Point(b.tileX.Value + 6, b.tileY.Value + 2);
                    var lc = new LightSource($"{PremiumCP}CoopLight_{b.tileX.Value}_{b.tileY.Value}", 4, ultimateLightC.ToVector2() * Game1.tileSize, 1f, Color.Black, LightSource.LightContext.None);
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
                if (building.buildingType.Value is PremiumBarn or PremiumCoop)
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

        private string? _cachedBarnFloorConfig = null;
        private string GetBarnFloorConfig()
        {
            if (_cachedBarnFloorConfig != null) return _cachedBarnFloorConfig;
            var config = cpPack?.ReadJsonFile<Dictionary<string, string>>("config.json");
            if (config != null && config.TryGetValue("Barn Floor", out string? value))
                _cachedBarnFloorConfig = value;
            return _cachedBarnFloorConfig ?? "Clean";
        }

        private string? _cachedCoopFloorConfig = null;
        private string GetCoopFloorConfig()
        {
            if (_cachedCoopFloorConfig != null) return _cachedCoopFloorConfig;
            var config = cpPack?.ReadJsonFile<Dictionary<string, string>>("config.json");
            if (config != null && config.TryGetValue("Coop Floor", out string? value))
                _cachedCoopFloorConfig = value;
            return _cachedCoopFloorConfig ?? "Clean";
        }

        [HarmonyPatch(typeof(Building), nameof(Building.InitializeIndoor))]
        public static class BuildingInitializeIndoorPrefix
        {
            public static void Prefix(Building __instance)
            {
                if (__instance.buildingType.Value is not (PremiumBarn or PremiumCoop))
                    return;

                var interior = __instance.indoors.Value;
                if (interior == null) return;

                if (string.IsNullOrEmpty(interior.mapPath.Value) || interior.mapPath.Value.Contains("{{"))
                {
                    interior.mapPath.Value = __instance.buildingType.Value switch
                    {
                        PremiumBarn => $"Maps/SVE_{modInstance!.GetBarnFloorConfig()}_UltimateBarn",
                        PremiumCoop => $"Maps/SVE_{modInstance!.GetCoopFloorConfig()}_UltimateCoop",
                        _ => interior.mapPath.Value
                    };
                }
            }
        }

        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            Utility.ForEachBuilding(building =>
            {
                if (building.buildingType.Value is PremiumBarn or PremiumCoop)
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

        [HarmonyPatch(typeof(Building), nameof(Building.FinishConstruction))]
        public static class InstantBuildingConstructionPatch
        {
            public static void Postfix(Building __instance)
            {
                if (__instance.buildingType.Value is not (PremiumBarn or PremiumCoop))
                    return;

                //double check which indoor items need to be spawned
            }
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
                            property_value = "meow";
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
                string? toCheck = null;
                if (buildingId == "Barn" || buildingId == "Big Barn" || buildingId == "Deluxe Barn")
                    toCheck = PremiumBarn;
                else if (buildingId == "Coop" || buildingId == "Big Coop" || buildingId == "Deluxe Coop")
                    toCheck = PremiumCoop;

                if (!__result && toCheck != null)
                {
                    if (location.getNumberBuildingsConstructed(toCheck) > 0)
                    {
                        __result = true;
                    }
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
                    __instance.buildingType.Value != PremiumCoop)
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