using System;
using System.Collections.Generic;
using Assets.Scripts.Networks;
using Assets.Scripts.Objects.Pipes;
using Assets.Scripts.Serialization;
using HarmonyLib;

namespace LogicFilterBridge;

[HarmonyPatch(typeof(CableNetwork), "RefreshPowerAndDataDeviceLists")]
internal static class RefreshPowerAndDataDeviceListsPatch
{
    [HarmonyPostfix]
    private static void Postfix(CableNetwork __instance)
    {
        BridgeNetworkManager.PopulateOutputNetwork(__instance);
    }
}

[HarmonyPatch(typeof(CableNetwork), nameof(CableNetwork.DirtyPowerAndDataDeviceLists))]
internal static class DirtyPowerAndDataDeviceListsPatch
{
    [HarmonyPostfix]
    private static void Postfix(CableNetwork __instance)
    {
        BridgeNetworkManager.PropagateDirty(__instance);
    }
}

[HarmonyPatch(typeof(CableNetwork), nameof(CableNetwork.DirtyDataDeviceList))]
internal static class DirtyDataDeviceListPatch
{
    [HarmonyPostfix]
    private static void Postfix(CableNetwork __instance)
    {
        BridgeNetworkManager.HandleDataOnlyDirty(__instance);
    }
}

[HarmonyPatch(typeof(CableNetwork), nameof(CableNetwork.OnDeviceNameChanged))]
internal static class CableNetworkDeviceNameChangedPatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        // Renames are infrequent. Invalidating all Name-mode outputs also covers names
        // reaching a bridge through vanilla data transmitters/receivers.
        BridgeNetworkManager.InvalidateAllNameFilters();
    }
}

[HarmonyPatch(typeof(XmlSaveLoad), nameof(XmlSaveLoad.AddExtraTypes))]
internal static class XmlSaveLoadExtraTypesPatch
{
    [HarmonyPostfix]
    private static void Postfix(ref List<Type> extraTypes)
    {
        if (extraTypes != null && !extraTypes.Contains(typeof(LogicFilterBridgeSaveData)))
        {
            extraTypes.Add(typeof(LogicFilterBridgeSaveData));
        }
    }
}
