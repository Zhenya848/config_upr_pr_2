using DependencyGraphApp.Models;

namespace DependencyGraphApp.Repositories;

public interface IPackageRepository
{
    Task<IReadOnlyList<PackageRecord>> LoadAsync(
        CancellationToken cancellationToken = default);
}
