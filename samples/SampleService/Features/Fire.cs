using Featurewise;

namespace SampleService;

// Ends someone's employment. What follows, such as telling payroll, happens later through the queue.
public class Fire(IEmployeeStore employees, LeaverRecorder leaverRecorder) : Feature<Fire, Fire.Args, Fire.Result>
{
    public record Args(string EmployeeId) : Link<Fire, Result>;

    public abstract record Result
    {
        private Result()
        {
        }

        public record Fired : Result;

        public record AlreadyLeft : Result;

        public record NotFound(string EmployeeId) : Result;
    }

    public override async Task<Result> Invoke(Args args, CancellationToken cancellationToken)
    {
        var employee = await employees.Find(args.EmployeeId, cancellationToken);
        if (employee is null)
            return new Result.NotFound(args.EmployeeId);
        if (employee.Status == EmploymentStatus.Left)
            return new Result.AlreadyLeft();

        await leaverRecorder.Record(employee, cancellationToken);
        return new Result.Fired();
    }
}
