using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Project.Services
{
    [RequireComponent(typeof(AudioSource))]
    public class OpenAITTSVoice : MonoBehaviour, IVoiceService
    {
        [Header("API Configuration")]
        [SerializeField] private string apiKey = "PASTE_YOUR_OPENAI_KEY_HERE";
        [SerializeField] private string modelName = "tts-1";

        [Header("Smart Fallback Options")]
        [SerializeField] private string defaultFemaleVoice = "alloy";
        [SerializeField] private string defaultMaleVoice = "echo";

        [Header("Timing Calibration")]
        [Range(-0.5f, 1f)]
        [Tooltip("Fine-tune text pop sync. If text pops too early, move slider up. If text lags behind, move slider down.")]
        [SerializeField] private float textLatencyCompensation = 0.05f;

        private AudioSource audioSource;
        private const string OpenAiUrl = "https://api.openai.com/v1/audio/speech";

        [Serializable]
        private struct OpenAiTtsPayload
        {
            public string model;
            public string input;
            public string voice;
        }

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();

            // SYSTEM COUPLING: Automatically read the secure token straight out of your machine's environment RAM
            if (string.IsNullOrEmpty(apiKey))
            {
                apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY", EnvironmentVariableTarget.User);
            }

            // Local Fallback: If you don't use environment variables, we can look for a local text file that Git ignores
            if (string.IsNullOrEmpty(apiKey))
            {
                string hiddenPath = System.IO.Path.Combine(Application.dataPath, "../secret_key.txt");
                if (System.IO.File.Exists(hiddenPath))
                {
                    apiKey = System.IO.File.ReadAllText(hiddenPath).Trim();
                }
            }
        }

        public void SpeakGossip(string textToSpeak, string voicePersona = "", string gender = "", Action onSpeechStarted = null)
        {
            // DYNAMIC RUNTIME RESOLUTION: Check for the file on-the-fly if the RAM key variable is blank
            if (string.IsNullOrEmpty(apiKey) || apiKey == "PASTE_YOUR_OPENAI_KEY_HERE")
            {
                // Look for our local text file sitting right outside the Assets folder
                string hiddenPath = System.IO.Path.Combine(Application.dataPath, "../secret_key.txt");
                if (System.IO.File.Exists(hiddenPath))
                {
                    apiKey = System.IO.File.ReadAllText(hiddenPath).Trim();
                    Debug.Log("<color=cyan>[Key Resolved]</color> Secure API key loaded dynamically from secret_key.txt at runtime.");
                }
            }

            // Standard safety check follows immediately after
            if (string.IsNullOrEmpty(apiKey) || apiKey == "PASTE_YOUR_OPENAI_KEY_HERE")
            {
                Debug.LogError("[OpenAITTSVoice] Safety abort triggered: Please provide a valid OpenAI API Key inside 'secret_key.txt'.");
                return;
            }

            if (string.IsNullOrEmpty(textToSpeak)) return;

            string sanitizedPersona = voicePersona.ToLower().Trim();
            string sanitizedGender = gender.ToLower().Trim();

            bool isValidVoice = sanitizedPersona == "alloy" ||
                                sanitizedPersona == "echo" ||
                                sanitizedPersona == "fable" ||
                                sanitizedPersona == "onyx" ||
                                sanitizedPersona == "nova" ||
                                sanitizedPersona == "shimmer";

            string targetVoice = defaultFemaleVoice;

            if (isValidVoice)
            {
                targetVoice = sanitizedPersona;
            }
            else
            {
                if (sanitizedGender == "male" || sanitizedGender == "m")
                {
                    targetVoice = defaultMaleVoice;
                }
                else
                {
                    targetVoice = defaultFemaleVoice;
                }
            }

            StartCoroutine(DownloadVoiceRoutine(textToSpeak, targetVoice, onSpeechStarted));
        }

        private IEnumerator DownloadVoiceRoutine(string text, string targetVoice, Action onSpeechStarted)
        {
            OpenAiTtsPayload payload = new OpenAiTtsPayload
            {
                model = modelName,
                input = text,
                voice = targetVoice
            };

            string jsonPayload = JsonUtility.ToJson(payload);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);

            using (UnityWebRequest request = new UnityWebRequest(OpenAiUrl, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerAudioClip(OpenAiUrl, AudioType.MPEG);

                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", "Bearer " + apiKey);

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    AudioClip clip = DownloadHandlerAudioClip.GetContent(request);

                    if (clip != null)
                    {
                        audioSource.clip = clip;

                        // 1. Kick off audio source rendering first to clear underlying hardware loops
                        audioSource.Play();

                        // 2. Route our layout text canvas update through the latency compensation filter
                        if (onSpeechStarted != null)
                        {
                            StartCoroutine(DelayedCallbackRoutine(textLatencyCompensation, onSpeechStarted));
                        }
                    }
                }
                else
                {
                    Debug.LogError($"[OpenAITTSVoice] API Web Service Request Error: {request.error}");
                }
            }
        }

        /// <summary>
        /// Introduces a non-blocking timeline offset to counteract hardware sound card buffering lags.
        /// </summary>
        private IEnumerator DelayedCallbackRoutine(float delaySeconds, Action callback)
        {
            // If delay values are greater than zero, hold back UI calculation loops
            if (delaySeconds > 0f)
            {
                yield return new WaitForSeconds(delaySeconds);
            }

            callback?.Invoke();
        }
    }
}