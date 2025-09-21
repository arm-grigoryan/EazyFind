using EazyFind.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using System.Linq.Expressions;

namespace EazyFind.Infrastructure.Extensions;

public static class QueryableExtensions
{
    public static IQueryable<T> ApplyPagination<T>(this IQueryable<T> query, PaginationFilter pagination)
    {
        if (pagination == null)
        {
            return query;
        }

        return query.Skip(pagination.Skip).Take(pagination.Take);
    }

    public static async Task<PaginatedResult<T>> PageAsync<T>(
        this IQueryable<T> query,
        PaginationFilter paginationFilter,
        CancellationToken cancellationToken)
    {
        return new PaginatedResult<T>
        {
            TotalCount = await query.CountAsync(cancellationToken),
            Items = await query.ApplyPagination(paginationFilter).ToListAsync(cancellationToken),
        };
    }

    public static IQueryable<T> WhereIf<T>(
        this IQueryable<T> query,
        bool condition,
        Expression<Func<T, bool>> predicate) => condition ? query.Where(predicate) : query;

    public static IIncludableQueryable<TEntity, TProperty> IncludeIf<TEntity, TProperty>(
        this IQueryable<TEntity> query,
        bool condition,
        Expression<Func<TEntity, TProperty>> navigationPropertyPath) where TEntity : class
            => (condition ? query.Include(navigationPropertyPath) : query) as IIncludableQueryable<TEntity, TProperty>;

    public static IOrderedQueryable<TEntity> OrderByIf<TEntity, TProperty>(
        this IQueryable<TEntity> query,
        Expression<Func<TEntity, TProperty>> keySelector,
        bool? isDesc = false)
    {
        if (isDesc is null)
        {
            // Keep query as is, but ensure it’s treated as ordered
            return query.OrderBy(e => 0); // no-op order to keep chainable
        }

        return isDesc.Value
            ? query.OrderByDescending(keySelector)
            : query.OrderBy(keySelector);
    }

    public static IQueryable<T> If<T>(
        this IQueryable<T> query,
        bool condition,
        Func<IQueryable<T>, IQueryable<T>> then)
            => condition ? then(query) : query;
}
