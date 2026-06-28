using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Project.Architecture;
using Project.Data;

namespace Project.Services
{
    // NOTE: Now, we build the engine implementation. This class constructs a highly structured,
    // data-packed prompt detailing the NPC's gender, mood, and voice rules, and pushes it
    // asynchronously to a lightweight local server

    /// <summary>
    /// Communicates with a localized offline LLM server running natively on localhost.
    /// </summary>
    public class LocalAiWebClient : IAiGenerationService
    {
        private const string LocalUrl = "http://localhost:11434/api/generate"; // Replace with your local server URL
        private const string ModelName = "llama3";

        public async Task<string> GenerateGossipDialogueAsync(AiPromptContext context)
        {
            // First, we construct a detailed prompt injecting our dynamic profile variables
            string systemRules = $"System: You are a {context.Gender} NPC named {context.SpeakerID} in a medieval simulation. Your voice style is {context.VocalStyle}. ";
            string contextRules = $"Context: You are whispering gossip to a passing Player. Current mood: {context.CurrentToneName}. ";
            string factualData = $"Fact to convey: {context.RumorCoreFact}. Belief score: {context.PersonalCredibilityScore * 100}%. ";

            // OPTIMIZED CONSTRAINT: Explicitly demand brevity to speed up generation
            string taskRules = "Task: Generate exactly ONE extremely short, brief dialogue sentence whispering this rumor. Maximum 10-15 words total. No meta-text, no action text, no quotation marks.";

            // We combine into one completely flat string and sanitize any bad characters
            string fullPrompt = $"{systemRules}{contextRules}{factualData}{taskRules}";
            fullPrompt = fullPrompt.Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", " ");

            // Second We format into a structurally standard JSON contract payload with token optimization limits
            string jsonPayload = "{ " +
                                 $"\"model\": \"{ModelName}\", " +
                                 $"\"prompt\": \"{fullPrompt}\", " +
                                 "\"stream\": false, " +
                                 "\"options\": { \"num_predict\": 30 } " + // NEW HOOK: Hard ceiling to force short execution times
                                 "}";

            // Third, we send the request to the local server asynchronously
            using (UnityWebRequest request = new UnityWebRequest(LocalUrl, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    await Task.Yield(); // Keeps our frame rate high and smooth
                }

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string responseText = request.downloadHandler.text;

                    try
                    {
                        // Parse the raw string into our data container object
                        OllamaResponse parsedData = JsonUtility.FromJson<OllamaResponse>(responseText);

                        // Check if the response field actually filled up safely
                        if (parsedData != null && !string.IsNullOrEmpty(parsedData.response))
                        {
                            return parsedData.response;
                        }

                        Debug.LogWarning("[Local AI Client] JSON parsed, but 'response' field was empty. Returning raw text.");
                        return responseText;
                    }
                    catch (System.Exception ex)
                    {
                        // If Unity throws a parsing error, log it here so we can see it!
                        Debug.LogError($"[Local AI Client] JSON Parsing Exception: {ex.Message}");
                        return responseText;
                    }
                }
                else
                {
                    // Graceful local fallback snippet if the background local engine is loading or waking up
                    Debug.LogWarning($"[Local AI Client] Offline server unreachable. Using fallback text context: {request.error}");
                    return $"[Mumbles anxiously in a {context.VocalStyle} voice about {context.RumorCoreFact.Substring(0, Mathf.Min(15, context.RumorCoreFact.Length))}...]";
                }
            }
        }
    }

    /// <summary>
    /// Lightweight data container matching Ollama's expected API JSON layout.
    /// </summary>
    [System.Serializable]
    public class OllamaResponse
    {
        public string model;
        public string response;
        public bool done;
    }
}