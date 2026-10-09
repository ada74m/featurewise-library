using System.Security.Claims;
using Featurewise;

namespace SampleService.Tests;

// Callers for invoking features directly: Mia the manager, Ed her report, and a scheduled job.
public static class Callers
{
    public static ClaimsPrincipal Manager { get; } = Person("m1", "manager");

    public static ClaimsPrincipal Employee { get; } = Person("e1");

    public static ClaimsPrincipal Job { get; } = SystemCaller.For("test-job");

    private static ClaimsPrincipal Person(string name, params string[] roles) =>
        new(new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, name), .. roles.Select(role => new Claim(ClaimTypes.Role, role))],
            authenticationType: "test"));
}
