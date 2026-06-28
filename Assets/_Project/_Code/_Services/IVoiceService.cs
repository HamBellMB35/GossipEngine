using System;

namespace Project.Services
{
    // NOTE: This interface acts as our abstract boundary for audio text-to-speech engines.
    // By keeping it strictly abstract, our character systems remain completely blind to 
    // whether the speech is generated locally, via the cloud, or by a preset audio clip file.

    public interface IVoiceService
    {
        /// <summary>
        /// Pass a text string alongside specified character attributes to be synthesized into spoken audio data.
        /// </summary>
        /// <param name="textToSpeak">The raw character dialogue text string to render.</param>
        /// <param name="voicePersona">The target profile identity name, matching OpenAI's accepted values.</param>
        /// <param name="gender">The character's descriptive gender string used to calculate smart fallbacks.</param>
        /// <param name="onSpeechStarted">A callback action that fires the exact moment the hardware audio playback begins.</param>
        void SpeakGossip(string textToSpeak, string voicePersona = "", string gender = "", Action onSpeechStarted = null);
    }
}