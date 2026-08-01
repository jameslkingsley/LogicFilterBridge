using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace LogicFilterBridge;

internal static class ReflectionClone
{
    internal static void CopyMatchingFields(Component source, Component destination)
    {
        Dictionary<string, FieldInfo> destinationFields = new Dictionary<string, FieldInfo>(StringComparer.Ordinal);
        for (Type type = destination.GetType(); type != null && type != typeof(Component) && type != typeof(MonoBehaviour); type = type.BaseType)
        {
            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                destinationFields[field.Name] = field;
            }
        }

        for (Type type = source.GetType(); type != null && type != typeof(Component) && type != typeof(MonoBehaviour); type = type.BaseType)
        {
            foreach (FieldInfo sourceField in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (!destinationFields.TryGetValue(sourceField.Name, out FieldInfo destinationField) || !destinationField.FieldType.IsAssignableFrom(sourceField.FieldType))
                {
                    continue;
                }

                try
                {
                    destinationField.SetValue(destination, sourceField.GetValue(source));
                }
                catch (Exception)
                {
                    // Unity and compiler generated backing fields can be read-only.
                }
            }
        }
    }

    internal static void CopyAllFields(object source, object destination)
    {
        for (Type type = source.GetType(); type != null && type != typeof(object); type = type.BaseType)
        {
            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                try
                {
                    field.SetValue(destination, field.GetValue(source));
                }
                catch (Exception)
                {
                    // Ignore fields that the runtime does not allow us to copy.
                }
            }
        }
    }

    internal static void ReplaceComponentReferences(GameObject root, Component oldComponent, Component newComponent)
    {
        HashSet<object> visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        foreach (Component component in root.GetComponentsInChildren<Component>(true))
        {
            if (component != null && component != oldComponent)
            {
                ReplaceReferences(component, oldComponent, newComponent, visited, 0);
            }
        }
    }

    private static void ReplaceReferences(object value, Component oldComponent, Component newComponent, HashSet<object> visited, int depth)
    {
        if (value == null || depth > 5 || !visited.Add(value))
        {
            return;
        }

        Type type = value.GetType();
        if (type.IsPrimitive || type.IsEnum || type == typeof(string) || typeof(Delegate).IsAssignableFrom(type))
        {
            return;
        }

        if (value is IList list)
        {
            for (int index = 0; index < list.Count; index++)
            {
                object item = list[index];
                if (ReferenceEquals(item, oldComponent))
                {
                    try
                    {
                        list[index] = newComponent;
                    }
                    catch (Exception)
                    {
                    }
                }
                else
                {
                    ReplaceReferences(item, oldComponent, newComponent, visited, depth + 1);
                }
            }
            return;
        }

        for (Type current = type; current != null && current != typeof(object); current = current.BaseType)
        {
            foreach (FieldInfo field in current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (field.IsStatic)
                {
                    continue;
                }

                object fieldValue;
                try
                {
                    fieldValue = field.GetValue(value);
                }
                catch (Exception)
                {
                    continue;
                }

                if (ReferenceEquals(fieldValue, oldComponent) && field.FieldType.IsAssignableFrom(newComponent.GetType()))
                {
                    try
                    {
                        field.SetValue(value, newComponent);
                    }
                    catch (Exception)
                    {
                    }
                }
                else if (!(fieldValue is UnityEngine.Object))
                {
                    ReplaceReferences(fieldValue, oldComponent, newComponent, visited, depth + 1);
                }
            }
        }
    }

    private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        internal static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();

        public new bool Equals(object left, object right) => ReferenceEquals(left, right);

        public int GetHashCode(object value) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
    }
}

