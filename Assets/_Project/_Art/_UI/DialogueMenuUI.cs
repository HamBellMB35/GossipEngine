using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.Video;
using TMPro;
using Project.GamePlay;
using Project.Data;

namespace Project.UI
{
    /// <summary>
    /// Shared, single dialogue menu UI — one instance in the scene, opened and repopulated for
    /// whichever NPC the player is currently talking to. Supports two display modes:
    /// - List: all options shown in a scrollable list at once (classic menu).
    /// - Carousel: one option shown at a time; scroll wheel browses, with a crossfade
    ///   transition (fade out -> swap content -> fade in) between options. Click to select
    ///   whichever option is currently showing.
    ///
    /// v12: Two-level navigation. The main view shows Greet / "What do you hear on the
    /// streets?" / custom options. Selecting "What do you hear" navigates into a rumor
    /// sub-list (every rumor this NPC knows, plus Back) — both display modes support this
    /// automatically, since it's rendered through the same generic option-data pipeline.
    /// Picking a specific rumor shows its text in a dedicated popup (not the world-space
    /// bubble), closable via its own [X] button or Escape.
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

        [Header("Option Text Colors")]
        [Tooltip("Text color for an option in its normal/available state.")]
        [SerializeField] private Color _normalOptionColor = Color.white;

        [Tooltip("Text color for an option that's been \"used\" — Greet while on cooldown, or a rumor that's already been heard. Still clickable; this is purely a visual indicator.")]
        [SerializeField] private Color _usedOptionColor = new Color(0.5f, 0.5f, 0.5f, 1f);

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

        [Header("Rumor Popup")]
        [Tooltip("Shown on top of the dialogue panel when the player picks a specific rumor from the sub-list.")]
        [SerializeField] private CanvasGroupFader _rumorPopupFader;
        [SerializeField] private TextMeshProUGUI _rumorPopupText;
        [SerializeField] private Button _rumorPopupCloseButton;

        [Header("Rumor Popup — Portrait")]
        [Tooltip("Static portrait area. Shown when the current NPC has a Portrait Image and no Portrait Video.")]
        [SerializeField] private Image _popupPortraitImage;
        [Tooltip("Video portrait area. Shown when the current NPC has a Portrait Video assigned (takes priority over the static image).")]
        [SerializeField] private RawImage _popupPortraitVideoImage;
        [SerializeField] private VideoPlayer _popupVideoPlayer;

        [Header("Click Sound")]
        [Tooltip("Optional UI click sound played on every deliberate interaction click — opening via [E], selecting any option, Back, Leave, closing the popup (by button or Escape). NOT played when things close automatically from walking away.")]
        [SerializeField] private AudioClip _clickSound;
        [SerializeField] private AudioSource _clickAudioSource;

        [Header("Placement")]
        [Tooltip("Anchored position of the whole conversation panel (offset from its anchor point). Adjust anytime to move the panel — updates live in the Editor, no need to enter Play mode.")]
        [SerializeField] private Vector2 _panelAnchoredPosition = Vector2.zero;

        [Tooltip("Size of the conversation panel.")]
        [SerializeField] private Vector2 _panelSize = new Vector2(420f, 480f);

        private RectTransform _panelRectTransform;

        private enum MenuView { MainOptions, RumorList }
        private MenuView _currentView = MenuView.MainOptions;

        private struct DialogueOptionData
        {
            public string Label;
            public Action OnSelect;
            public bool Interactable;
            public bool UseDarkenedColor;
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

        // Drives the live Greet cooldown color update — see UpdateGreetCooldownDisplay().
        private const float CooldownRefreshIntervalSeconds = 1f;
        private float _cooldownRefreshTimer;
        private bool _wasOnGreetCooldown;
        private RectTransform _carouselOptionRect;
        private Vector2 _carouselRestingAnchoredPosition;
        private bool _carouselRestPositionCaptured;

        private bool _isRumorPopupOpen;
        private RenderTexture _portraitRenderTexture;

