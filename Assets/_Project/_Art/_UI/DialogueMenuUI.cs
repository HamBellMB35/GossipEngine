using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using Project.GamePlay;

namespace Project.UI
{
    /// <summary>
    /// Shared, single dialogue menu UI — one instance in the scene, opened and repopulated for
    /// whichever NPC the player is currently talking to. Supports two display modes:
    /// - List: all options shown in a scrollable list at once (classic menu).
    /// - Carousel: one option shown at a time; scroll wheel browses, with a crossfade
    ///   transition (fade out -> swap content -> fade in) between options. Click to select
    ///   whichever option is currently showing.
    /// </summary>
    public class DialogueMenuUI : MonoBehaviour
    {
        public static DialogueMenuUI Instance { get; private set; }

        [Header("Shared")]
        [SerializeField] private CanvasGroupFader _panelFader;
        [SerializeField] private TextMeshProUGUI _npcNameText;
        [SerializeField] private Button _leaveButton;

        [Header("Display Mode")]
        [Tooltip("List: all options in a scrollable list. Carousel: one option at a time, scroll to browse with a crossfade.")]
        [SerializeField] private bool _useCarouselMode = false;

        [Header("List Mode UI")]
        [SerializeField] private GameObject _listModeRoot;
        [SerializeField] private Transform _optionsContainer;
        [SerializeField] private Button _optionButtonPrefab;

        [Header("Carousel Mode UI")]
        [SerializeField] private GameObject _carouselModeRoot;
        [SerializeField] private CanvasGroup _carouselOptionGroup;
        [SerializeField] private Button _carouselOptionButton;
        [SerializeField] private TextMeshProUGUI _carouselOptionLabel;
        [SerializeField] private TextMeshProUGUI _carouselIndexText;
        [SerializeField] private float _carouselFadeDuration = 0.2f;
        [Tooltip("How far (in UI units) the outgoing/incoming option slides while transitioning.")]
        [SerializeField] private float _carouselSlideDistance = 30f;

        [Header("Greet Option")]
        [Tooltip("Personal opinion boost applied to this NPC when the player selects 'Greet'. Subject to that NPC's own greet cooldown.")]
        [SerializeField] private float _greetBoostAmount = 5f;

        private struct DialogueOptionData
        {
            public string Label;
            public Action OnSelect;
            public bool Interactable;
        }

        private NPCGossipMemory _currentGossipMemory;
        private NPCReputationOpinion _currentReputationOpinion;
        private string _currentNpcName;
        private Action _onClosedCallback;
        private bool _isOpen;

        private readonly List<GameObject> _spawnedListButtons = new List<GameObject>();
        private readonly List<DialogueOptionData> _currentOptions = new List<DialogueOptionData>();
        private int _carouselIndex;
        private Coroutine _carouselTransition;
        private RectTransform _carouselOptionRect;
        private Vector2 _carouselRestingAnchoredPosition;
        private bool _carouselRestPositionCaptured;

        private void Awake()
        {
            Instance = this;

            if (_leaveButton != null) _leaveButton.onClick.AddListener(Close);
            if (_carouselOptionButton != null) _carouselOptionButton.onClick.AddListener(OnCarouselOptionClicked);

            _panelFader?.SetInstant(false);

            if (_listModeRoot != null) _listModeRoot.SetActive(!_useCarouselMode);
            if (_carouselModeRoot != null) _carouselModeRoot.SetActive(_useCarouselMode);

            if (_carouselOptionGroup != null)
            {
                _carouselOptionRect = _carouselOptionGroup.GetComponent<RectTransform>();
                // v5: Position is deliberately NOT captured here — see
                // EnsureCarouselRestPositionCaptured(), called from Open() instead.
            }
        }

        private void Update()
        {
            if (!_isOpen || !_useCarouselMode) return;
            if (Mouse.current == null) return;

            float scroll = Mouse.current.scroll.y.value;
            if (scroll > 0.01f) ScrollCarousel(-1);
            else if (scroll < -0.01f) ScrollCarousel(1);
        }

        /// <summary>
        /// v5: Captures the carousel option's resting position on first use only, from Open()
        /// rather than Awake(). By the time Open() is ever called (player has to walk up and
        /// press [E]), many frames have passed since scene start, so Unity's layout system has
        /// definitely already run its natural calculation pass — no forcing required, and no
        /// risk of the side effects that came from forcing it during Awake().
        /// </summary>
        private void EnsureCarouselRestPositionCaptured()
        {
            if (_carouselRestPositionCaptured || _carouselOptionRect == null) return;

            _carouselRestingAnchoredPosition = _carouselOptionRect.anchoredPosition;
            _carouselRestPositionCaptured = true;
        }

