using System.Diagnostics;
using Npgsql;

namespace Govern.IntegrationTests.Support;

/// <summary>
/// Forces concurrent writes to one game to overlap. Holding the <c>games</c> row lock does not block plain reads, so every
/// request loads the game in the same state and then queues on its write; the lock is released only when all of them wait.
/// That makes the lost-race branches (concurrency token, unique violation) run deterministically.
/// </summary>
public static class GameRowLock
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(15);

    public static async Task<T> RaceAsync<T>(string connectionString, Guid gameId, int writers, Func<Task<T>> race, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await using (var lockRow = new NpgsqlCommand("SELECT 1 FROM games WHERE id = @id FOR UPDATE", connection, transaction))
        {
            lockRow.Parameters.AddWithValue("id", gameId);
            await lockRow.ExecuteScalarAsync(ct);
        }

        var running = race();
        await WaitForLockWaitersAsync(connectionString, writers, running, ct);
        await transaction.CommitAsync(ct);
        return await running;
    }

    /// <summary>
    /// Like <see cref="RaceAsync{T}"/>, but <paramref name="first"/> queues on the lock before <paramref name="second"/>
    /// starts, so Postgres grants the row to <paramref name="first"/> first and the race has a fixed winner.
    /// </summary>
    public static async Task<(HttpResponseMessage First, HttpResponseMessage Second)> RaceInOrderAsync(
        string connectionString, Guid gameId, Func<Task<HttpResponseMessage>> first, Func<Task<HttpResponseMessage>> second, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await using (var lockRow = new NpgsqlCommand("SELECT 1 FROM games WHERE id = @id FOR UPDATE", connection, transaction))
        {
            lockRow.Parameters.AddWithValue("id", gameId);
            await lockRow.ExecuteScalarAsync(ct);
        }

        var winner = first();
        await WaitForLockWaitersAsync(connectionString, 1, winner, ct);
        var loser = second();
        await WaitForLockWaitersAsync(connectionString, 2, loser, ct);
        await transaction.CommitAsync(ct);
        return (await winner, await loser);
    }

    // Polls until the given number of sessions wait on a lock (every writer read the game and is blocked on its write).
    private static async Task WaitForLockWaitersAsync(string connectionString, int writers, Task running, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var count = new NpgsqlCommand(
            "SELECT count(*) FROM pg_stat_activity WHERE datname = current_database() AND wait_event_type = 'Lock'", connection);

        var clock = Stopwatch.StartNew();
        while ((long)(await count.ExecuteScalarAsync(ct))! < writers)
        {
            if (running.IsCompleted || clock.Elapsed > WaitTimeout)
            {
                throw new TimeoutException($"Expected {writers} writers to block on the game row, but they did not.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(20), ct);
        }
    }
}
