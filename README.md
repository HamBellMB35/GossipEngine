

```markdown
# Data-Driven NPC Gossip & Social Simulation Engine

A  highly decoupled social simulation engine built natively in **Unity 6** using the Universal Render Pipeline (URP).
The core architectural goal of this project is to create an advanced town ecosystem where non-player characters (NPCs)
autonomously witness events, alter dynamic reputation metrics, store mutating rumor records in private memory banks,
and physically interact using localized AI Small Language Models (SLMs) to exchange information.

By avoiding rigid, performance-heavy Unity execution models and traditional global singletons,
this project is being built to prioritize clean scaling. By avoiding rigid Unity singletons,
it leverages a high-performance C# framework that allows for seamless system expansion.

---

## 🛠️ Technical Milestones Achieved So Far

* **Established Clean Project Framework** — Organized an isolated directory architecture (`Assets/_Project/`) to cleanly separate foundational game code from external plugins or assets.
* **Integrated Dependency Injection Container** — Installed and integrated **VContainer** to act as the core object resolver and dependency manager.
* **Implemented Single Entry Point (Composition Root)** — Created `GameLifetimeScope` and a non-MonoBehaviour `GameBootstrapper` to manage frame-zero system setup, guaranteeing initialization safety before standard gameplay updates execute.
* **Coded Abstract Architectural Contracts** — Created the decoupled `IGossipEngine` interface and concrete `CoreGossipEngine` handler class to isolate simulation logic from scene constraints.
* **Built Data-Driven Animation Architecture** — Swapped out rigid animator state-machine transitions for an asset-driven pipeline using custom `GossipToneData` ScriptableObjects.
* **Implemented Procedural Motion Blending** — Programmed `NPCAnimationBridge` using Unity’s `CrossFadeInFixedTime` API to smoothly blend character models into dynamic poses by name, completely eliminating transition graph bottlenecks.
* **Created Variance Pool Selection Logic** — Enhanced the animation data engine to support string arrays, allowing the system to pick a random gesture variant from a pool of animation clips to avoid repetitive, robotic NPC movements.
* **Engineered NPC Memory Profile System** — Constructed an independent, memory-efficient data tracking layout using `RumorTemplate` ScriptableObjects and lightweight `RuntimeRumorState` memory tracking objects.
* **Programmed Private NPC Brain Storage** — Developed the `NpcGossipMemory` system component utilizing a C# `Dictionary` to index known rumors by unique string keys, enabling fast lookups, memory tracking, and belief/credibility adjustments.
* **Constructed Scene Injection Scanner** — Wired VContainer’s `RegisterComponentInHierarchy<T>` functionality into the Composition Root to automatically discover scene-bound NPCs and deliver core engines on boot.
* **Executed Successful Integration Test** — Built a standalone `GossipTester` simulation driver that successfully validated the sequential initialization, runtime rumor learning, and real-time animation blending loops without compilation warnings or errors.

---

## 🏗️ Core Architecture Overview

The system architecture is strictly split into **Immutable Templates** (static assets on disk) and **Runtime States** (dynamic memory instances running on the client machine). 

```text
[VContainer DI Container]
           │
           ├──► Injects IGossipEngine ──► [GameBootstrapper]
           │
           └──► Injects Scene Context ──► [NpcGossipMemory] (NPC Brain)
                                                    │
                                                    ▼
                                     Reads: [RumorTemplate] (Asset)
                                     Tracks: [RuntimeRumorState] (Memory)
                                     Visualizes: [NPCAnimationBridge] (Crossfade)

```

1. **Dependency Injection:** System components do not look for each other via `GameObject.Find` or rigid Singleton patterns. Dependencies are requested via `[Inject]` tags and resolved cleanly on frame-zero by **VContainer**.
2. **Data-Driven Poses:** The Unity Animator window contains isolated states without any transition arrow lines. The C# code instructs the controller exactly which state to crossfade into by matching string names inside the `GossipToneData` asset.
3. **Memory Isolation:** NPCs do not alter global data. Every character stores an isolated, internal `Dictionary` tracking their personal version, credibility rating, and timestamps for any rumors they have acquired.

---

## 🚀 Next Steps: Localized AI Integration

The next major development phase introduces **Localized Large Language Models (LLMs)** running completely offline on the player's machine (via tools like Unity Sentis, LLamaSharp, or Ollama).

* **Zero API Costs:** By avoiding cloud dependencies (OpenAI/Gemini), thousands of conversational evaluations can process simultaneously on the player's hardware with zero server utility costs.
* **Contextual Prompt Generation:** The C# framework will automatically inject the NPC's profile id, emotional stance, target reputation scores, and current rumor facts directly into a local small language model (such as *Llama 3 8B* or *Phi-3*) to stream unique, contextual dialogue choices in real-time.

---

## 💻 Tech Stack

* **Engine:** Unity 6 (6000.4.10f1)
* **Render Pipeline:** Universal Render Pipeline (URP)
* **Language:** C# (.NET 8 compatible)
* **DI Framework:** VContainer

```

```
