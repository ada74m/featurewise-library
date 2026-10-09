using Featurewise;

namespace SampleService;

// Removes the records of people who left more than the given number of days ago.
public class ArchiveLeavers(IEmployeeStore employees, TimeProvider time)
    : Feature<ArchiveLeavers, ArchiveLeavers.Args, ArchiveLeavers.Result>
{
    public record Args(int AfterDays = 90) : Link<ArchiveLeavers, Result>;

    public record Result(int Archived);

    public override async Task<Result> Invoke(Args args, CancellationToken cancellationToken)
    {
        var cutoff = time.Today().AddDays(-args.AfterDays);
        var leavers = (await employees.All(cancellationToken))
            .Where(e => e.Status == EmploymentStatus.Left && e.LeftOn <= cutoff)
            .ToList();

        foreach (var leaver in leavers)
            await employees.Delete(leaver.Id, cancellationToken);

        return new Result(leavers.Count);
    }
}
