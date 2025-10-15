using System.Collections.Generic;
using System.Linq;
using EazyFind.Domain.Entities;
using EazyFind.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EazyFind.Infrastructure.Data.Repositories;

internal class ProductAlertMatchRepository(EazyFindDbContext dbContext) : IProductAlertMatchRepository
{
    public async Task BulkInsertAsync(IEnumerable<ProductAlertMatch> matches, CancellationToken cancellationToken)
    {
        if (matches is null)
        {
            return;
        }

        var items = matches.ToList();
        if (items.Count == 0)
        {
            return;
        }

        try
        {
            await dbContext.ProductAlertMatches.AddRangeAsync(items, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
        }
        catch (DbUpdateException ex) when (IsDuplicateKey(ex))
        {
            dbContext.ChangeTracker.Clear();
        }
    }

    private static bool IsDuplicateKey(DbUpdateException ex)
    {
        if (ex.InnerException is PostgresException postgresException)
        {
            return postgresException.SqlState == PostgresErrorCodes.UniqueViolation;
        }

        return false;
    }
}
