using PlantOps.Domain.Common;

namespace PlantOps.Application.Auth;

public static class AuthErrors
{
    // One message for unknown email, wrong password and locked account, so responses don't reveal which accounts exist.
    // AccountService also spends equal hashing time on unknown emails, so response timing doesn't reveal it either.
    public static readonly Error InvalidCredentials = Error.Unauthorized(
        "Auth.InvalidCredentials", "Invalid email or password.");
}
