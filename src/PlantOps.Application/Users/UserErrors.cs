using PlantOps.Domain.Common;

namespace PlantOps.Application.Users;

public static class UserErrors
{
    public static Error NotATechnician(Guid userId) => Error.Validation(
        "User.NotATechnician", $"User '{userId}' is not a technician.");

    public static readonly Error EmailInUse = Error.Conflict(
        "User.EmailInUse", "A user with this email already exists.");
}
