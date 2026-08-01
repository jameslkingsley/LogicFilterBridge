using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Assets.Scripts;
using Assets.Scripts.Inventory;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Electrical;
using Assets.Scripts.Objects.Items;
using Assets.Scripts.UI.ImGuiUi;
using Reagents;
using StationeersMods.Interface;
using UnityEngine;

namespace LogicFilterBridge;

internal static class PrefabRegistrar
{
    internal const string StructurePrefabName = "StructureLogicFilterBridge";
    internal const string KitPrefabName = "ItemKitLogicFilterBridge";

    private const string SourceStructurePrefabName = "StructureLogicMirror";
    private const string SourceKitPrefabName = "ItemKitLogicInputOutput";

    private static readonly object RegistrationLock = new object();
    private static bool _registered;
    private static GameObject _templateRoot;

    internal static void TryRegister(ModBehaviour owner)
    {
        lock (RegistrationLock)
        {
            if (_registered || Prefab.Find(StructurePrefabName) != null)
            {
                _registered = true;
                return;
            }

            LogicMirror sourceStructure = Prefab.Find(SourceStructurePrefabName) as LogicMirror;
            MultiConstructor sourceKit = Prefab.Find(SourceKitPrefabName) as MultiConstructor;
            if (sourceStructure == null || sourceKit == null || sourceStructure.BuildStates == null || sourceStructure.BuildStates.Count == 0)
            {
                return;
            }

            try
            {
                _templateRoot = new GameObject("LogicFilterBridgeTemplates");
                _templateRoot.SetActive(false);
                UnityEngine.Object.DontDestroyOnLoad(_templateRoot);

                LogicFilterBridge customStructure = CloneStructure(owner, sourceStructure);
                MultiConstructor customKit = CloneKit(owner, sourceKit, customStructure);
                ReplaceConstructionKit(customStructure, sourceKit, customKit);

                Prefab.RegisterExisting(customStructure);
                Prefab.RegisterExisting(customKit);

                RegisterLocalization();
                RegisterInventoryAndCreativeEntries(customKit);
                RegisterElectronicsPrinterRecipe(customKit);

                _registered = true;
                LogicFilterBridgeMod.Log($"Registered {KitPrefabName} and {StructurePrefabName}.");
            }
            catch (Exception exception)
            {
                LogicFilterBridgeMod.LogError("Prefab registration failed", exception);
            }
        }
    }

    private static LogicFilterBridge CloneStructure(ModBehaviour owner, LogicMirror source)
    {
        bool wasActive = source.gameObject.activeSelf;
        source.gameObject.SetActive(false);
        GameObject clone;
        try
        {
            clone = owner.Instantiate(source.gameObject);
        }
        finally
        {
            source.gameObject.SetActive(wasActive);
        }

        if (clone == null)
        {
            throw new InvalidOperationException("LaunchPad could not clone the Logic Mirror prefab.");
        }

        clone.SetActive(false);
        clone.name = StructurePrefabName;
        clone.transform.SetParent(_templateRoot.transform, false);

        LogicMirror oldComponent = clone.GetComponent<LogicMirror>();
        LogicFilterBridge newComponent = owner.AddComponent<LogicFilterBridge>(clone);
        if (oldComponent == null || newComponent == null)
        {
            throw new InvalidOperationException("Could not replace the cloned LogicMirror component.");
        }

        ReflectionClone.CopyMatchingFields(oldComponent, newComponent);
        ReflectionClone.ReplaceComponentReferences(clone, oldComponent, newComponent);
        UnityEngine.Object.DestroyImmediate(oldComponent);

        newComponent.PrefabName = StructurePrefabName;
        newComponent.PrefabHash = Animator.StringToHash(StructurePrefabName);
        clone.tag = "NotSpawnable";
        clone.SetActive(true);
        return newComponent;
    }

    private static MultiConstructor CloneKit(ModBehaviour owner, MultiConstructor source, LogicFilterBridge structure)
    {
        bool wasActive = source.gameObject.activeSelf;
        source.gameObject.SetActive(false);
        GameObject clone;
        try
        {
            clone = owner.Instantiate(source.gameObject);
        }
        finally
        {
            source.gameObject.SetActive(wasActive);
        }

        if (clone == null)
        {
            throw new InvalidOperationException("LaunchPad could not clone the Logic I/O kit prefab.");
        }

        clone.SetActive(false);
        clone.name = KitPrefabName;
        clone.transform.SetParent(_templateRoot.transform, false);

        MultiConstructor kit = clone.GetComponent<MultiConstructor>();
        if (kit == null)
        {
            throw new InvalidOperationException("The cloned Logic I/O kit has no MultiConstructor component.");
        }

        kit.PrefabName = KitPrefabName;
        kit.PrefabHash = Animator.StringToHash(KitPrefabName);
        kit.Constructables = new List<Structure> { structure };
        kit.LastSelectedIndex = 0;
        clone.tag = "Untagged";

        Thing.Deregister(kit);
        clone.SetActive(true);
        return kit;
    }

