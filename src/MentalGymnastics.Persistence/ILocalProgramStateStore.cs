namespace MentalGymnastics.Persistence;

public interface ILocalProgramStateStore
{
    ValueTask<LocalProgramStateSnapshot?> LoadAsync(CancellationToken cancellationToken = default);

    ValueTask SaveAsync(LocalProgramStateSnapshot snapshot, CancellationToken cancellationToken = default);
}
