using VContainer;
using VContainer.Unity;
using UnityEngine;
using Project.Architecture;
using Project.GamePlay;
using Project.Services;
using Project.Testing;


/// <summary>
/// This class acts as the main gateway for VContainer. 
/// It configures our object injections before any gameplay logic awakens.
/// </summary>
public class GameLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        // Core Framework Systems
        // We Register our CoreGossipEngine as a Singleton.
        // This means VContainer creates exactly ONE instance of it in memory,
        // and shares that exact same instance with any script that requests an IGossipEngine.

        builder.Register<IGossipEngine, CoreGossipEngine>(Lifetime.Singleton);

        // We Register a custom bootstrapper class to control frame-zero execution.
        builder.RegisterEntryPoint<GameBootstrapper>();

        // v1: AI generation service and TTS voice service registrations removed.
        // AI/LLM and TTS are no longer part of this project's pipeline.

        // Beacause our NPCs live directly in the scene hierarchy as GameObjects, we need to instruct our Composition Root
        // ( GameLifetimeScope ) to automatically scan the scene when it wakes up, find these NPCGossipMemory brains ( components ), 
        // and feed them the central engine dependency AKA "auto-wiring" and it saves us from having to manually drag-and-drop references in the inspector.

        // Dynamic Scene Discovery Scanner
        // We tell VContainer to scan the active scene layout and automatically bind
        // all NPCGossipMemory scripts to our central injection framework.   
        builder.RegisterComponentInHierarchy<NPCGossipMemory>();

        // Tells VContainer to find and inject dependencies straight into our trigger script!
        builder.RegisterComponentInHierarchy<NPCProximityGossip>();

        // v2: Added — this was previously only wired via the now-deleted NPCLifetimeScope,
        // meaning NPCAnimationBridge was never actually getting its ReputationService injected.
        builder.RegisterComponentInHierarchy<NPCAnimationBridge>();

        // We register the tester to then inject directly
        builder.RegisterComponentInHierarchy<GossipTester>();

        // v2: GossipManager and ReputationService are now registered ONCE, here, at the game
        // level. NPCLifetimeScope (which duplicated these registrations as a separate scope)
        // has been removed. Every script in the project that requests these via [Inject] now
        // reliably receives the same shared instance.
        builder.Register<GossipManager>(Lifetime.Singleton);
        builder.Register<ReputationService>(Lifetime.Singleton);

    }


    /// <summary>
    /// Our non-MonoBehaviour startup class. 
    /// By inheriting from VContainer's 'IStartable', it gains a safe entry point.
    /// </summary>
    public class GameBootstrapper : IStartable
    {
        private readonly IGossipEngine _gossipEngine;
        private readonly GossipTester _tester; // Store the reference

        // VContainer injects BOTH the engine AND the tester here automatically
        public GameBootstrapper(IGossipEngine gossipEngine, GossipTester tester)
        {
            _gossipEngine = gossipEngine;
            _tester = tester;
        }


        /// <summary>
        /// This is the absolute first frame of the game.
        /// VContainer triggers this automatically as soon as the scene wakes up.
        /// </summary>
        //  This method is called by VContainer immediately after all dependencies are injected.
        public void Start()
        {
            // Now we can safely initialize our core systems without worrying about scene loading order.
            _gossipEngine.Initialize();
            Debug.Log("<color=yellow>[Bootstrapper]</color> Entry point achieved. Waking engines...");
            Debug.Log("<color=magenta>[Game Bootstrapper]</color> Game initialization complete. All systems are online.");


            /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            /// FOR TESTING PORPUSES ONLY: This is a temporary development block to simulate a rumor injection into our NPCs memory banks.///
            /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

            //// No searching required!
            //if (_tester != null)
            //{
            //    _tester.ExecuteTestInjection();
            //}
        }
    }

}