namespace Eleven.Keeper
{
    /// <summary>
    /// Provides a <see cref="KeeperCues"/> snapshot at a given time-to-contact.
    /// Implementations must be deterministic for the same animation / transform state
    /// and must not allocate GC memory inside <see cref="Sample"/>.
    /// </summary>
    public interface ICueSource
    {
        KeeperCues Sample(float timeToContact);
    }
}
