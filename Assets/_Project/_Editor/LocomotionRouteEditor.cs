#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using TownsPeople.GamePlay;

namespace TownsPeople.CustomEditor
{
    /// <summary>
    /// Custom Inspector + Scene view tooling for LocomotionRoute. Lets you place new waypoints
    /// at the Scene view camera's current focus point (same convenience as
    /// NPCCreatorWizardWindow's NPC spawn placement), drag existing waypoints directly in the
    /// Scene view via position handles (with numbered labels as the visual aid), edit each
    /// waypoint's speed tier / linger duration from the Inspector list, and traces a thick,
    /// per-route-colored line across every leg of the route (via Handles.DrawLine, since
    /// Gizmos.DrawLine has no thickness parameter) whenever this route is the active
    /// Selection � including via NPCControlPanelWindow's "Show Locomotion Route" button.
    ///
    /// FIX: TownsPeople.CustomEditor's own last namespace segment shadows UnityEditor's
    /// CustomEditor attribute and Editor base class (same collision already fixed once on
    /// PlayerDeedBroadcasterEditor) � both are fully qualified below instead of relying on
    /// the `using UnityEditor;` import for these two specifically.
    ///
    /// v2: Draws each waypoint's Point of Interest fields (Is Point Of Interest / Stop
    /// Behavior / Stop Chance) alongside the pre-existing Position/Arrival Speed Tier/Linger
    /// Duration � Stop Chance only shown when Stop Behavior is actually Random Chance.
    /// </summary>
    [UnityEditor.CustomEditor(typeof(LocomotionRoute))]
    public class LocomotionRouteEditor : UnityEditor.Editor
    {
        private const float RaycastMaxDistance = 1000f;
        private const float FallbackDistance = 5f;

