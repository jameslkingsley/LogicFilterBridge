using System;
using Assets.Scripts.Objects;
using HarmonyLib;
using StationeersMods.Interface;
using UnityEngine;

namespace LogicFilterBridge;

[StationeersMod(ModId, DisplayName, Version)]
public sealed class LogicFilterBridgeMod : ModBehaviour
{
    public const string ModId = "com.james.logicfilterbridge";
    public const string DisplayName = "Logic Filter Bridge";
    public const string Version = "0.1.0";

    private Harmony _harmony;

    public override void OnLoaded(ContentHandler contentHandler)
    {
        base.OnLoaded(contentHandler);

        try
        {
            _harmony = new Harmony(ModId);
            _harmony.PatchAll();

            Prefab.OnPrefabsLoaded += RegisterPrefabs;
            if (Prefab.AllPrefabs != null && Prefab.AllPrefabs.Count > 0)
            {
                RegisterPrefabs();
            }

            Log("Loaded and waiting for vanilla prefabs.");
        }
        catch (Exception exception)
        {
            LogError("Failed during mod initialization", exception);
        }
    }

    private void RegisterPrefabs()
    {
        PrefabRegistrar.TryRegister(this);
    }

    private void OnDestroy()
    {
        Prefab.OnPrefabsLoaded -= RegisterPrefabs;
        _harmony?.UnpatchSelf();
    }

    internal static void Log(string message)
    {
        Debug.unityLogger.Log(LogType.Log, $"[LogicFilterBridge] {message}");
    }

    internal static void LogWarning(string message)
    {
        Debug.unityLogger.Log(LogType.Warning, $"[LogicFilterBridge] {message}");
    }

    internal static void LogError(string message, Exception exception = null)
    {
        string details = exception == null ? message : $"{message}: {exception}";
        Debug.unityLogger.Log(LogType.Error, $"[LogicFilterBridge] {details}");
    }
}

