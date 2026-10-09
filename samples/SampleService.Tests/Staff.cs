namespace SampleService.Tests;

// The employees tests start with: a manager, two reports with reviews at different distances, and two
// leavers, one long gone.
public static class Staff
{
    public static readonly Employee Mia = new("m1", "Mia", null, EmploymentStatus.Active, Fakes.Today.AddMonths(6));

    public static readonly Employee Ed = new("e1", "Ed", "m1", EmploymentStatus.Active, Fakes.Today.AddDays(5));

    public static readonly Employee Eve = new("e2", "Eve", "m1", EmploymentStatus.Active, Fakes.Today.AddDays(60));

    public static readonly Employee Leo = new("e3", "Leo", "m1", EmploymentStatus.Left, Fakes.Today.AddDays(-100), Fakes.Today.AddDays(-100));

    public static readonly Employee Lou = new("e4", "Lou", "m1", EmploymentStatus.Left, Fakes.Today.AddDays(-10), Fakes.Today.AddDays(-10));

    public static IEnumerable<Employee> All => [Mia, Ed, Eve, Leo, Lou];
}