        private void Awake()
        {
            Instance = this;

            // v13: Wired to "clicked" wrapper methods (which play the click sound then call
            // the real, silent logic) instead of calling Close()/CloseRumorPopup() directly —
            // those are also called programmatically when the player walks away, and that path
            // must stay silent.
            if (_leaveButton != null) _leaveButton.onClick.AddListener(OnLeaveButtonClicked);
            if (_carouselOptionButton != null) _carouselOptionButton.onClick.AddListener(OnCarouselOptionClicked);
            if (_rumorPopupCloseButton != null) _rumorPopupCloseButton.onClick.AddListener(OnPopupCloseClicked);

            _panelFader?.SetInstant(false);
            _rumorPopupFader?.SetInstant(false);

            if (_listModeRoot != null) _listModeRoot.SetActive(!_useCarouselMode);
            if (_carouselModeRoot != null) _carouselModeRoot.SetActive(_useCarouselMode);

            ApplyPanelPlacement();

            if (_carouselOptionGroup != null)
            {
                _carouselOptionRect = _carouselOptionGroup.GetComponent<RectTransform>();
                // Position is deliberately NOT captured here — see
                // EnsureCarouselRestPositionCaptured(), called from Open() instead.
            }

            if (_popupVideoPlayer != null && _popupPortraitVideoImage != null)
            {
                _portraitRenderTexture = new RenderTexture(256, 256, 0);
                _popupVideoPlayer.renderMode = VideoRenderMode.RenderTexture;
                _popupVideoPlayer.targetTexture = _portraitRenderTexture;
                _popupPortraitVideoImage.texture = _portraitRenderTexture;
            }
        }

        private void OnValidate()
        {
            ApplyPanelPlacement();
        }

        private void ApplyPanelPlacement()
        {
            if (_panelRectTransform == null)
            {
                _panelRectTransform = GetComponent<RectTransform>();
            }

            if (_panelRectTransform != null)
            {
                _panelRectTransform.anchoredPosition = _panelAnchoredPosition;
                _panelRectTransform.sizeDelta = _panelSize;
            }
        }

        private void Update()
        {
            if (!_isOpen) return;

            UpdateGreetCooldownDisplay();

            // v13: Routed through OnPopupCloseClicked (plays the click sound, and only does
            // anything if the popup is actually open — avoids firing sound on every stray
            // Escape press when nothing's showing).
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                OnPopupCloseClicked();
            }

            if (!_useCarouselMode) return;
            if (Mouse.current == null) return;

            float scroll = Mouse.current.scroll.y.value;
            if (scroll > 0.01f) ScrollCarousel(-1);
            else if (scroll < -0.01f) ScrollCarousel(1);
        }

        /// <summary>
        /// Ticks the Greet cooldown color state roughly once per second while actually on
        /// cooldown, and fires one final refresh the instant it becomes available again so the
        /// color snaps back to normal immediately instead of waiting out the last interval.
        /// </summary>
        private void UpdateGreetCooldownDisplay()
        {
            if (_currentReputationOpinion == null) return;

            bool isOnCooldown = !_currentReputationOpinion.CanGreet();

            if (!isOnCooldown)
            {
                if (_wasOnGreetCooldown)
                {
                    _wasOnGreetCooldown = false;
                    _cooldownRefreshTimer = 0f;
                    RefreshAfterAction();
                }
                return;
            }

            _wasOnGreetCooldown = true;
            _cooldownRefreshTimer += Time.deltaTime;
            if (_cooldownRefreshTimer < CooldownRefreshIntervalSeconds) return;

            _cooldownRefreshTimer = 0f;
            RefreshAfterAction();
        }

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
            _cooldownRefreshTimer = 0f;
            _wasOnGreetCooldown = false;
            _currentView = MenuView.MainOptions;

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
            CloseRumorPopup();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            _currentGossipMemory?.HideSpeechBubble();

            Action callback = _onClosedCallback;

            _isOpen = false;
            _currentGossipMemory = null;
            _currentReputationOpinion = null;
            _onClosedCallback = null;
            _currentView = MenuView.MainOptions;
            ClearListButtons();
            _currentOptions.Clear();