    private static void ReplaceConstructionKit(Structure structure, MultiConstructor sourceKit, MultiConstructor customKit)
    {
        foreach (BuildState buildState in structure.BuildStates)
        {
            if (buildState?.Tool == null)
            {
                continue;
            }

            ToolUse clonedTool = new ToolUse();
            ReflectionClone.CopyAllFields(buildState.Tool, clonedTool);

            if (IsSourceKit(clonedTool.ToolEntry, sourceKit))
            {
                clonedTool.ToolEntry = customKit;
                clonedTool.EntryQuantity = 1;
            }

            if (IsSourceKit(clonedTool.ToolEntry2, sourceKit))
            {
                clonedTool.ToolEntry2 = customKit;
                clonedTool.EntryQuantity2 = 1;
            }

            if (IsSourceKit(clonedTool.ToolExit, sourceKit))
            {
                clonedTool.ToolExit = customKit;
                clonedTool.ExitQuantity = 1;
            }

            buildState.Tool = clonedTool;
        }
    }

    private static bool IsSourceKit(Thing candidate, MultiConstructor sourceKit)
    {
        return candidate != null && (ReferenceEquals(candidate, sourceKit) || candidate.PrefabName == sourceKit.PrefabName);
    }

    private static void RegisterLocalization()
    {
        Localization.LocalizationThingDat kitLocalization = new Localization.LocalizationThingDat
        {
            PrefabName = "Kit (Logic Filter Bridge)",
            Description = "Contains the components needed to construct a Logic Filter Bridge."
        };
        Localization.LocalizationThingDat structureLocalization = new Localization.LocalizationThingDat
        {
            PrefabName = "Logic Filter Bridge",
            Description = "A configurable bridge that exposes selected devices from one logic network to another."
        };

        AddLocalizationEntry("ThingLocalized", Animator.StringToHash(KitPrefabName), kitLocalization);
        AddLocalizationEntry("ThingLocalized", Animator.StringToHash(StructurePrefabName), structureLocalization);
        AddLocalizationEntry("FallbackThingsLocalized", Animator.StringToHash(KitPrefabName), kitLocalization);
        AddLocalizationEntry("FallbackThingsLocalized", Animator.StringToHash(StructurePrefabName), structureLocalization);
    }

    private static void AddLocalizationEntry(string fieldName, int hash, Localization.LocalizationThingDat localization)
    {
        FieldInfo field = typeof(Localization).GetField(fieldName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (field?.GetValue(null) is IDictionary dictionary)
        {
            dictionary[hash] = localization;
        }
    }

    private static void RegisterInventoryAndCreativeEntries(MultiConstructor kit)
    {
        if (InventoryManager.DynamicThingPrefabs != null && !InventoryManager.DynamicThingPrefabs.Contains(KitPrefabName))
        {
            InventoryManager.DynamicThingPrefabs.Add(KitPrefabName);
        }

        if (!GameManager.IsBatchMode)
        {
            ImguiCreativeSpawnMenu.AddDynamicItem(kit);
        }

        ConstructionCursorRegistration.EnsureRegistered(InventoryManager.Instance);
    }

    private static void RegisterElectronicsPrinterRecipe(MultiConstructor kit)
    {
        Recipe recipe = new Recipe
        {
            Time = 10f,
            Energy = 1000f,
            Copper = 1.0,
            Gold = 1.0
        };

        WorldManager.RecipeData recipeData = new WorldManager.RecipeData
        {
            PrefabName = KitPrefabName,
            Recipe = recipe,
            RecipeTier = MachineTier.Undefined,
            Output = 1f
        };

        ElectronicsPrinter.RecipeComparable.AddRecipe(recipeData, null);
        ElectronicsPrinter.RecipeComparable.GenerateRecipieList();
        SynchronizeRecipeReferenceList(ElectronicReader.ElectronicsPrinterRecipes, kit, recipe);
        SynchronizeRecipeReferenceList(ElectronicReader.AllRecipes, kit, recipe);
    }

    private static void SynchronizeRecipeReferenceList(
        List<RecipeReference> recipes,
        MultiConstructor kit,
        Reagents.Recipe recipe)
    {
        for (var index = recipes.Count - 1; index >= 0; index--)
        {
            var existing = recipes[index];
            if (existing?.DynamicThing?.PrefabHash == kit.PrefabHash)
            {
                recipes.RemoveAt(index);
            }
        }

        recipes.Add(new RecipeReference(kit, recipe, "StructureElectronicsPrinter"));
    }
}
