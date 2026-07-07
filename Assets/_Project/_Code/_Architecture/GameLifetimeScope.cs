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
        builder.Register<IGossipEngine, CoreGossipEngine>(Lifetime.Singleton);

        // We Register a custom bootstrapper class to control frame-zero execution.
        builder.RegisterEntryPoint<GameBootstrapper>();

        // Dynamic Scene Discovery Scanner — auto-wires every relevant MonoBehaviour found
        // in the scene hierarchy, so no manual drag-and-drop references are needed.
        builder.RegisterComponentInHierarchy<NPCGossipMemory>();
        builder.RegisterComponentInHierarchy<NPCProximityGossip>();
        builder.RegisterComponentInHierarchy<NPCAnimationBridge>();
        builder.RegisterComponentInHierarchy<NPCReputationOpinion>();

        // v4: Added — the witness step (on the Player) and the tick-based broadcast driver
        // (a single scene-wide GameObject) both need dependencies injected.
        builder.RegisterComponentInHierarchy<PlayerDeedBroadcaster>();
        builder.RegisterComponentInHierarchy<GossipTickDriver>();

        // Testers
        builder.RegisterComponentInHierarchy<GossipTester>();
        builder.RegisterComponentInHierarchy<ReputationTester>();
        builder.RegisterComponentInHierarchy<DeedTester>();

        // v5: Added — ReputationBarUI is an OPTIONAL visualization tool. This registration
        // simply has no effect if no ReputationBarUI exists in the scene; adding one is
        // entirely up to you.
        builder.RegisterComponentInHierarchy<Project.UI.ReputationBarUI>();

        // GossipManager and ReputationService are registered ONCE, here, at the game level.
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
        private readonly GossipTester _tester;

        public GameBootstrapper(IGossipEngine gossipEngine, GossipTester tester)
        {
            _gossipEngine = gossipEngine;
            _tester = tester;
        }

        public void Start()
        {
            _gossipEngine.Initialize();
            Debug.Log("<color=yellow>[Bootstrapper]</color> Entry point achieved. Waking engines...");
            Debug.Log("<color=magenta>[Game Bootstrapper]</color> Game initialization complete. All systems are online.");
        }
    }

}