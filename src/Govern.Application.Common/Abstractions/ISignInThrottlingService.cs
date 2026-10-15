using Govern.Domain.ValueObjects;

namespace Govern.Application.Common.Abstractions;

/// <summary>
/// Limits sign-in attempts per account (normalised email) so one account can't be brute-forced from many IP addresses.
/// Unknown emails are counted the same way, so throttling doesn't reveal whether an account exists.
/// </summary>
public interface ISignInThrottlingService
{
    /// <summary>
    /// Atomically reserves a sign-in attempt for <paramref name="email"/> <b>before</b> the password is checked, so
    /// parallel requests can't all pass a check that is only updated after the slow verification. The first attempt
    /// starts a new window; an attempt stays counted unless <see cref="ResetAsync"/> is called.
    /// </summary>
    /// <param name="email">The normalised email being signed in.</param>
    /// <param name="cancellationToken">Cancels the reservation.</param>
    /// <returns><see langword="false"/> when the limit is reached and sign-in must be refused without checking the password.</returns>
    ValueTask<bool> TryBeginAttemptAsync(Email email, CancellationToken cancellationToken);

    /// <summary>
    /// Forgets the attempts of <paramref name="email"/> after a successful sign-in.
    /// </summary>
    /// <param name="email">The normalised email that signed in.</param>
    /// <param name="cancellationToken">Cancels the reset.</param>
    /// <returns>A task that completes once the attempts are forgotten.</returns>
    ValueTask ResetAsync(Email email, CancellationToken cancellationToken);
}
