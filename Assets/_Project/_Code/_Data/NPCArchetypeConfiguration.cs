using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Data
{
    // NOTE: Structural enums to track rig styles and choice matrices across your asset package layers.
    public enum AnimationRigType { Humanoid, Generic }
    public enum ConversationalBrainType { FixedScripted, GenerativeAI }
    public enum EmotionalState { Neutral, Happy, Scared, Sad, Angry }

    /// <summary>
    /// ScriptableObject data container serving as the primary design profile for your NPC Creator tool pipeline.
    /// Exposes absolute control over text strings, colors, scales, and spatial positioning layout fields inside the Editor.
    /// </summary>
    [CreateAssetMenu(fileName = "NewNPCConfiguration", menuName = "NPC Creator/Archetype Configuration")]
    public class NPCArchetypeConfiguration : ScriptableObject
    {
        [Header("Identity & Framework Design")]
        [Tooltip("The default name assigned to this character prefab anchor node.")]
        public string DefaultName = "Townsperson";
        public AnimationRigType RigStyle = AnimationRigType.Humanoid;
        public ConversationalBrainType BrainStyle = ConversationalBrainType.FixedScripted;

        [Header("Proximity UI Configuration (Editable)")]
        [Tooltip("The text string shown when the player steps inside the trigger boundary. (e.g., 'Talk [E]', 'Interact [F]')")]
        public string InteractionPromptText = "Talk [E]";

        [Tooltip("The vertical 3D world space offset height position where the dialogue canvas floats above the character pivot.")]
        public float UiVerticalOffsetHeight = 2.2f;

        [Tooltip("The background sprite graphic placeholder panel layout canvas size bounds.")]
        public Vector2 SpeechBubbleCanvasSize = new Vector2(400f, 200f);

        [Tooltip("The custom scale multiplier to correctly size text and panel visuals down into native 3D world space metric units.")]
        public Vector3 CanvasWorldScale = new Vector3(0.01f, 0.01f, 0.01f);

        [Header("Visual Placeholder Styling")]
        [Tooltip("The default color overlay applied to the graphic placeholder panel mask behind speech strings.")]
        public Color SpeechBackgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.85f);

        [Tooltip("The default color overlay applied to your premium full screen marketplace transaction backing panels.")]
        public Color MerchantMarketBackgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.95f);

        [Header("Base Emotional Thresholds")]
        [Range(0f, 1f)] public float InitialJoy = 0.5f;
        [Range(0f, 1f)] public float InitialFear = 0.0f;
        [Range(0f, 1f)] public float InitialSadness = 0.0f;

        // --- Deterministic Data Structure Matrix ---
        [Header("Scripted Dialogue Pool (Used if FixedScripted)")]
        [Tooltip("The pool of possible random lines this NPC can murmur completely offline if no extensions hijack the interaction call.")]
        public List<ScriptedResponsePacket> ScriptedDialogues = new List<ScriptedResponsePacket>();
    }

    /// <summary>
    /// Specialized data structure grouping text responses with dedicated hardware audio clips.
    /// </summary>
    [Serializable]
    public struct ScriptedResponsePacket
    {
        // Internal data reference to check matching engine emotional states before sorting pools
        public EmotionalState RequiredState;

        [TextArea(2, 5)]
        public string ResponseText;

        // Pre-recorded audio asset clip provided by the developer to play alongside the text canvas overlays
        public AudioClip VoiceLineAudio;
    }
}