        public override void OnInspectorGUI()
        {
            LocomotionRoute route = (LocomotionRoute)target;

            EditorGUILayout.LabelField("Locomotion Route", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Drag waypoint handles directly in the Scene view to reposition them. Use the buttons below to add/remove stops.", MessageType.None);
            EditorGUILayout.Space();

            SerializedProperty loopProp = serializedObject.FindProperty("_isLoop");
            EditorGUILayout.PropertyField(loopProp, new GUIContent("Loop", "If enabled, the last waypoint connects back to the first."));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Visualization", EditorStyles.boldLabel);
            SerializedProperty lineColorProp = serializedObject.FindProperty("_lineColor");
            SerializedProperty lineThicknessProp = serializedObject.FindProperty("_lineThickness");
            EditorGUILayout.PropertyField(lineColorProp, new GUIContent("Line Color", "Color of the traced connecting line shown in Scene view when this route is selected."));
            EditorGUILayout.PropertyField(lineThicknessProp, new GUIContent("Line Thickness", "Thickness, in pixels, of the traced connecting line."));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Waypoints ({route.Count})", EditorStyles.boldLabel);

            SerializedProperty waypointsProp = serializedObject.FindProperty("_waypoints");
            for (int i = 0; i < waypointsProp.arraySize; i++)
            {
                SerializedProperty waypointProp = waypointsProp.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginVertical("box");

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Waypoint {i}", EditorStyles.boldLabel);
                GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
                bool removed = GUILayout.Button("Remove", GUILayout.Width(70));
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();

                if (removed)
                {
                    Undo.RecordObject(route, "Remove Waypoint");
                    route.RemoveWaypointAt(i);
                    EditorUtility.SetDirty(route);
                    serializedObject.Update();
                    EditorGUILayout.EndVertical();
                    break;
                }

                EditorGUILayout.PropertyField(waypointProp.FindPropertyRelative("Position"));
                EditorGUILayout.PropertyField(waypointProp.FindPropertyRelative("ArrivalSpeedTier"), new GUIContent("Arrival Speed Tier"));
                EditorGUILayout.PropertyField(waypointProp.FindPropertyRelative("LingerDuration"));

                // v13: POI fields � Stop Chance only shown when actually relevant (Random
                // Chance mode), so the Inspector doesn't clutter itself for the common Always
                // Stop case.
                SerializedProperty isPoiProp = waypointProp.FindPropertyRelative("IsPointOfInterest");
                EditorGUILayout.PropertyField(isPoiProp, new GUIContent("Is Point Of Interest"));

                if (isPoiProp.boolValue)
                {
                    EditorGUI.indentLevel++;
                    SerializedProperty stopBehaviorProp = waypointProp.FindPropertyRelative("StopBehavior");
                    EditorGUILayout.PropertyField(stopBehaviorProp, new GUIContent("Stop Behavior"));

                    if (stopBehaviorProp.enumValueIndex == (int)WaypointStopBehavior.RandomChance)
                    {
                        EditorGUILayout.PropertyField(waypointProp.FindPropertyRelative("StopChance"), new GUIContent("Stop Chance"));
                    }
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndVertical();
            }

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            GUI.backgroundColor = new Color(0.6f, 0.85f, 0.6f);
            if (GUILayout.Button("Add Waypoint At Scene Camera Focus", GUILayout.Height(30)))
            {
                Undo.RecordObject(route, "Add Waypoint");
                route.AddWaypoint(ComputeSceneCameraFocusPosition());
                EditorUtility.SetDirty(route);
            }
            GUI.backgroundColor = Color.white;
        }

        private void OnSceneGUI()
        {
            LocomotionRoute route = (LocomotionRoute)target;

            DrawTracedRouteLine(route);

            for (int i = 0; i < route.Count; i++)
            {
                LocomotionWaypoint waypoint = route.GetWaypoint(i);

                EditorGUI.BeginChangeCheck();
                Vector3 newPosition = Handles.PositionHandle(waypoint.Position, Quaternion.identity);
                Handles.Label(waypoint.Position + Vector3.up * 0.5f, i.ToString());

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(route, "Move Waypoint");
                    route.MoveWaypointPosition(i, newPosition);
                    EditorUtility.SetDirty(route);
                }
            }
        }

        /// <summary>
        /// Draws a thick, editable-color line tracing every leg of the route.
        /// Handles.DrawLine supports a pixel-thickness parameter that Gizmos.DrawLine does not
        /// � that's why this lives here rather than in LocomotionRoute's own OnDrawGizmos.
        /// Runs automatically whenever this route is the active Selection, including right
        /// after NPCControlPanelWindow's "Show Locomotion Route" button � the exact trigger
        /// this was built for.
        /// </summary>
        private static void DrawTracedRouteLine(LocomotionRoute route)
        {
            if (route.Count < 2) return;

            Handles.color = route.LineColor;

            for (int i = 0; i < route.Count - 1; i++)
            {
                Handles.DrawLine(route.GetWaypoint(i).Position, route.GetWaypoint(i + 1).Position, route.LineThickness);
            }

            if (route.IsLoop && route.Count > 1)
            {
                Handles.DrawLine(route.GetWaypoint(route.Count - 1).Position, route.GetWaypoint(0).Position, route.LineThickness);
            }
        }

        /// <summary>
        /// Same raycast-from-Scene-camera convenience as NPCCreatorWizardWindow's NPC spawn
        /// placement � lands on the first collider hit (e.g. the street/ground), falling back
        /// to a fixed distance in front of the camera if nothing's hit.
        /// </summary>
        private static Vector3 ComputeSceneCameraFocusPosition()
        {
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null || sceneView.camera == null)
            {
                return Vector3.zero;
            }

            Camera sceneCamera = sceneView.camera;
            Ray lookRay = new Ray(sceneCamera.transform.position, sceneCamera.transform.forward);

            if (Physics.Raycast(lookRay, out RaycastHit hit, RaycastMaxDistance))
            {
                return hit.point;
            }

            return lookRay.GetPoint(FallbackDistance);
        }
    }
}
#endif