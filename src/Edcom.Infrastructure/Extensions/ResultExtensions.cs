using System.Linq.Expressions;

namespace Edcom.Infrastructure.Extensions;

public static class QueryableExtensions
{
    public static IQueryable<T> ApplyPaging<T>(this IQueryable<T> query, int page, int pageSize)
        => query.Skip((page - 1) * pageSize).Take(pageSize);

    public static IQueryable<T> ApplyOrdering<T, TKey>(
        this IQueryable<T> query,
        Expression<Func<T, TKey>> keySelector,
        bool descending = false)
        => descending ? query.OrderByDescending(keySelector) : query.OrderBy(keySelector);
}