            callback?.Invoke();
        }

        /// <summary>Called when the player deliberately clicks Leave — plays the click sound, then does the real (silent) close.</summary>
        private void OnLeaveButtonClicked()
        {
            PlayClickSound();
            Close();
        }

        /// <summary>Plays the shared UI click sound, if one is assigned. Safe to call with no clip set.</summary>
        public void PlayClickSound()
        {
            if (_clickSound == null || _clickAudioSource == null) return;
            _clickAudioSource.PlayOneShot(_clickSound);
        }

        /// <summary>
        /// True if the menu is currently open specifically for the given NPC. Used by
        /// NPCProximityGossip to force-close the menu if the player walks out of that NPC's
        /// trigger zone while still mid-conversation with it.
        /// </summary>
        public bool IsOpenFor(NPCGossipMemory memory)
        {
            return _isOpen && _currentGossipMemory == memory;
        }

        // ---------- Option data (view-aware) ----------

        private void RebuildOptionData()
        {
            _currentOptions.Clear();

            if (_currentGossipMemory == null) return;

            if (_currentView == MenuView.RumorList)
            {
                BuildRumorListOptions();
                return;
            }

            BuildMainOptions();
        }

        private void BuildMainOptions()
        {
            DialogueOptionSettings greetSettings = _currentGossipMemory.GreetOptionSettings;
            if (greetSettings.Enabled)
            {
                bool canGreet = _currentReputationOpinion == null || _currentReputationOpinion.CanGreet();

                // v24: CustomLabel is never blank now (populated with real default text at
                // field-definition time) — just substitute the {NpcName} token instead of the
                // old "is it empty" branch.
                string baseLabel = greetSettings.CustomLabel.Replace("{NpcName}", _currentNpcName);

                string greetLabel = canGreet
                    ? baseLabel
                    : $"{baseLabel} (wait {Mathf.CeilToInt(_currentReputationOpinion.GetGreetCooldownRemaining())}s)";

                _currentOptions.Add(new DialogueOptionData
                {
                    Label = greetLabel,
                    OnSelect = OnSelectGreet,
                    Interactable = true,
                    UseDarkenedColor = !canGreet
                });
            }

            DialogueOptionSettings rumorsSettings = _currentGossipMemory.RumorsOptionSettings;
            if (rumorsSettings.Enabled && _currentGossipMemory.KnownRumors.Count > 0)
            {
                string label = rumorsSettings.CustomLabel.Replace("{NpcName}", _currentNpcName);

                _currentOptions.Add(new DialogueOptionData
                {
                    Label = label,
                    OnSelect = OnSelectOpenRumorList,
                    Interactable = true,
                    UseDarkenedColor = false
                });
            }

            foreach (CustomDialogueOption customOption in _currentGossipMemory.CustomOptions)
            {
                if (!customOption.Enabled) continue;

                UnityEngine.Events.UnityEvent onSelected = customOption.OnSelected;
                AudioClip maleAudio = customOption.MaleAudio;
                AudioClip femaleAudio = customOption.FemaleAudio;
                string customLabel = string.IsNullOrEmpty(customOption.Label) ? "New Option" : customOption.Label;

                _currentOptions.Add(new DialogueOptionData
                {
                    Label = customLabel,
                    OnSelect = () => OnSelectCustomOption(onSelected, maleAudio, femaleAudio),
                    Interactable = true,
                    UseDarkenedColor = false
                });
            }
        }

        /// <summary>
        /// v12: One entry per rumor this NPC currently knows, plus a Back entry. Each rumor's
        /// label is its stable RumorDisplayText/RumorID — NOT whichever tiered response would
        /// actually play, since that's resolved only at selection time. Already-presented
        /// rumors (HasBeenPresented) render darkened but remain fully clickable.
        /// </summary>
        private void BuildRumorListOptions()
        {
            _currentOptions.Add(new DialogueOptionData
            {
                Label = "< Back",
                OnSelect = OnSelectBackToMainOptions,
                Interactable = true,
                UseDarkenedColor = false
            });

            foreach (RuntimeRumorState state in _currentGossipMemory.KnownRumors)
            {
                if (state.SourceTemplate == null) continue;

                RumorTemplate rumor = state.SourceTemplate;

                // v15: Preview the ACTUAL tiered response (Specific/General/Default) instead
                // of always showing the static RumorDisplayText fallback — matches what
                // clicking the entry will actually produce.
                string label = _currentGossipMemory.PeekRumorPreviewText(rumor);
                if (string.IsNullOrEmpty(label))
                {
                    label = !string.IsNullOrEmpty(rumor.RumorDisplayText) ? rumor.RumorDisplayText : rumor.RumorID;
                }

                bool alreadyHeard = state.HasBeenPresented;

                _currentOptions.Add(new DialogueOptionData
                {
                    Label = label,
                    OnSelect = () => OnSelectRumorEntry(rumor),
                    Interactable = true,
                    UseDarkenedColor = alreadyHeard
                });
            }
        }

        // ---------- Selection handlers ----------

        private void OnSelectGreet()
        {
            PlayClickSound();
            _currentReputationOpinion?.TryApplyGreetBoost(_greetBoostAmount);

            // v24: Optional gendered audio for this option, separate from any rumor's audio.
            DialogueOptionSettings greetSettings = _currentGossipMemory != null ? _currentGossipMemory.GreetOptionSettings : default;
            _currentGossipMemory?.PlayOptionAudio(greetSettings.MaleAudio, greetSettings.FemaleAudio);

            RefreshAfterAction();
        }

        private void OnSelectOpenRumorList()
        {
            PlayClickSound();

            DialogueOptionSettings rumorsSettings = _currentGossipMemory != null ? _currentGossipMemory.RumorsOptionSettings : default;
            _currentGossipMemory?.PlayOptionAudio(rumorsSettings.MaleAudio, rumorsSettings.FemaleAudio);

            _currentView = MenuView.RumorList;
            RefreshAfterAction();
        }

        private void OnSelectBackToMainOptions()
        {
            PlayClickSound();
            _currentView = MenuView.MainOptions;
            RefreshAfterAction();
        }

        private void OnSelectRumorEntry(RumorTemplate rumor)
        {
            PlayClickSound();
            string resolvedText = _currentGossipMemory?.PresentRumorForPopup(rumor);
            ShowRumorPopup(resolvedText);
            RefreshAfterAction(); // Updates that entry's color to darkened now that it's been heard.
        }

        private void OnSelectCustomOption(UnityEngine.Events.UnityEvent onSelected, AudioClip maleAudio, AudioClip femaleAudio)
        {
            PlayClickSound();
            _currentGossipMemory?.PlayOptionAudio(maleAudio, femaleAudio);
            onSelected?.Invoke();
            RefreshAfterAction();
        }

        // ---------- Rumor popup ----------

        private void ShowRumorPopup(string text)
        {
            if (_rumorPopupText != null)
            {
                _rumorPopupText.text = string.IsNullOrEmpty(text) ? "..." : text;
            }

            ApplyPopupPortrait();

            _rumorPopupFader?.Show();
            _isRumorPopupOpen = true;
        }

        /// <summary>
        /// v13: Shows the current NPC's static portrait or video (video takes priority if
        /// assigned), hiding whichever isn't in use.
        /// </summary>
        private void ApplyPopupPortrait()
        {
            VideoClip video = _currentGossipMemory != null ? _currentGossipMemory.PortraitVideo : null;
            Sprite portrait = _currentGossipMemory != null ? _currentGossipMemory.PortraitImage : null;

            bool useVideo = video != null && _popupVideoPlayer != null && _popupPortraitVideoImage != null;

            if (_popupPortraitVideoImage != null) _popupPortraitVideoImage.gameObject.SetActive(useVideo);
            if (_popupPortraitImage != null) _popupPortraitImage.gameObject.SetActive(!useVideo && portrait != null);

            if (useVideo)
            {
                _popupVideoPlayer.clip = video;
                _popupVideoPlayer.isLooping = true;
                _popupVideoPlayer.Play();
            }
            else
            {
                _popupVideoPlayer?.Stop();
                if (_popupPortraitImage != null) _popupPortraitImage.sprite = portrait;
            }
        }

        /// <summary>Silent — called both by the deliberate-click path and programmatically when the player walks away.</summary>
        private void CloseRumorPopup()
        {
            _rumorPopupFader?.Hide();
            _popupVideoPlayer?.Stop();
            _isRumorPopupOpen = false;
        }

        /// <summary>Called when the popup's [X] is clicked, or Escape is pressed — plays the click sound only if the popup was actually open.</summary>
        private void OnPopupCloseClicked()
        {
            if (!_isRumorPopupOpen) return;

            PlayClickSound();
            CloseRumorPopup();
        }

        /// <summary>Re-reads option data after a selection (e.g. cooldown/read state may have changed) without a crossfade.</summary>
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
                    labelText.color = option.UseDarkenedColor ? _usedOptionColor : _normalOptionColor;
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

        private IEnumerator CarouselCrossfadeRoutine(int direction)
        {
            bool hasRect = _carouselOptionRect != null;
            bool hasGroup = _carouselOptionGroup != null;
            Vector2 exitOffset = new Vector2(0f, direction * _carouselSlideDistance);

            if (hasGroup)
            {
                Vector2 currentPos = hasRect ? _carouselOptionRect.anchoredPosition : Vector2.zero;
                float currentAlpha = _carouselOptionGroup.alpha;
                Vector2 exitTarget = _carouselRestingAnchoredPosition + exitOffset;

                yield return AnimateSlideAndFade(currentPos, exitTarget, currentAlpha, 0f, _carouselFadeDuration, hasRect);
            }

            ApplyCurrentCarouselOption();

            if (hasRect)
            {
                _carouselOptionRect.anchoredPosition = _carouselRestingAnchoredPosition - exitOffset;
            }

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
                if (_carouselOptionLabel != null)
                {
                    _carouselOptionLabel.text = "...";
                    _carouselOptionLabel.color = _normalOptionColor;
                }
                if (_carouselOptionButton != null) _carouselOptionButton.interactable = false;
                if (_carouselIndexText != null) _carouselIndexText.text = "";
                return;
            }

            DialogueOptionData current = _currentOptions[_carouselIndex];

            if (_carouselOptionLabel != null)
            {
                _carouselOptionLabel.text = current.Label;
                _carouselOptionLabel.color = current.UseDarkenedColor ? _usedOptionColor : _normalOptionColor;
            }
            if (_carouselOptionButton != null) _carouselOptionButton.interactable = current.Interactable;
            if (_carouselIndexText != null) _carouselIndexText.text = $"{_carouselIndex + 1} / {_currentOptions.Count}";
        }
    }
}