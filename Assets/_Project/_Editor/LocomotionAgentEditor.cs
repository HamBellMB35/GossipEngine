#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using TownsPeople.GamePlay;

namespace TownsPeople.CustomEditor
{
    /// <summary>
    /// Custom Inspector for LocomotionAgent. Draws every existing field normally
    /// (DrawDefaultInspector), then appends two sections:
    /// - Blend Tree: select a Blend Tree (auto-populated from the assigned Animator's
    ///   Controller) and sync a Multiplier field per motion in it — feeds LocomotionAgent's
    ///   PosePlaybackRates, delivered live to the Animator's State Speed > Multiplier >
    ///   Parameter binding at runtime. Only meaningful for the SAME tree driving Speed.
    /// - Individual Animation: select a standalone AnimatorState (not a Blend Tree) and edit
    ///   its native Speed field directly.
    /// </summary>
    [UnityEditor.CustomEditor(typeof(LocomotionAgent))]
    public class LocomotionAgentEditor : UnityEditor.Editor
    {
        private struct BlendTreeEntry
        {
            public string Label;
            public BlendTree Tree;
        }

        private struct AnimationStateEntry
        {
            public string Label;
            public AnimatorState State;
        }

        private int _selectedBlendTreeIndex;
        private int _selectedStateIndex;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Animation Speed Tuning (Editor Convenience)", EditorStyles.boldLabel);

            SerializedProperty animatorProp = serializedObject.FindProperty("_animator");
            Animator animator = animatorProp != null ? animatorProp.objectReferenceValue as Animator : null;

            AnimatorController controller = animator != null ? animator.runtimeAnimatorController as AnimatorController : null;

            if (controller == null)
            {
                EditorGUILayout.HelpBox("No AnimatorController resolved yet — assign an Animator above.", MessageType.Warning);
                return;
            }

            DrawBlendTreeSection(controller);
            EditorGUILayout.Space(6);
            DrawAnimationStateSection(controller);
        }

