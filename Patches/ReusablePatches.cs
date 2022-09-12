﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.TerrainFeatures;

using static PlacementPlus.ModState;
using static PlacementPlus.Utility.Utility;

using Object = StardewValley.Object;

namespace PlacementPlus.Patches
{
    [HarmonyPatch]
    internal class ReusablePatches
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            var assembly = Assembly.Load("Stardew Valley").GetTypes();
            
            // Get MethodInfo for each 'CheckForAction()' in subclasses of StardewValley.Object.
            var checkForActionEnumerable = assembly
                .Where(t => t.IsSubclassOf(typeof(Object)))
                .Select(m => m.GetMethod(nameof(Object.checkForAction)));

            // Get MethodInfo for each 'doAction()' in subclasses of StardewValley.Building.
            var doActionEnumerable = assembly
                .Where(t => t.IsSubclassOf(typeof(Building)))
                .Select(m => m.GetMethod(nameof(Building.doAction)));

            // Combining all gathered target MethodInfo entries with some additional ones.
            return checkForActionEnumerable.Concat(doActionEnumerable).Concat(new [] {
                AccessTools.Method(typeof(GameLocation), nameof(GameLocation.performAction)), 
                AccessTools.Method(typeof(ShippingBin), nameof(ShippingBin.leftClicked)),
            });
        }

        
        
        // TODO: Interactable objects shouldn't trigger until after left click is released--but they do atm!
        /// <summary> Alters the requirements for when interactable objects/components can be interacted with. </summary>
        [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
        private static bool Prefix(ref bool __result, MethodBase __originalMethod) 
        {
            try 
            {
                // Only do new checks if the player is holding the use tool button and is holding an item and the cursor
                // tile is placeable. We must use 'didPlayerJustLeftClick()' as modState.holdingToolButton does not
                // cover the initial button press.
                var holdingToolButton = Game1.didPlayerJustLeftClick() || ModState.holdingToolButton;
                if (!holdingToolButton || heldItem == null) return true; // Run original logic.
                
                
                var sameFlooring = DoesTileHaveFlooring(terrainFeatures, cursorTile) && 
                                   IsItemTargetFlooring(heldItem, (Flooring) terrainFeatures[cursorTile]);
                // For fences, we allow interaction if the player is not holding a fence/flooring OR the player-held 
                // item is the same as the tile fence/flooring respectively.
                if (__originalMethod.DeclaringType == typeof(Fence))
                {
                    var sameFences = tileObject is Fence fence && IsItemTargetFence(heldItem, fence);
                    __result = !(IsItemFlooring(heldItem) || IsItemFence(heldItem)) 
                               || sameFlooring 
                               || sameFences;
                }
                // For all other objects/buildings, we allow interaction if the player-held flooring is the same as the
                // tile flooring.
                else
                {
                    __result = !IsItemFlooring(heldItem) || sameFlooring;
                }
                
                return __result; // Run original logic if it was determined that the action *should* occur.
            } catch (Exception e) {
                Monitor.Log($"Failed in {nameof(ReusablePatches)}:\n{e}", LogLevel.Error);
                return true; // Run original logic.
            }
        }
    }
}