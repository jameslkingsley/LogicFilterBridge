using System;
using System.Collections.Generic;
using System.Reflection;
using Assets.Scripts.Inventory;
using Assets.Scripts.Objects;
using HarmonyLib;

namespace LogicFilterBridge;

internal static class ConstructionCursorRegistration
{
    internal static void EnsureRegistered(InventoryManager inventoryManager)
    {
        try
        {
            inventoryManager ??= InventoryManager.Instance;

            FieldInfo cursorsField = typeof(InventoryManager).GetField("_constructionCursors", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (!(cursorsField?.GetValue(null) is Dictionary<string, Structure> cursors) || cursors.ContainsKey(PrefabRegistrar.StructurePrefabName))
            {
                return;
            }

            FieldInfo parentField = typeof(InventoryManager).GetField("_constructionCursorParent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (inventoryManager == null || parentField?.GetValue(inventoryManager) == null)
            {
                return;
            }

            Structure structure = Prefab.Find(PrefabRegistrar.StructurePrefabName) as Structure;
            MethodInfo handler = typeof(InventoryManager).GetMethod("HandleStructurePrefab", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (structure == null || handler == null || (!handler.IsStatic && inventoryManager == null))
            {
                return;
            }

            handler.Invoke(handler.IsStatic ? null : inventoryManager, new object[] { structure });
            LogicFilterBridgeMod.Log("Registered the construction placement cursor.");
        }
        catch (Exception exception)
        {
            LogicFilterBridgeMod.LogWarning($"Could not register the construction cursor yet: {exception.Message}");
        }
    }
}

[HarmonyPatch(typeof(InventoryManager), "SetupConstructionCursors")]
internal static class SetupConstructionCursorsPatch
{
    [HarmonyPostfix]
    private static void Postfix(InventoryManager __instance)
    {
        ConstructionCursorRegistration.EnsureRegistered(__instance);
    }
}

[HarmonyPatch(typeof(InventoryManager), "UpdatePlacement", new Type[] { typeof(Structure) })]
internal static class UpdateStructurePlacementPatch
{
    [HarmonyPrefix]
    private static void Prefix()
    {
        ConstructionCursorRegistration.EnsureRegistered(InventoryManager.Instance);
    }
}

[HarmonyPatch(typeof(InventoryManager), "UpdatePlacement", new Type[] { typeof(Constructor) })]
internal static class UpdateConstructorPlacementPatch
{
    [HarmonyPrefix]
    private static void Prefix()
    {
        ConstructionCursorRegistration.EnsureRegistered(InventoryManager.Instance);
    }
}
