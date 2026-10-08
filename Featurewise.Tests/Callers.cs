using System.Security.Claims;

namespace Featurewise.Tests;

// Callers for tests: an anonymous one, and authenticated ones with and without the manager role.
public static class Callers
{
    public static ClaimsPrincipal Anonymous { get; } = new(new ClaimsIdentity());

    public static ClaimsPrincipal Employee { get; } = Authenticated("employee");

    public static ClaimsPrincipal Manager { get; } = Authenticated("manager", "manager");

    private static ClaimsPrincipal Authenticated(string name, params string[] roles) =>
        new(new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, name), .. roles.Select(role => new Claim(ClaimTypes.Role, role))],
            authenticationType: "test"));
}