        private void DrawBlendTreeSection(AnimatorController controller)
        {
            List<BlendTreeEntry> blendTrees = CollectBlendTrees(controller);

            EditorGUILayout.LabelField("Blend Tree — Per-Pose Playback Rate", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Select the SAME Blend Tree that drives this NPC's Speed parameter (Locomotion). A different tree here won't correspond to anything at runtime — only the Locomotion tree's live blend position is measured.", MessageType.Info);

            if (blendTrees.Count == 0)
            {
                EditorGUILayout.HelpBox("No Blend Trees found in this Animator Controller.", MessageType.None);
                return;
            }

            string[] labels = blendTrees.Select(b => b.Label).ToArray();
            _selectedBlendTreeIndex = Mathf.Clamp(_selectedBlendTreeIndex, 0, blendTrees.Count - 1);
            int newIndex = EditorGUILayout.Popup("Select Blend Tree", _selectedBlendTreeIndex, labels);

            BlendTree tree = blendTrees[newIndex].Tree;
            LocomotionAgent agent = (LocomotionAgent)target;

            bool indexChanged = newIndex != _selectedBlendTreeIndex;
            bool syncClicked = GUILayout.Button("Sync Poses From This Blend Tree");
            _selectedBlendTreeIndex = newIndex;

            if (indexChanged || syncClicked)
            {
                SyncPosePlaybackRates(agent, tree);
            }

            EditorGUILayout.Space(4);

            SerializedProperty posesProp = serializedObject.FindProperty("_posePlaybackRates");
            if (posesProp.arraySize == 0)
            {
                EditorGUILayout.HelpBox("No poses synced yet — click \"Sync Poses From This Blend Tree\" above.", MessageType.None);
            }
            else
            {
                for (int i = 0; i < posesProp.arraySize; i++)
                {
                    SerializedProperty entryProp = posesProp.GetArrayElementAtIndex(i);
                    SerializedProperty nameProp = entryProp.FindPropertyRelative("MotionName");
                    SerializedProperty multiplierProp = entryProp.FindPropertyRelative("Multiplier");

                    EditorGUILayout.PropertyField(multiplierProp, new GUIContent($"Multiplier — {nameProp.stringValue}"));
                }
            }

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(4);
            SerializedProperty multiplierParamProp = serializedObject.FindProperty("_stateSpeedMultiplierParameterName");
            EditorGUILayout.PropertyField(multiplierParamProp, new GUIContent("State Speed Multiplier Parameter"));
            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// Reads the given Blend Tree's children (motion name + threshold) and rewrites
        /// LocomotionAgent's PosePlaybackRates to match, in Threshold order. Preserves
        /// existing Multiplier values for motions with matching names across re-syncs (e.g.
        /// after adding a new pose to the tree), instead of resetting everything to 1 each time.
        /// </summary>
        private void SyncPosePlaybackRates(LocomotionAgent agent, BlendTree tree)
        {
            Dictionary<string, float> existingMultipliers = new Dictionary<string, float>();
            foreach (LocomotionAgent.PosePlaybackRate entry in agent.PosePlaybackRates)
            {
                existingMultipliers[entry.MotionName] = entry.Multiplier;
            }

            List<LocomotionAgent.PosePlaybackRate> newEntries = new List<LocomotionAgent.PosePlaybackRate>();
            foreach (ChildMotion child in tree.children.OrderBy(c => c.threshold))
            {
                string motionName = child.motion != null ? child.motion.name : "(empty)";
                float multiplier = existingMultipliers.TryGetValue(motionName, out float existing) ? existing : 1f;

                newEntries.Add(new LocomotionAgent.PosePlaybackRate
                {
                    MotionName = motionName,
                    Threshold = child.threshold,
                    Multiplier = multiplier
                });
            }

            Undo.RecordObject(agent, "Sync Pose Playback Rates");
            agent.PosePlaybackRates.Clear();
            agent.PosePlaybackRates.AddRange(newEntries);
            EditorUtility.SetDirty(agent);
            serializedObject.Update();
        }

        private void DrawAnimationStateSection(AnimatorController controller)
        {
            List<AnimationStateEntry> states = CollectAnimationStates(controller);

            EditorGUILayout.LabelField("Individual Animation", EditorStyles.boldLabel);

            if (states.Count == 0)
            {
                EditorGUILayout.HelpBox("No individual animation states found in this Animator Controller.", MessageType.None);
                return;
            }

            string[] labels = states.Select(s => s.Label).ToArray();
            _selectedStateIndex = Mathf.Clamp(_selectedStateIndex, 0, states.Count - 1);
            _selectedStateIndex = EditorGUILayout.Popup("Select Animation", _selectedStateIndex, labels);

            AnimatorState state = states[_selectedStateIndex].State;

            EditorGUI.BeginChangeCheck();
            float newSpeed = EditorGUILayout.FloatField($"Speed for \"{state.name}\"", state.speed);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(state, "Change Animation State Speed");
                state.speed = newSpeed;
                EditorUtility.SetDirty(state);
            }
        }

        private static List<BlendTreeEntry> CollectBlendTrees(AnimatorController controller)
        {
            var results = new List<BlendTreeEntry>();
            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                CollectBlendTreesRecursive(layer.stateMachine, results);
            }
            return results;
        }

        private static void CollectBlendTreesRecursive(AnimatorStateMachine stateMachine, List<BlendTreeEntry> results)
        {
            foreach (ChildAnimatorState childState in stateMachine.states)
            {
                if (childState.state.motion is BlendTree blendTree)
                {
                    results.Add(new BlendTreeEntry { Label = $"{childState.state.name} ({blendTree.name})", Tree = blendTree });
                }
            }
            foreach (ChildAnimatorStateMachine childMachine in stateMachine.stateMachines)
            {
                CollectBlendTreesRecursive(childMachine.stateMachine, results);
            }
        }

        private static List<AnimationStateEntry> CollectAnimationStates(AnimatorController controller)
        {
            var results = new List<AnimationStateEntry>();
            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                CollectAnimationStatesRecursive(layer.stateMachine, results);
            }
            return results;
        }

        private static void CollectAnimationStatesRecursive(AnimatorStateMachine stateMachine, List<AnimationStateEntry> results)
        {
            foreach (ChildAnimatorState childState in stateMachine.states)
            {
                if (childState.state.motion is AnimationClip)
                {
                    results.Add(new AnimationStateEntry { Label = childState.state.name, State = childState.state });
                }
            }
            foreach (ChildAnimatorStateMachine childMachine in stateMachine.stateMachines)
            {
                CollectAnimationStatesRecursive(childMachine.stateMachine, results);
            }
        }
    }
}
#endif