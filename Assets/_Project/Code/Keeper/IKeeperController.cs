namespace Eleven.Keeper
{
    public interface IKeeperController
    {
        KeeperPhase Phase { get; }
        bool TryCommit(in KeeperRead read, float timeToContact, KeeperProfile p, out DiveDecision decision);
    }
}
