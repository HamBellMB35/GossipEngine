#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using TownsPeople.Data;

namespace TownsPeople.EditorTools
{
    /// <summary>
    /// Automatically relocates newly-created ScriptableObject assets of known types into their
    /// designated System Data folders, regardless of which folder was selected in the Project
    /// window when Assets > Create > TownsPeople Creator > ... was used.
    ///
    /// Extend _typeToFolder below to auto-organize additional concrete types. Base types (e.g.
    /// FlockTriggerCondition, FlockReturnCondition) are checked via IsAssignableFrom too, so any
    /// FUTURE custom subclass a user writes is automatically organized the same way, with no
    /// edit needed here — exactly the "user can add more events" extensibility the Flocking
    /// trigger/condition system itself was built around.
    /// </summary>
    public class AssetAutoOrganizer : AssetPostprocessor
    {
        private const string SystemDataRoot = "Assets/_Project/System Data";

        private static readonly Dictionary<System.Type, string> _typeToFolder = new Dictionary<System.Type, string>
        {
            { typeof(RumorTemplate), SystemDataRoot + "/Gossip" },
            { typeof(GossipToneData), SystemDataRoot + "/Gossip" },
            { typeof(GeneralRumorResponseLibrary), SystemDataRoot + "/Gossip" },
            { typeof(NPCArchetypeConfiguration), SystemDataRoot + "/NPC Archetypes" },
            // Base types — matched via IsAssignableFrom in ResolveTargetFolder(), so every
            // concrete subclass (the two shipped defaults AND any future custom one) lands here
            // without this dictionary needing a new entry per subclass.
            { typeof(FlockTriggerCondition), SystemDataRoot + "/Flocking" },
            { typeof(FlockReturnCondition), SystemDataRoot + "/Flocking" },
        };

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            foreach (string assetPath in importedAssets)
            {
                if (!assetPath.EndsWith(".asset")) continue;

                Object asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
                if (asset == null) continue;

                string targetFolder = ResolveTargetFolder(asset.GetType());
                if (targetFolder == null) continue;

                string currentFolder = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
                if (currentFolder == targetFolder) continue; // Already in the right place.

                EnsureFolderExists(targetFolder);

                string fileName = Path.GetFileName(assetPath);
                string newPath = AssetDatabase.GenerateUniqueAssetPath($"{targetFolder}/{fileName}");

                string error = AssetDatabase.MoveAsset(assetPath, newPath);
                if (!string.IsNullOrEmpty(error))
                {
                    Debug.LogWarning($"<color=orange>[AssetAutoOrganizer]</color> Could not move '{assetPath}' to '{newPath}': {error}");
                }
            }
        }

        /// <summary>Exact type match first, then base-type match (IsAssignableFrom) — the latter is what auto-covers future custom trigger/condition subclasses.</summary>
        private static string ResolveTargetFolder(System.Type assetType)
        {
            if (assetType == null) return null;
            if (_typeToFolder.TryGetValue(assetType, out string exact)) return exact;

            foreach (KeyValuePair<System.Type, string> pair in _typeToFolder)
            {
                if (pair.Key.IsAssignableFrom(assetType)) return pair.Value;
            }
            return null;
        }

        private static void EnsureFolderExists(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;

            string[] parts = folderPath.Split('/');
            string current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }
    }
}
#endif