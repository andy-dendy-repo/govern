namespace Govern.Client.Features.Users.Models;

/// <summary>
/// The request body for <c>POST /api/users/sign-up</c>.
/// </summary>
/// <param name="Email">The email address; unique per account.</param>
/// <param name="Password">The password, <see cref="RegisterForm.PasswordMinLength"/>–<see cref="RegisterForm.PasswordMaxLength"/> characters.</param>
public sealed record SignUpRequest(string Email, string Password);
