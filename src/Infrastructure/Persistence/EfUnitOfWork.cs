using KartOfferService.Application.Common.Exceptions;
using KartOfferService.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace KartOfferService.Infrastructure.Persistence;

public sealed class EfUnitOfWork : IUnitOfWork
{
    private const string PostgresUniqueViolationSqlState = "23505";

    private readonly OfferDbContext _dbContext;
    private IDbContextTransaction? _transaction;

    public EfUnitOfWork(OfferDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresUniqueViolationSqlState } pgEx)
        {
            // IssueCouponCommandHandler's own existence pre-check is a TOCTOU race, not a
            // guarantee - this is the database-enforced backstop, translated to a stable
            // Application-layer exception type so Kart.Shared.ErrorHandling can map it to 409
            // without Application ever referencing Npgsql.
            throw new DuplicateKeyException($"A row with the same natural key already exists ({pgEx.ConstraintName}).");
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException("The row was modified by another request since it was last read.");
        }
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken)
    {
        _transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken)
    {
        if (_transaction is null)
        {
            return;
        }

        await _transaction.CommitAsync(cancellationToken);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken)
    {
        if (_transaction is null)
        {
            return;
        }

        await _transaction.RollbackAsync(cancellationToken);
        await _transaction.DisposeAsync();
        _transaction = null;
    }
}
