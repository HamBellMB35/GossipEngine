using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Project.UI;
using Project.Data;

namespace Project.GamePlay
{
    public class NPCGossipMemory : MonoBehaviour
    {
        public string NpcName;
        public List<string> KnownRumors = new List<string>();
        public string openAIApiKey = "YOUR_KEY_HERE";

        public void LearnRumor(RumorTemplate rumor, float credibility)
        {
            KnownRumors.Add(rumor.RumorID);
        }

        private string SanitizeRumor(string raw)
        {
            string clean = raw;
            if (clean.Contains("]")) clean = clean.Substring(clean.LastIndexOf(']') + 1);
            if (clean.Contains(":")) clean = clean.Substring(clean.IndexOf(':') + 1);

            string[] noise = { "Weight", "Stats", "(", ")", "[", "]" };
            foreach (var n in noise) clean = clean.Replace(n, "");

            return clean.Trim();
        }

        public void OnGenerativeAIInteract() => StartCoroutine(GetAIResponse());

        private IEnumerator GetAIResponse()
        {
            string rawRumor = KnownRumors.Count > 0 ? KnownRumors[KnownRumors.Count - 1] : "The weather is nice.";
            string cleanRumor = SanitizeRumor(rawRumor);

            string json = JsonUtility.ToJson(new
            {
                model = "llama3",
                messages = new[] { new { role = "user", content = "Tell me this news in one short sentence: " + cleanRumor } }
            });

            using (UnityWebRequest req = new UnityWebRequest("http://localhost:11434/v1/chat/completions", "POST"))
            {
                byte[] body = System.Text.Encoding.UTF8.GetBytes(json);
                req.uploadHandler = new UploadHandlerRaw(body);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    string aiText = JsonUtility.FromJson<ChatResponse>(req.downloadHandler.text).choices[0].message.content;
                    yield return StartCoroutine(PlayAudio(aiText));
                }
            }
        }

        private IEnumerator PlayAudio(string text)
        {
            string json = JsonUtility.ToJson(new { model = "tts-1", input = text, voice = "alloy" });

            using (UnityWebRequest req = UnityWebRequestMultimedia.GetAudioClip("https://api.openai.com/v1/audio/speech", AudioType.MPEG))
            {
                req.method = "POST";
                req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", "Bearer " + openAIApiKey);
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    AudioSource src = GetComponent<AudioSource>();
                    src.clip = DownloadHandlerAudioClip.GetContent(req);
                    src.Play();
                }
            }
        }

        [System.Serializable] public class ChatResponse { public List<Choice> choices; }
        [System.Serializable] public class Choice { public Message message; }
        [System.Serializable] public class Message { public string content; }
    }
}