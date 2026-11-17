using Fusion;

public sealed class DoctorReactionState : NetworkBehaviour
{
    [Networked, OnChangedRender(nameof(ApplyReaction))]
    public NetworkBool ReactionActive { get; private set; }

    public void Activate()
    {
        if (Object.HasStateAuthority && !ReactionActive)
            ReactionActive = true;
    }

    public override void Spawned() => ApplyReaction();

    private void ApplyReaction()
    {
        if (ReactionActive && Runner.TryGetComponent(out FusionBootstrap scenario))
            scenario.ApplyReaction();
    }
}
