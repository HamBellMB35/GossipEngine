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
    /// Custom Inspector for LocomotionAgent. Draws every existing field normally EXCEPT
    /// _posePlaybackRates (drawn exclusively by the Blend Tree section below, not twice),
    /// then appends two sections:
    /// - Blend Tree: select a Blend Tree (auto-populated from the assigned Animator's
    ///   Controller) and sync a Multiplier field per motion in it � feeds LocomotionAgent's
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
            serializedObject.Update();

            // v3 FIX: was DrawDefaultInspector(), which drew _posePlaybackRates here in its
            // raw default form AND a second time down in DrawBlendTreeSection() below �
            // the exact duplicate the "Per-Pose Playback Rate" field showing up twice was
            // caused by. Skips that one field from this pass; every other field draws
            // exactly as before.
            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (iterator.propertyPath == "_posePlaybackRates") continue;

                // v4: Stacked layout (label above, control below, full width) instead of
                // Unity's default side-by-side rendering � neither the label nor the
                // slider/dropdown had enough room on one line for these two specifically.
                if (iterator.propertyPath == "_stopAnimationMinNormalizedSpeed")
                {
                    EditorGUILayout.LabelField("Stop Animation Min Normalized Speed");
                    iterator.floatValue = EditorGUILayout.Slider(iterator.floatValue, 0f, 1f);
                    continue;
                }

                if (iterator.propertyPath == "_stateSpeedMultiplierParameterName")
                {
                    EditorGUILayout.LabelField("State Speed Multiplier Parameter");
                    EditorGUILayout.PropertyField(iterator, GUIContent.none);
                    continue;
                }

                EditorGUILayout.PropertyField(iterator, true);
            }

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Animation Speed Tuning (Editor Convenience)", EditorStyles.boldLabel);

            SerializedProperty animatorProp = serializedObject.FindProperty("_animator");
            Animator animator = animatorProp != null ? animatorProp.objectReferenceValue as Animator : null;

            AnimatorController controller = animator != null ? animator.runtimeAnimatorController as AnimatorController : null;

            if (controller == null)
            {
                EditorGUILayout.HelpBox("No AnimatorController resolved yet � assign an Animator above.", MessageType.Warning);
                return;
            }

            DrawBlendTreeSection(controller);
            EditorGUILayout.Space(6);
            DrawAnimationStateSection(controller);
        }

        private void DrawBlendTreeSection(AnimatorController controller)
        {
            List<BlendTreeEntry> blendTrees = CollectBlendTrees(controller);

            EditorGUILayout.LabelField("Blend Tree � Per-Pose Playback Rate", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Select the SAME Blend Tree that drives this NPC's Speed parameter (Locomotion). A different tree here won't correspond to anything at runtime � only the Locomotion tree's live blend position is measured.", MessageType.Info);

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
                EditorGUILayout.HelpBox("No poses synced yet � click \"Sync Poses From This Blend Tree\" above.", MessageType.None);
            }
            else
            {
                for (int i = 0; i < posesProp.arraySize; i++)
                {
                    SerializedProperty entryProp = posesProp.GetArrayElementAtIndex(i);
                    SerializedProperty nameProp = entryProp.FindPropertyRelative("MotionName");
                    SerializedProperty multiplierProp = entryProp.FindPropertyRelative("Multiplier");

                    EditorGUILayout.PropertyField(multiplierProp, new GUIContent($"Multiplier � {nameProp.stringValue}"));
                }
            }

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(4);
            SerializedProperty multiplierParamProp = serializedObject.FindProperty("_stateSpeedMultiplierParameterName");
            // v4: Stacked layout here too � same fix as the field's other occurrence above.
            EditorGUILayout.LabelField("State Speed Multiplier Parameter");
            EditorGUILayout.PropertyField(multiplierParamProp, GUIContent.none);
            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// Reads the given Blend Tree's children (motion name + Speed-axis position) and
        /// rewrites LocomotionAgent's PosePlaybackRates to match. Preserves existing Multiplier
        /// values for motions with matching names across re-syncs (e.g. after adding a new pose
        /// to the tree), instead of resetting everything to 1 each time.
        /// </summary>
        // v2 FIX: ChildMotion.threshold is a 1D-ONLY concept — for a 2D Freeform/Directional
        // tree, every child's .threshold silently reads back as 0. Fixed by reading .position.x
        // (the Speed axis, per the documented Editor setup — X = Speed, Y = Turn) for any
        // non-1D tree instead.
        //
        // v6 FIX: v2's dedup-by-Speed-value (added alongside the threshold fix) silently dropped
        // every Turn-variant pose sharing a Speed with another pose — e.g. Walk Turn Left/Right
        // both vanished behind whichever Walk Forward was found first at X = 0.5. Not
        // acceptable: every pose needs its own editable Multiplier. Dedup REMOVED — back to one
        // row per Blend Tree child, exactly like the original 1D-only version, just with the
        // corrected Speed-axis read for 2D trees. LocomotionAgent's runtime side no longer needs
        // unique Thresholds anyway — v6 there reads live blend weights per-clip instead of
        // interpolating along this Speed-axis value, so Threshold is informational display data
        // only now, not a correctness requirement.
        private void SyncPosePlaybackRates(LocomotionAgent agent, BlendTree tree)
        {
            Dictionary<string, float> existingMultipliers = new Dictionary<string, float>();
            foreach (LocomotionAgent.PosePlaybackRate entry in agent.PosePlaybackRates)
            {
                existingMultipliers[entry.MotionName] = entry.Multiplier;
            }

            bool is1D = tree.blendType == BlendTreeType.Simple1D;

            // Local function: the correct "Speed axis" value for THIS child, regardless of
            // whether the tree is 1D (.threshold) or 2D (.position.x — the Speed axis, per the
            // documented setup convention). Display/reference only as of v6 — see class comment.
            float GetSpeedAxisValue(ChildMotion c) => is1D ? c.threshold : c.position.x;

            // v6: Every child gets its own row again — ordered by Speed then Turn (Y) for a
            // readable, grouped display, but no entries are merged or dropped.
            List<ChildMotion> orderedChildren = tree.children
                .OrderBy(GetSpeedAxisValue)
                .ThenBy(c => is1D ? 0f : c.position.y)
                .ToList();

            List<LocomotionAgent.PosePlaybackRate> newEntries = new List<LocomotionAgent.PosePlaybackRate>();
            foreach (ChildMotion child in orderedChildren)
            {
                string motionName = child.motion != null ? child.motion.name : "(empty)";
                float multiplier = existingMultipliers.TryGetValue(motionName, out float existing) ? existing : 1f;

                newEntries.Add(new LocomotionAgent.PosePlaybackRate
                {
                    MotionName = motionName,
                    Threshold = GetSpeedAxisValue(child),
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