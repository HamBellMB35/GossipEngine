using UnityEngine; // Access to Unity engine core classes
using UnityEngine.InputSystem; // Access to the modern Input System package
using System.Collections.Generic; // Access to collection types like List
using Project.Data; // Custom project data namespace for RumorTemplates

namespace Project.GamePlay
{
    /// <summary>
    /// NPCAnimationBridge: Manages NPC reactions to rumors.
    /// This script acts as the bridge between data (Rumors) and the visual output (Animator).
    /// </summary>
    public class NPCAnimationBridge : MonoBehaviour
    {
        [Header("Components")]
        [Tooltip("The Animator component controlling this character's rig.")]
        [SerializeField] private Animator _animator; // Reference to the Animator component
        [Tooltip("The UI element to show for 'Manual' interaction.")]
        [SerializeField] private GameObject _talkPromptUI; // UI that appears over the NPC's head
        [Tooltip("Reference to the Input Action (e.g., 'Interact') from your Input Asset.")]
        [SerializeField] private InputActionReference _interactAction; // The modern input action for interaction

        [Header("Behavior Settings")]
        [Tooltip("Minimum time (in seconds) the NPC must wait before reacting again.")]
        [SerializeField] private float ReactionCooldown = 5.0f; // Cooldown duration in seconds

        [Header("Default Behavior")]
        [Tooltip("List of animator state names used for default idle behavior.")]
        [SerializeField] private List<string> IdleStateNames = new List<string>(); // List of animation states for idling

        // Private internal state tracking
        private Transform _playerTransform; // Cached transform of the player for distance math
        private RumorTemplate _activeRumor; // The specific rumor currently assigned to this NPC
        private bool _isCurrentlyReacting = false; // Flag representing if the NPC is currently playing a rumor reaction
        private float _cooldownTimer = 0.0f; // Timer tracking remaining cooldown seconds
        private bool _wasPlayerInRange = false; // Tracks the last proximity state to filter logs

        // Hash caches: Integers lookups are significantly faster than string lookups
        private List<int> _idleStateHashes = new List<int>(); // List of cached integer hashes for idle animations
        private List<int> _rumorStateHashes = new List<int>(); // List of cached integer hashes for rumor reactions

        // Enable input listening when this component is enabled in the scene
        private void OnEnable() => _interactAction?.action.Enable();
        // Disable input listening when this component is disabled to free resources
        private void OnDisable() => _interactAction?.action.Disable();

        private void Start()
        {
            // Iterate through every idle state name defined in the Inspector
            foreach (string state in IdleStateNames)
            {
                // Only hash the string if it is not null or empty to prevent invalid hashes
                if (!string.IsNullOrEmpty(state))
                {
                    // Convert the string name to a numeric hash and add it to our list
                    _idleStateHashes.Add(Animator.StringToHash(state));
                }
            }

            // Locate the GameObject tagged as 'Player' in the scene hierarchy
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            // If the player exists, save its transform reference
            if (player != null) _playerTransform = player.transform;

            // Log the initialization event to confirm the bridge is running
            Debug.Log($"<color=white>[Init]</color> NPC {gameObject.name} initialized. Idle States: {_idleStateHashes.Count}");

            // Play a random idle animation so the NPC is not stuck in a T-Pose
            PlayRandomIdle();
        }

