using VContainer;
using VContainer.Unity;
using UnityEngine;
using Project.Architecture;


/// <summary>
/// This class acts as the main gateway for VContainer. 
/// It configures our object injections before any gameplay logic awakens.
/// </summary>
public class GameLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        // Rule 1: Register our CoreGossipEngine as a Singleton.
        // This means VContainer creates exactly ONE instance of it in memory,
        // and shares that exact same instance with any script that requests an IGossipEngine.

        builder.Register<IGossipEngine, CoreGossipEngine>(Lifetime.Singleton);

        // Rule 2: Register a custom bootstrapper class to control frame-zero execution.
        builder.RegisterEntryPoint<GameBootstrapper>();
    }

    /// <summary>
    /// Our non-MonoBehaviour startup class. 
    /// By inheriting from VContainer's 'IStartable', it gains a safe entry point.
    /// </summary>
    
    public class GameBootstrapper : IStartable
    {
        private readonly IGossipEngine _gossipEngine;
        // VContainer automatically injects the registered IGossipEngine instance here.
        public GameBootstrapper(IGossipEngine gossipEngine)
        {
            _gossipEngine = gossipEngine;
        }


        /// <summary>
        /// This is the absolute first frame of the game.
        /// VContainer triggers this automatically as soon as the scene wakes up.
        /// </summary>
        /// This method is called by VContainer immediately after all dependencies are injected.
        public void Start()
        {
            // Now we can safely initialize our core systems without worrying about scene loading order.
            _gossipEngine.Initialize();
            Debug.Log("<color=magenta>[Game Bootstrapper]</color> Game initialization complete. All systems are online.");
        }
    }

}