        /// <summary>
        /// Opens the menu for a given NPC. gossipMemory and reputationOpinion may be null
        /// individually (options requiring them are simply omitted).
        /// </summary>
        public void Open(string npcName, NPCGossipMemory gossipMemory, NPCReputationOpinion reputationOpinion, Action onClosed)
        {
            EnsureCarouselRestPositionCaptured();

            _currentNpcName = npcName;
            _currentGossipMemory = gossipMemory;
            _currentReputationOpinion = reputationOpinion;
            _onClosedCallback = onClosed;
            _isOpen = true;

            if (_npcNameText != null)
            {
                _npcNameText.text = npcName;
            }

            // Opening line plays through the NPC's own speech bubble/audio, same as any other presentation.
            gossipMemory?.PlayGreeting();

            RebuildOptionData();

            if (_useCarouselMode)
            {
                _carouselIndex = 0;
                if (_carouselTransition != null) { StopCoroutine(_carouselTransition); _carouselTransition = null; }
                ApplyCurrentCarouselOption();
                if (_carouselOptionGroup != null) _carouselOptionGroup.alpha = 1f;
                if (_carouselOptionRect != null) _carouselOptionRect.anchoredPosition = _carouselRestingAnchoredPosition;
            }
            else
            {
                RebuildListButtons();
            }

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

            _isOpen = false;
            _currentGossipMemory = null;
            _currentReputationOpinion = null;
            _onClosedCallback = null;
            ClearListButtons();
            _currentOptions.Clear();

            callback?.Invoke();
        }

        // v7: Options are now defined per-NPC (see NPCGossipMemory.GreetOptionSettings/
        // RumorsOptionSettings) instead of being hardcoded here — this method just reads
        // whatever the current NPC says it offers.
        private void RebuildOptionData()
        {
            _currentOptions.Clear();

            if (_currentGossipMemory == null) return;

            DialogueOptionSettings greetSettings = _currentGossipMemory.GreetOptionSettings;
            if (greetSettings.Enabled)
            {
                bool canGreet = _currentReputationOpinion == null || _currentReputationOpinion.CanGreet();

                string baseLabel = string.IsNullOrEmpty(greetSettings.CustomLabel)
                    ? $"Greet {_currentNpcName}"
                    : greetSettings.CustomLabel;

                string greetLabel = canGreet
                    ? baseLabel
                    : $"{baseLabel} (wait {Mathf.CeilToInt(_currentReputationOpinion.GetGreetCooldownRemaining())}s)";

                _currentOptions.Add(new DialogueOptionData { Label = greetLabel, OnSelect = OnSelectGreet, Interactable = canGreet });
            }

            DialogueOptionSettings rumorsSettings = _currentGossipMemory.RumorsOptionSettings;
            if (rumorsSettings.Enabled && _currentGossipMemory.KnownRumors.Count > 0)
            {
                string label = string.IsNullOrEmpty(rumorsSettings.CustomLabel)
                    ? "What do you hear on the streets?"
                    : rumorsSettings.CustomLabel;

                _currentOptions.Add(new DialogueOptionData { Label = label, OnSelect = OnSelectAskAboutRumors, Interactable = true });
            }

            // v8: Fully custom, NPC-authored options — each invokes its own UnityEvent.
            foreach (CustomDialogueOption customOption in _currentGossipMemory.CustomOptions)
            {
                if (!customOption.Enabled) continue;

                UnityEngine.Events.UnityEvent onSelected = customOption.OnSelected;
                string customLabel = string.IsNullOrEmpty(customOption.Label) ? "..." : customOption.Label;

                _currentOptions.Add(new DialogueOptionData
                {
                    Label = customLabel,
                    OnSelect = () => OnSelectCustomOption(onSelected),
                    Interactable = true
                });
            }
        }

        private void OnSelectCustomOption(UnityEngine.Events.UnityEvent onSelected)
        {
            onSelected?.Invoke();
            RefreshAfterAction();
        }

        private void OnSelectGreet()
        {
            _currentReputationOpinion?.TryApplyGreetBoost(_greetBoostAmount);
            RefreshAfterAction();
        }

        private void OnSelectAskAboutRumors()
        {
            _currentGossipMemory?.TryTellNextRumor();
            RefreshAfterAction();
        }

        /// <summary>Re-reads option data after a selection (e.g. cooldown state may have changed) without a crossfade.</summary>
        private void RefreshAfterAction()
        {
            RebuildOptionData();

            if (_useCarouselMode)
            {
                _carouselIndex = Mathf.Clamp(_carouselIndex, 0, Mathf.Max(0, _currentOptions.Count - 1));
                if (_carouselTransition != null) { StopCoroutine(_carouselTransition); _carouselTransition = null; }
                ApplyCurrentCarouselOption();
                if (_carouselOptionGroup != null) _carouselOptionGroup.alpha = 1f;
                if (_carouselOptionRect != null) _carouselOptionRect.anchoredPosition = _carouselRestingAnchoredPosition;
            }
            else
            {
                RebuildListButtons();
            }
        }

        // ---------- List mode ----------

        private void RebuildListButtons()
        {
            ClearListButtons();

            if (_optionButtonPrefab == null || _optionsContainer == null) return;

            foreach (DialogueOptionData option in _currentOptions)
            {
                Button buttonInstance = Instantiate(_optionButtonPrefab, _optionsContainer);
                buttonInstance.interactable = option.Interactable;

                TextMeshProUGUI labelText = buttonInstance.GetComponentInChildren<TextMeshProUGUI>();
                if (labelText != null)
                {
                    labelText.text = option.Label;
                }

                Action callback = option.OnSelect;
                buttonInstance.onClick.AddListener(() => callback());

                _spawnedListButtons.Add(buttonInstance.gameObject);
            }
        }

