using Featurewise;

namespace SampleService;

// An employee's details, with a link to fire them if the caller may.
public class EmployeeDetails(IEmployeeStore employees, LinkPermissions permissions)
    : Feature<EmployeeDetails, EmployeeDetails.Args, EmployeeDetails.Result>
{
    public record Args(string EmployeeId) : Link<EmployeeDetails, Result>;

    public abstract record Result
    {
        private Result()
        {
        }

        public record Found(Employee Employee, Fire.Args? LinkToFire) : Result;

        public record NotFound(string EmployeeId) : Result;
    }

    public override async Task<Result> Invoke(Args args, CancellationToken cancellationToken)
    {
        var employee = await employees.Find(args.EmployeeId, cancellationToken);
        if (employee is null)
            return new Result.NotFound(args.EmployeeId);

        var fire = new Fire.Args(employee.Id);
        var offerFire = employee.Status == EmploymentStatus.Active && await permissions.MayInvoke(fire);
        return new Result.Found(employee, offerFire ? fire : null);
    }
}
