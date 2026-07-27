using UnityEngine;
using UnityEngine.Events;
using TownsPeople.Data;

namespace TownsPeople.GamePlay
{
    /// <summary>
    /// Per-NPC configuration for one dialogue menu option: whether it appears at all, an
    /// optional custom label overriding the default (supports a {NpcName} token, substituted
    /// with this NPC's actual name at display time), and optional gendered audio played when
    /// selected.
    /// </summary>
    [System.Serializable]
    public struct DialogueOptionSettings
    {
        [Tooltip("If disabled, this option never appears in the dialogue menu for this NPC.")]
        public bool Enabled;

        [Tooltip("Label shown for this option. Supports a {NpcName} token, replaced with this NPC's actual name.")]
        public string CustomLabel;

        [Tooltip("Optional. Played when the reacting NPC's Voice Gender is set to Male.")]
        public AudioClip MaleAudio;

        [Tooltip("Optional. Played when the reacting NPC's Voice Gender is set to Female.")]
        public AudioClip FemaleAudio;

        /// <summary>Returns the clip matching the requested gender, falling back to the other gender's clip if only one is assigned.</summary>
        public AudioClip GetVoiceLine(VoiceGender gender)
        {
            AudioClip preferred = gender == VoiceGender.Male ? MaleAudio : FemaleAudio;
            if (preferred != null) return preferred;
            return gender == VoiceGender.Male ? FemaleAudio : MaleAudio;
        }
    }

    /// <summary>
    /// A fully custom, NPC-authored dialogue option. Wire OnSelected to any method on any
    /// component directly in the Inspector — no code required per new option. Unlike Greet/
    /// Rumors, these have no built-in conditional-interactability logic; they're always
    /// available whenever Enabled.
    /// </summary>
    [System.Serializable]
    public struct CustomDialogueOption
    {
        [Tooltip("If disabled, this option never appears.")]
        public bool Enabled;

        [Tooltip("Text shown for this option in the dialogue menu.")]
        public string Label;

        [Tooltip("Optional. Played when the reacting NPC's Voice Gender is set to Male.")]
        public AudioClip MaleAudio;

        [Tooltip("Optional. Played when the reacting NPC's Voice Gender is set to Female.")]
        public AudioClip FemaleAudio;

        [Tooltip("Invoked when the player selects this option. Wire this to any method on any component.")]
        public UnityEvent OnSelected;

        /// <summary>Returns the clip matching the requested gender, falling back to the other gender's clip if only one is assigned.</summary>
        public AudioClip GetVoiceLine(VoiceGender gender)
        {
            AudioClip preferred = gender == VoiceGender.Male ? MaleAudio : FemaleAudio;
            if (preferred != null) return preferred;
            return gender == VoiceGender.Male ? FemaleAudio : MaleAudio;
        }
    }
}