        private void ClearListButtons()
        {
            foreach (GameObject spawned in _spawnedListButtons)
            {
                if (spawned != null)
                {
                    Destroy(spawned);
                }
            }
            _spawnedListButtons.Clear();
        }

        // ---------- Carousel mode ----------

        private void ScrollCarousel(int direction)
        {
            if (_currentOptions.Count == 0) return;

            _carouselIndex = (_carouselIndex + direction + _currentOptions.Count) % _currentOptions.Count;
            StartCarouselTransition(direction);
        }

        private void OnCarouselOptionClicked()
        {
            if (_currentOptions.Count == 0) return;
            _currentOptions[_carouselIndex].OnSelect?.Invoke();
        }

        private void StartCarouselTransition(int direction)
        {
            if (_carouselTransition != null) StopCoroutine(_carouselTransition);
            _carouselTransition = StartCoroutine(CarouselCrossfadeRoutine(direction));
        }

        /// <summary>
        /// Slides + fades the outgoing option away, swaps content while invisible, then slides
        /// + fades the new option in from the opposite side back to rest. Direction convention:
        /// +1 (scrolling to the next option) exits upward and the new one enters from below;
        /// -1 (scrolling to the previous option) exits downward and the new one enters from above.
        /// </summary>
        // v6: BUG FIX — Phase 1 previously always started from the hardcoded resting
        // position/full alpha, regardless of where the element actually currently was. If a
        // scroll interrupted an in-progress transition (very likely when scrolling fast/a lot),
        // the new transition would visibly SNAP to that hardcoded start point before animating,
        // reading as jitter. Now reads the element's real current position/alpha at the moment
        // each transition begins, so interrupted transitions continue smoothly instead.
        private IEnumerator CarouselCrossfadeRoutine(int direction)
        {
            bool hasRect = _carouselOptionRect != null;
            bool hasGroup = _carouselOptionGroup != null;
            Vector2 exitOffset = new Vector2(0f, direction * _carouselSlideDistance);

            // Phase 1: outgoing slides away + fades out, starting from wherever it CURRENTLY is.
            if (hasGroup)
            {
                Vector2 currentPos = hasRect ? _carouselOptionRect.anchoredPosition : Vector2.zero;
                float currentAlpha = _carouselOptionGroup.alpha;
                Vector2 exitTarget = _carouselRestingAnchoredPosition + exitOffset;

                yield return AnimateSlideAndFade(currentPos, exitTarget, currentAlpha, 0f, _carouselFadeDuration, hasRect);
            }

            // Content swaps while fully invisible.
            ApplyCurrentCarouselOption();

            // Snap to the entry point (opposite side) instantly — still invisible, so no visible pop.
            if (hasRect)
            {
                _carouselOptionRect.anchoredPosition = _carouselRestingAnchoredPosition - exitOffset;
            }

            // Phase 2: incoming slides back to rest + fades in.
            if (hasGroup)
            {
                Vector2 entryStart = hasRect ? _carouselOptionRect.anchoredPosition : Vector2.zero;
                yield return AnimateSlideAndFade(entryStart, _carouselRestingAnchoredPosition, 0f, 1f, _carouselFadeDuration, hasRect);
            }
        }

        private IEnumerator AnimateSlideAndFade(Vector2 fromPos, Vector2 toPos, float fromAlpha, float toAlpha, float duration, bool animatePosition)
        {
            if (duration <= 0f)
            {
                if (animatePosition && _carouselOptionRect != null) _carouselOptionRect.anchoredPosition = toPos;
                if (_carouselOptionGroup != null) _carouselOptionGroup.alpha = toAlpha;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                if (animatePosition && _carouselOptionRect != null)
                {
                    _carouselOptionRect.anchoredPosition = Vector2.Lerp(fromPos, toPos, t);
                }

                if (_carouselOptionGroup != null)
                {
                    _carouselOptionGroup.alpha = Mathf.Lerp(fromAlpha, toAlpha, t);
                }

                yield return null;
            }

            if (animatePosition && _carouselOptionRect != null) _carouselOptionRect.anchoredPosition = toPos;
            if (_carouselOptionGroup != null) _carouselOptionGroup.alpha = toAlpha;
        }

        private void ApplyCurrentCarouselOption()
        {
            if (_currentOptions.Count == 0)
            {
                if (_carouselOptionLabel != null) _carouselOptionLabel.text = "...";
                if (_carouselOptionButton != null) _carouselOptionButton.interactable = false;
                if (_carouselIndexText != null) _carouselIndexText.text = "";
                return;
            }

            DialogueOptionData current = _currentOptions[_carouselIndex];

            if (_carouselOptionLabel != null) _carouselOptionLabel.text = current.Label;
            if (_carouselOptionButton != null) _carouselOptionButton.interactable = current.Interactable;
            if (_carouselIndexText != null) _carouselIndexText.text = $"{_carouselIndex + 1} / {_currentOptions.Count}";
        }
    }
}