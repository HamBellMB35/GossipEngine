#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace TownsPeople.CustomEditor
{
    /// <summary>
    /// Shared auto-wiring scanner used by "Use Existing Prefab" mode on the Reputation Bar UI
    /// and Dialogue Menu UI wizards. Given a root GameObject (a developer-supplied custom UI
    /// prefab/instance) and a list of expected sub-elements, walks the hierarchy looking for a
    /// child whose name contains ALL of a field's NameHints (and none of its ExcludeHints) AND
    /// carries the expected component type, then wires it into the target script via
    /// SerializedProperty. Never fails hard on a miss — reports what it couldn't find so the
    /// developer can finish wiring the rest by hand in the Inspector.
    ///
    /// Matching is name + type based, not psychic — for a good hit rate, name your custom
    /// prefab's elements with recognizable keywords (see each wizard's HelpBox for the exact
    /// hints it looks for).
    /// </summary>
    public static class UIElementAutoWirer
    {
        public struct FieldTarget
        {
            public string PropertyName;   // Serialized field name on the target script, e.g. "_leaveButton"
            public Type ExpectedType;     // Component type expected (or typeof(GameObject))
            public string[] NameHints;    // ALL must appear as substrings (case-insensitive) in the candidate's name
            public string[] ExcludeHints; // If ANY appears in the name, the candidate is skipped. Optional.
            public bool ExtractAsPrefab;  // If true: save the match as its OWN prefab asset and remove the
                                          // live instance from the hierarchy (for templates like an option
                                          // button or a per-faction row, instantiated at runtime rather
                                          // than existing as a visible sibling).

            public FieldTarget(string propertyName, Type expectedType, string[] nameHints, bool extractAsPrefab = false)
            {
                PropertyName = propertyName;
                ExpectedType = expectedType;
                NameHints = nameHints;
                ExcludeHints = null;
                ExtractAsPrefab = extractAsPrefab;
            }
        }

        public class Result
        {
            public List<string> Wired = new List<string>();
            public List<string> Missing = new List<string>();
            public Dictionary<string, string> ExtractedPrefabPaths = new Dictionary<string, string>();
        }

        /// <summary>
        /// Scans rootInstance's hierarchy (including inactive children) for each FieldTarget and
        /// wires whatever it finds onto targetComponent via SerializedObject/SerializedProperty
        /// (so it respects prefab overrides and Undo, same as the rest of these wizards).
        /// </summary>
        public static Result AutoWire(GameObject rootInstance, Component targetComponent, List<FieldTarget> fields, string extractedPrefabFolder)
        {
            var result = new Result();
            Transform[] allChildren = rootInstance.GetComponentsInChildren<Transform>(true);
            SerializedObject serializedTarget = new SerializedObject(targetComponent);

            foreach (FieldTarget field in fields)
            {
                SerializedProperty prop = serializedTarget.FindProperty(field.PropertyName);
                if (prop == null) continue; // Field doesn't exist on this script version — skip quietly.

                Transform match = FindBestMatch(allChildren, field.ExpectedType, field.NameHints, field.ExcludeHints, rootInstance.transform);
                if (match == null)
                {
                    result.Missing.Add(field.PropertyName);
                    continue;
                }

                if (field.ExtractAsPrefab)
                {
                    EnsureFolderExists(extractedPrefabFolder);
                    string prefabPath = AssetDatabase.GenerateUniqueAssetPath(
                        $"{extractedPrefabFolder}/{rootInstance.name}_{field.PropertyName.TrimStart('_')}.prefab");
                    GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(match.gameObject, prefabPath);
                    UnityEngine.Object valueToAssign = field.ExpectedType == typeof(GameObject)
                        ? savedPrefab
                        : savedPrefab.GetComponent(field.ExpectedType);

                    prop.objectReferenceValue = valueToAssign;
                    result.ExtractedPrefabPaths[field.PropertyName] = prefabPath;
                    result.Wired.Add(field.PropertyName);

                    // It was a template, not meant to appear as a live sibling — remove it from
                    // the hierarchy now that it's saved as its own asset.
                    UnityEngine.Object.DestroyImmediate(match.gameObject);
                }
                else
                {
                    UnityEngine.Object valueToAssign = field.ExpectedType == typeof(GameObject)
                        ? match.gameObject
                        : match.GetComponent(field.ExpectedType);

                    prop.objectReferenceValue = valueToAssign;
                    result.Wired.Add(field.PropertyName);
                }
            }

            serializedTarget.ApplyModifiedProperties();
            return result;
        }

        private static Transform FindBestMatch(Transform[] candidates, Type expectedType, string[] nameHints, string[] excludeHints, Transform root)
        {
            Transform best = null;
            int bestDepth = int.MaxValue;

            foreach (Transform candidate in candidates)
            {
                if (expectedType != typeof(GameObject) && candidate.GetComponent(expectedType) == null) continue;

                string nameLower = candidate.name.ToLowerInvariant();
                if (!nameHints.All(h => nameLower.Contains(h.ToLowerInvariant()))) continue;
                if (excludeHints != null && excludeHints.Any(h => nameLower.Contains(h.ToLowerInvariant()))) continue;

                // Multiple matches: prefer the shallowest one (closer to root = more likely the
                // "real" element rather than an incidentally-named nested child).
                int depth = GetDepth(candidate, root);
                if (depth < bestDepth)
                {
                    bestDepth = depth;
                    best = candidate;
                }
            }

            return best;
        }

        private static int GetDepth(Transform t, Transform root)
        {
            int depth = 0;
            while (t != null && t != root) { depth++; t = t.parent; }
            return depth;
        }

        private static void EnsureFolderExists(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;
            string[] parts = folderPath.Split('/');
            string currentPath = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string nextPath = $"{currentPath}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(nextPath)) AssetDatabase.CreateFolder(currentPath, parts[i]);
                currentPath = nextPath;
            }
        }

        public static string BuildSummaryMessage(Result result, int totalFields)
        {
            string message = $"Wired: {result.Wired.Count}/{totalFields}";
            if (result.Missing.Count > 0)
            {
                message += $"\n\nCould not find a match for:\n\u2022 {string.Join("\n\u2022 ", result.Missing.Distinct())}\n\nAssign these manually in the Inspector.";
            }
            return message;
        }
    }
}
#endif