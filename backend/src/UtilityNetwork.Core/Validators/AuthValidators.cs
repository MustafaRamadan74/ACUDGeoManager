using System.Text.RegularExpressions;
using FluentValidation;
using UtilityNetwork.Core.Models;

namespace UtilityNetwork.Core.Validators;

public sealed partial class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    [GeneratedRegex("^[A-Za-z0-9_.-]{3,32}$")]
    private static partial Regex UsernameRegex();

    public LoginRequestValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Username is required.")
            .Matches(UsernameRegex()).WithMessage("Username must be between 3 and 32 characters and contain only letters, numbers, underscores, dots, or dashes.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(10).WithMessage("Password must be at least 10 characters long.");
    }
}

public sealed partial class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    private static readonly string[] AllowedRoles = ["VIEWER", "EDITOR", "ADMIN"];
    private static readonly HashSet<string> CommonPasswords = new(StringComparer.OrdinalIgnoreCase)
    {
        "password1234", "admin123456", "1234567890", "password123", "letmein1234", "welcome1234"
    };

    [GeneratedRegex("^[A-Za-z0-9_.-]{3,32}$")]
    private static partial Regex UsernameRegex();

    [GeneratedRegex(@"^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$")]
    private static partial Regex EmailRegex();

    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Username is required.")
            .Matches(UsernameRegex()).WithMessage("Username must be between 3 and 32 characters and contain only letters, numbers, underscores, dots, or dashes.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .MaximumLength(255).WithMessage("Email must not exceed 255 characters.")
            .Matches(EmailRegex()).WithMessage("A valid email address is required.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(10).WithMessage("Password must be at least 10 characters long.")
            .Must(p => !CommonPasswords.Contains(p)).WithMessage("Password is too common and easily guessed.");

        RuleFor(x => x.Role)
            .NotEmpty().WithMessage("Role is required.")
            .Must(r => AllowedRoles.Contains(r.ToUpperInvariant()))
            .WithMessage("Role must be one of: VIEWER, EDITOR, ADMIN.");
    }
}

public sealed partial class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    private static readonly string[] AllowedRoles = ["VIEWER", "EDITOR", "ADMIN"];

    [GeneratedRegex(@"^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$")]
    private static partial Regex EmailRegex();

    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.Email)
            .Matches(EmailRegex()).When(x => !string.IsNullOrWhiteSpace(x.Email))
            .WithMessage("A valid email address is required.");

        RuleFor(x => x.Role)
            .Must(r => r is null || AllowedRoles.Contains(r.ToUpperInvariant()))
            .WithMessage("Role must be one of: VIEWER, EDITOR, ADMIN.");
    }
}
