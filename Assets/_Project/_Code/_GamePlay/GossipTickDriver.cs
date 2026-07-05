using UnityEngine;
using VContainer;
using Project.Architecture;

namespace Project.GamePlay
{
    /// <summary>
    /// Drives the tick-based half of the Gossip Propagation Engine. CoreGossipEngine is
    /// deliberately a plain C# class (not a MonoBehaviour), so it has no Update loop of its
    /// own — this small bridge component is what actually calls RunPropagationTick() on an
    /// interval. Add one instance of this anywhere in your scene (a single empty "GossipSystem"
    /// GameObject is enough — it's not per-NPC).
    /// </summary>
    public class GossipTickDriver : MonoBehaviour
    {
        [Tooltip("How often (in seconds) a full gossip propagation tick runs across every NPC in the scene.")]
        [SerializeField] private float _tickIntervalSeconds = 15f;

        private IGossipEngine _gossipEngine;
        private float _timer;

        [Inject]
        public void Construct(IGossipEngine gossipEngine)
        {
            _gossipEngine = gossipEngine;
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            if (_timer < _tickIntervalSeconds) return;

            _timer = 0f;
            _gossipEngine?.RunPropagationTick();
            Debug.Log("<color=blue>[GossipTickDriver]</color> Propagation tick executed.");
        }
    }
}