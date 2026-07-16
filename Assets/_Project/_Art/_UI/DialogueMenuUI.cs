using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Project.GamePlay;

namespace Project.UI
{
    /// <summary>
    /// Shared, single dialogue menu UI — one instance in the scene, opened and repopulated for
    /// whichever NPC the player is currently talking to. Replaces the old single-response [E]
    /// mechanic for Common NPCs with a scrollable list of conversation options (Greet, Ask
    /// about rumors, etc.). Non-Dialogue NPCs and Vendor NPCs never open this — see
    /// NPCProximityGossip for the routing logic.
    /// </summary>
    public class DialogueMenuUI : MonoBehaviour
    {
        public static DialogueMenuUI Instance { get; private set; }

        [SerializeField] private CanvasGroupFader _panelFader;
        [SerializeField] private TextMeshProUGUI _npcNameText;
        [SerializeField] private Transform _optionsContainer;
        [SerializeField] private Button _optionButtonPrefab;
        [SerializeField] private Button _leaveButton;

        [Header("Greet Option")]
        [Tooltip("Personal opinion boost applied to this NPC when the player selects 'Greet'. Subject to that NPC's own greet cooldown.")]
        [SerializeField] private float _greetBoostAmount = 5f;

        private NPCGossipMemory _currentGossipMemory;
        private NPCReputationOpinion _currentReputationOpinion;
        private string _currentNpcName;
        private Action _onClosedCallback;
        private readonly List<GameObject> _spawnedButtons = new List<GameObject>();

        private void Awake()
        {
            Instance = this;

            if (_leaveButton != null)
            {
                _leaveButton.onClick.AddListener(Close);
            }

            _panelFader?.SetInstant(false);
        }

        /// <summary>
        /// Opens the menu for a given NPC. gossipMemory and reputationOpinion may be null
        /// individually (options requiring them are simply omitted), but the menu itself
        /// requires at least a gossipMemory to be meaningfully useful — see NPCProximityGossip,
        /// which only calls this for Common NPCs.
        /// </summary>
        public void Open(string npcName, NPCGossipMemory gossipMemory, NPCReputationOpinion reputationOpinion, Action onClosed)
        {
            _currentNpcName = npcName;
            _currentGossipMemory = gossipMemory;
            _currentReputationOpinion = reputationOpinion;
            _onClosedCallback = onClosed;

            if (_npcNameText != null)
            {
                _npcNameText.text = npcName;
            }

            // Opening line plays through the NPC's own speech bubble/audio, same as any other presentation.
            gossipMemory?.PlayGreeting();

            RebuildOptions();

            _panelFader?.Show();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void Close()
        {
            _panelFader?.Hide();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            Action callback = _onClosedCallback;

            _currentGossipMemory = null;
            _currentReputationOpinion = null;
            _onClosedCallback = null;
            ClearButtons();

            callback?.Invoke();
        }

        private void RebuildOptions()
        {
            ClearButtons();

            bool canGreet = _currentReputationOpinion == null || _currentReputationOpinion.CanGreet();
            string greetLabel = canGreet || _currentReputationOpinion == null
                ? $"Greet {_currentNpcName}"
                : $"Greet {_currentNpcName} (wait {Mathf.CeilToInt(_currentReputationOpinion.GetGreetCooldownRemaining())}s)";
            AddOption(greetLabel, OnSelectGreet, canGreet);

            if (_currentGossipMemory != null && _currentGossipMemory.KnownRumors.Count > 0)
            {
                AddOption("What do you hear on the streets?", OnSelectAskAboutRumors, true);
            }
        }

        private void OnSelectGreet()
        {
            _currentReputationOpinion?.TryApplyGreetBoost(_greetBoostAmount);
            RebuildOptions(); // Refresh so the cooldown state/label updates immediately.
        }

        private void OnSelectAskAboutRumors()
        {
            _currentGossipMemory?.TryTellNextRumor();
        }

        private void AddOption(string label, Action onClick, bool interactable)
        {
            if (_optionButtonPrefab == null || _optionsContainer == null) return;

            Button buttonInstance = Instantiate(_optionButtonPrefab, _optionsContainer);
            buttonInstance.interactable = interactable;

            TextMeshProUGUI labelText = buttonInstance.GetComponentInChildren<TextMeshProUGUI>();
            if (labelText != null)
            {
                labelText.text = label;
            }

            buttonInstance.onClick.AddListener(() => onClick());

            _spawnedButtons.Add(buttonInstance.gameObject);
        }

        private void ClearButtons()
        {
            foreach (GameObject spawned in _spawnedButtons)
            {
                if (spawned != null)
                {
                    Destroy(spawned);
                }
            }
            _spawnedButtons.Clear();
        }
    }
}