        private void Update()
        {
            // If we don't have a player or a rumor, stop all logic to prevent errors
            if (_playerTransform == null || _activeRumor == null) return;

            // Reduce the cooldown timer by the amount of time passed since the last frame
            if (_cooldownTimer > 0) _cooldownTimer -= Time.deltaTime;

            // Calculate current Euclidean distance between the NPC and the player
            float distance = Vector3.Distance(transform.position, _playerTransform.position);
            // Check if player is within the trigger radius defined by the rumor template
            bool inRange = distance <= _activeRumor.TriggerDistance;

            // Check if the proximity state has changed compared to the last frame
            if (inRange != _wasPlayerInRange)
            {
                // Only log if the state changed to avoid flooding the console
                Debug.Log($"<color=yellow>[Detection]</color> NPC: {gameObject.name} | InRange: {inRange} | Distance: {distance:F2}");
                // Update the memory of the player's range status
                _wasPlayerInRange = inRange;
            }

            // Update UI visibility: Active only if in manual mode, in range, and not on cooldown
            if (_talkPromptUI != null)
                _talkPromptUI.SetActive(_activeRumor.TriggerMode == RumorTriggerMode.ManualTalk && inRange && _cooldownTimer <= 0);

            // AUTO-PROXIMITY LOGIC: NPC reacts whenever player walks into range
            if (_activeRumor.TriggerMode == RumorTriggerMode.AutoProximity)
            {
                // Verify player is in range, NPC is not already reacting, and cooldown is finished
                if (inRange && !_isCurrentlyReacting && _cooldownTimer <= 0)
                {
                    Debug.Log($"<color=cyan>[Logic]</color> Auto-Proximity triggered.");
                    TriggerToneVisuals(true);
                }
                // If player leaves range while NPC is reacting, stop the reaction
                else if (!inRange && _isCurrentlyReacting)
                {
                    TriggerToneVisuals(false);
                }
            }
            // MANUAL TALK LOGIC: NPC waits for the player to press the interact key
            else if (_activeRumor.TriggerMode == RumorTriggerMode.ManualTalk)
            {
                // Verify player is in range, input was pressed, and cooldown is finished
                if (inRange && _interactAction.action.triggered && _cooldownTimer <= 0)
                {
                    // Perform a probability check against the rumor's share likelihood percentage
                    if (Random.Range(0, 100) <= _activeRumor.ShareLikelihood)
                    {
                        Debug.Log($"<color=cyan>[Logic]</color> Manual success. Triggering reaction.");
                        TriggerToneVisuals(true);
                    }
                    else
                    {
                        // Log a warning if the player tried to talk but the random chance failed
                        Debug.Log($"<color=orange>[Logic]</color> Interaction triggered, likelihood roll failed.");
                    }
                }
                // If player leaves range while NPC is reacting, stop the reaction
                else if (!inRange && _isCurrentlyReacting)
                {
                    TriggerToneVisuals(false);
                }
            }

            // AUTO-REVERT LOGIC: Monitor if the reaction animation has finished playing
            if (_isCurrentlyReacting)
            {
                // Get the current state info from the animator base layer
                AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
                // Check if animation is nearly done (95%+) and is set to NOT loop
                if (stateInfo.normalizedTime >= 0.95f && !stateInfo.loop)
                {
                    Debug.Log("<color=purple>[Logic]</color> Reaction clip finished. Reverting to idle.");
                    TriggerToneVisuals(false);
                }
            }
        }

        // Updates the active rumor and caches animation states as hashes
        public void UpdateRumor(RumorTemplate newRumor)
        {
            // Assign the incoming rumor template to the local private variable
            _activeRumor = newRumor;
            // Purge the old rumor cache so no stale animation data remains
            _rumorStateHashes.Clear();

            // Verify that the rumor template has a valid tone attached
            if (_activeRumor.AssociatedTone != null)
            {
                // Iterate through every animation state name string in the tone asset
                foreach (string name in _activeRumor.AssociatedTone.AnimatorStateNames)
                {
                    // Convert the human-readable string into an integer hash for the Animator
                    int stateHash = Animator.StringToHash(name);
                    // Store the hash in our rumor reaction list
                    _rumorStateHashes.Add(stateHash);
                }
            }

            // Log the update event and the number of successfully hashed states
            Debug.Log($"<color=blue>[System]</color> Rumor Updated: {newRumor.RumorID}. Hashed {_rumorStateHashes.Count} states.");
        }

        // Orchestrates the visual transition between reaction and idle states
        private void TriggerToneVisuals(bool active)
        {
            // Set the reaction flag to the provided boolean state
            _isCurrentlyReacting = active;

            // If enabling a reaction, process the animation logic
            if (active)
            {
                // Set the cooldown timer to prevent spamming reactions
                _cooldownTimer = ReactionCooldown;
                Debug.Log($"<color=green>[Animation]</color> Reaction active. Cooldown: {ReactionCooldown}s.");

                // Ensure we have at least one valid state to play
                if (_rumorStateHashes.Count > 0)
                {
                    // Pick a random hash from our collection to ensure visual variety
                    int hash = _rumorStateHashes[Random.Range(0, _rumorStateHashes.Count)];
                    // Start the crossfade to the chosen state
                    SafeCrossFade(hash, _activeRumor.AssociatedTone.CrossfadeDuration);
                }
            }
            else
            {
                // If disabling the reaction, revert to the standard idle cycle
                Debug.Log("<color=green>[Animation]</color> Reverting to Idle.");
                PlayRandomIdle();
            }
        }

        // Selects a random idle state to maintain visual variety
        private void PlayRandomIdle()
        {
            // Guard clause: stop if no idle states exist to prevent index errors
            if (_idleStateHashes.Count == 0) return;

            // Pick a random hash from the idle pool
            int hash = _idleStateHashes[Random.Range(0, _idleStateHashes.Count)];
            // Log that the NPC is returning to ambient idle
            Debug.Log("<color=white>[Status]</color> Character has entered default idle state.");

            // Transition smoothly to the chosen idle animation
            SafeCrossFade(hash, 0.5f);
        }

        // Executes crossfade after validating state existence in the animator
        private void SafeCrossFade(int hash, float fade)
        {
            // Confirm the animator contains the state to prevent console warnings
            if (_animator.HasState(0, hash))
            {
                // Command the animator to crossfade using the duration specified
                _animator.CrossFadeInFixedTime(hash, fade);
            }
            else
            {
                // Log a high-priority error if an animation is missing from the controller
                Debug.LogError($"<color=red>[CRITICAL ERROR]</color> {gameObject.name}: Hash {hash} not found in Base Layer!");
            }
        }
    }
}