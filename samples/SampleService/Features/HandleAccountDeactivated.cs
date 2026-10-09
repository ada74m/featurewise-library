using Featurewise;

namespace SampleService;

// Handles the directory service's news that someone's account was closed: the directory is the record
// of who has left, so an employee still active here has left too.
public class HandleAccountDeactivated(IEmployeeStore employees, LeaverRecorder leaverRecorder)
    : Feature<HandleAccountDeactivated, HandleAccountDeactivated.Args, HandleAccountDeactivated.Result>
{
    public record Args(string EmployeeId) : Link<HandleAccountDeactivated, Result>;

    public abstract record Result
    {
        private Result()
        {
        }

        public record RecordedDeparture : Result;

        public record AlreadyLeft : Result;

        public record UnknownEmployee(string EmployeeId) : Result;
    }

    public override async Task<Result> Invoke(Args args, CancellationToken cancellationToken)
    {
        var employee = await employees.Find(args.EmployeeId, cancellationToken);
        if (employee is null)
            return new Result.UnknownEmployee(args.EmployeeId);
        if (employee.Status == EmploymentStatus.Left)
            return new Result.AlreadyLeft();

        await leaverRecorder.Record(employee, cancellationToken);
        return new Result.RecordedDeparture();
    }
}
