using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace PlayerAnalytics.Database.Shared;

/// <summary>
/// Fluent query builder for single-table queries.
/// </summary>
public interface IDatabaseQueryable<T>
{
    IDatabaseQueryable<T> Where(Expression<Func<T, bool>> predicate);
    IDatabaseQueryable<T> OrderBy(Expression<Func<T, object>> keySelector);
    IDatabaseQueryable<T> OrderByDescending(Expression<Func<T, object>> keySelector);
    IDatabaseQueryable<T> Take(int count);

    Task<T>        FirstAsync();
    Task<T?>       FirstOrDefaultAsync();
    Task<List<T>>  ToListAsync();
    Task<int>      CountAsync();
}
