using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using PlayerAnalytics.Database.Shared;
using SqlSugar;

namespace PlayerAnalytics.Database.Provider;

internal sealed class DatabaseQueryable<T>(ISugarQueryable<T> inner) : IDatabaseQueryable<T>
    where T : class, new()
{
    private ISugarQueryable<T> _q = inner;

    public IDatabaseQueryable<T> Where(Expression<Func<T, bool>> predicate)
    {
        _q = _q.Where(predicate);
        return this;
    }

    public IDatabaseQueryable<T> OrderBy(Expression<Func<T, object>> keySelector)
    {
        _q = _q.OrderBy(keySelector);
        return this;
    }

    public IDatabaseQueryable<T> OrderByDescending(Expression<Func<T, object>> keySelector)
    {
        _q = _q.OrderBy(keySelector, OrderByType.Desc);
        return this;
    }

    public IDatabaseQueryable<T> Take(int count)
    {
        _q = _q.Take(count);
        return this;
    }

    public Task<T>       FirstAsync()              => _q.FirstAsync()!;
    public Task<T?>      FirstOrDefaultAsync()     => _q.FirstAsync()!;
    public Task<List<T>> ToListAsync()             => _q.ToListAsync();
    public Task<int>     CountAsync()              => _q.CountAsync();
}
