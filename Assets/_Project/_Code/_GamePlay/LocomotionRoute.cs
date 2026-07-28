using System.Collections.Generic;
using UnityEngine;

namespace TownsPeople.GamePlay
{
    /// <summary>
    /// An ordered, reusable sequence of waypoints. Not tied to any one NPC — any number of
    /// NPCs can be assigned to walk the same route independently (e.g. several wandering
    /// townspeople sharing one market-loop route). Waypoints are authored visually in the Scene
    /// view via LocomotionRouteEditor, not edited by hand.
    /// </summary>
    public class LocomotionRoute : MonoBehaviour
    {
        [Tooltip("Ordered stops along this route. Edit via the Scene view handles and buttons on this component's Inspector — positions are world-space.")]
        [SerializeField] private List<LocomotionWaypoint> _waypoints = new List<LocomotionWaypoint>();

        [Tooltip("If enabled, the last waypoint connects back to the first (a continuous loop). If disabled, a behavior reaching the last waypoint decides for itself what happens next (e.g. reverse, or stop).")]
        [SerializeField] private bool _isLoop = true;

        [Header("Visualization")]
        [Tooltip("Color used when tracing this route's connecting line in the Scene view — e.g. via the NPC Control Panel's 'Show Locomotion Route' button. Editable per-route, so multiple routes in the same scene can be visually distinguished from each other.")]
        [SerializeField] private Color _lineColor = new Color(0.36f, 0.62f, 0.92f, 1f);

        [Tooltip("Thickness, in pixels, of the traced connecting line drawn when this route is selected.")]
        [SerializeField] private float _lineThickness = 4f;

        public IReadOnlyList<LocomotionWaypoint> Waypoints => _waypoints;
        public bool IsLoop => _isLoop;
        public int Count => _waypoints.Count;
        public Color LineColor => _lineColor;
        public float LineThickness => _lineThickness;

        public LocomotionWaypoint GetWaypoint(int index) => _waypoints[index];

#if UNITY_EDITOR
        // Editor-only mutation API — LocomotionRouteEditor is the only intended caller. Kept as
        // methods rather than exposing the raw list directly, so all mutation goes through one
        // place if this needs validation/undo support extended later.
        public void AddWaypoint(Vector3 position)
        {
            _waypoints.Add(new LocomotionWaypoint(position));
        }

        public void RemoveWaypointAt(int index)
        {
            if (index < 0 || index >= _waypoints.Count) return;
            _waypoints.RemoveAt(index);
        }

        public void MoveWaypointPosition(int index, Vector3 newPosition)
        {
            if (index < 0 || index >= _waypoints.Count) return;
            _waypoints[index].Position = newPosition;
        }

        private void OnDrawGizmos()
        {
            DrawRouteGizmos(selected: false);
        }

        private void OnDrawGizmosSelected()
        {
            DrawRouteGizmos(selected: true);
        }

        private void DrawRouteGizmos(bool selected)
        {
            if (_waypoints == null || _waypoints.Count == 0) return;

            // Thin, always-visible-or-faint indicator regardless of selection — the thick,
            // properly editable-color trace (what "Show Locomotion Route" is actually built to
            // reveal) is drawn separately via Handles in LocomotionRouteEditor.OnSceneGUI,
            // since Gizmos.DrawLine has no thickness parameter at all.
            Gizmos.color = selected ? _lineColor : new Color(_lineColor.r, _lineColor.g, _lineColor.b, _lineColor.a * 0.35f);

            for (int i = 0; i < _waypoints.Count; i++)
            {
                Vector3 point = _waypoints[i].Position;
                Gizmos.DrawSphere(point, 0.3f);

                int nextIndex = i + 1;
                if (nextIndex >= _waypoints.Count)
                {
                    if (_isLoop && _waypoints.Count > 1)
                    {
                        DrawArrow(point, _waypoints[0].Position);
                    }
                    continue;
                }

                DrawArrow(point, _waypoints[nextIndex].Position);
            }
        }

        private static void DrawArrow(Vector3 from, Vector3 to)
        {
            Gizmos.DrawLine(from, to);

            Vector3 direction = (to - from).normalized;
            if (direction.sqrMagnitude < 0.0001f) return;

            Vector3 midPoint = Vector3.Lerp(from, to, 0.5f);
            Vector3 right = Quaternion.LookRotation(direction) * Quaternion.Euler(0f, 160f, 0f) * Vector3.forward;
            Vector3 left = Quaternion.LookRotation(direction) * Quaternion.Euler(0f, -160f, 0f) * Vector3.forward;

            Gizmos.DrawLine(midPoint, midPoint + right * 0.3f);
            Gizmos.DrawLine(midPoint, midPoint + left * 0.3f);
        }
#endif
    }
}