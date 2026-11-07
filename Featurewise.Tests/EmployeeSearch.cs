namespace Featurewise.Tests;

// Has required, nullable and defaulted arguments, so tests can see which a link's JSON must supply.
public class EmployeeSearch : Query<EmployeeSearch.Args, string>
{
    public record Args(string Query, int Limit, string? Department, int? Grade, int Page = 1)
        : Link<EmployeeSearch, string>;

    public override Task<string> Invoke(Args args, CancellationToken cancellationToken) =>
        Task.FromResult($"{args.Query} {args.Limit} {args.Department ?? "-"} {args.Grade?.ToString() ?? "-"} {args.Page}");
}
