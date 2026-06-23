using Project.Data;

namespace Project.Architecture
{
    /// <summary>
    /// Updated professional contract. We now pass pure data (GossipToneData) 
    /// instead of a rigid, hardcoded enum or integer.
    /// </summary>
    /// 

    public interface INpcAnimationController
    {
        // Make sure the method name is spelled exactly 'TransitionToTone' 
        // and takes exactly a 'GossipToneData' object parameter.
        void TransitionToTone(GossipToneData toneData);
    }
}
