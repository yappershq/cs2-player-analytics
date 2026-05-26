using System;
using System.Threading.Tasks;

namespace PlayerAnalytics.Database.Shared;

/// <summary>
/// Wraps a database transaction. Dispose without commit to rollback.
/// </summary>
public interface IDatabaseTransaction : IAsyncDisposable
{
    Task CommitAsync();
    Task RollbackAsync();
}
