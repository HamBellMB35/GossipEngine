using VContainer;
using VContainer.Unity;
using UnityEngine;
using TownsPeople.Architecture;
using TownsPeople.GamePlay;
using TownsPeople.Services;
using TownsPeople.Testing;



/// <summary>
/// This class acts as the main gateway for VContainer. 
/// It configures our object injections before any gameplay logic awakens.
/// </summary>
// v8: CORRECTED FIX. The previous attempt (v7) tried registering every instance of each
// per-NPC type individually via builder.RegisterComponent(instance) in a loop � this throws
// "Conflict implementation type" the moment a SECOND instance of the same concrete type is
// registered, since VContainer's registry doesn't support multiple non-keyed registrations of
// an identical contract type that way.
//
// The correct, documented approach for "many scene instances of a type, each needs [Inject]
// run, but nothing ever needs to Resolve<T>() them by type" is to NOT register them in the
// container at all, and instead manually call IObjectResolver.Inject(instance) on each one
// after the container finishes building. See GameBootstrapper below.
public class GameLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        // Core Framework Systems
        builder.Register<IGossipEngine, CoreGossipEngine>(Lifetime.Singleton);
        builder.RegisterEntryPoint<GameBootstrapper>();

        // GossipManager and ReputationService are registered ONCE, here, at the game level.
        builder.Register<GossipManager>(Lifetime.Singleton);
        builder.Register<ReputationService>(Lifetime.Singleton);
        // v9: PlayerCombatState — the integration point for the Locomotion add-on's
        // Flocking/Fleeing triggers (FlockTriggerCondition/FlockReturnCondition). Registered as
        // a plain C# service, same as the two above — this project's own player weapon/combat
        // scripts should call SetWeaponDrawn()/NotifyCombatEngagement() into whichever instance
        // gets injected wherever they need it.
        builder.Register<PlayerCombatState>(Lifetime.Singleton);

        // v8: Per-NPC and scene-level components are intentionally NOT registered here.
        // GameBootstrapper manually injects every instance of each type below, after Build().
    }

    /// <summary>
    /// Our non-MonoBehaviour startup class. 
    /// By inheriting from VContainer's 'IStartable', it gains a safe entry point.
    /// </summary>
    public class GameBootstrapper : IStartable
    {
        private readonly IGossipEngine _gossipEngine;
        private readonly IObjectResolver _resolver;

        public GameBootstrapper(IGossipEngine gossipEngine, IObjectResolver resolver)
        {
            _gossipEngine = gossipEngine;
            _resolver = resolver;
        }

        public void Start()
        {
            _gossipEngine.Initialize();

            // v8: Manually injects EVERY instance of each type currently in the scene. This
            // is the documented VContainer pattern (container.Inject(instance)) for types with
            // many per-scene instances that don't need to be resolvable by type elsewhere.
            InjectAllInstancesOf<NPCGossipMemory>();
            InjectAllInstancesOf<NPCProximityGossip>();
            InjectAllInstancesOf<NPCAnimationBridge>();
            InjectAllInstancesOf<NPCReputationOpinion>();
            InjectAllInstancesOf<NPCGreetingResponder>();
            InjectAllInstancesOf<PlayerDeedBroadcaster>();
            InjectAllInstancesOf<GossipTickDriver>();
            InjectAllInstancesOf<GossipTester>();
            InjectAllInstancesOf<ReputationTester>();
            InjectAllInstancesOf<DeedTester>();
            InjectAllInstancesOf<TownsPeople.UI.ReputationBarUI>();
            // v10: PlayerCombatStateTester — Core test harness (same category as GossipTester/
            // ReputationTester/DeedTester above), direct reference is fine.
            InjectAllInstancesOf<PlayerCombatStateTester>();

            // v9: NPCFlockingBehavior lives in the separately-sold Locomotion add-on — cannot
            // be referenced by concrete type here without breaking compile safety for a buyer
            // who owns Core but not Locomotion (same reasoning already applied to
            // NPCAnimationBridge/NPCControlPanelWindow's optional-type checks). Reflection-safe
            // injection instead: no-ops entirely if the add-on isn't installed.
            InjectAllInstancesOfReflected("TownsPeople.GamePlay.NPCFlockingBehavior");

            Debug.Log("<color=yellow>[Bootstrapper]</color> Entry point achieved. Waking engines...");
            Debug.Log("<color=magenta>[Game Bootstrapper]</color> Game initialization complete. All systems are online.");
        }

        private void InjectAllInstancesOf<T>() where T : Component
        {
            T[] instances = Object.FindObjectsByType<T>(FindObjectsInactive.Include);
            foreach (T instance in instances)
            {
                _resolver.Inject(instance);
            }
        }

        /// <summary>
        /// v9: Reflection-based counterpart to InjectAllInstancesOf&lt;T&gt;() for optional
        /// add-on types Core cannot reference directly. No-ops entirely (Type.GetType returns
        /// null) if the named add-on isn't installed in this project.
        /// </summary>
        private void InjectAllInstancesOfReflected(string fullyQualifiedTypeName)
        {
            System.Type type = System.Type.GetType(fullyQualifiedTypeName);
            if (type == null) return;

            // v10: Uses the overload WITHOUT FindObjectsSortMode — that parameter is deprecated;
            // this reflection-based injection helper doesn't care about ordering anyway.
            Object[] instances = Object.FindObjectsByType(type, FindObjectsInactive.Include);
            foreach (Object instance in instances)
            {
                _resolver.Inject(instance);
            }
        }
    }

}