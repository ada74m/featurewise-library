using Featurewise;

namespace SampleService;

// Tells payroll that someone has left. Telling them twice does no harm, so redeliveries needn't be
// recognised.
public class HandleEmployeeLeft(IEmployeeStore employees, INotifier notifier)
    : Feature<HandleEmployeeLeft, HandleEmployeeLeft.Args, Nil>
{
    public record Args(string EmployeeId) : Link<HandleEmployeeLeft, Nil>;

    public override async Task<Nil> Invoke(Args args, CancellationToken cancellationToken)
    {
        var employee = await employees.Find(args.EmployeeId, cancellationToken);
        var who = employee is null ? args.EmployeeId : $"{employee.Name} ({employee.Id})";
        await notifier.Notify("payroll", $"{who} has left: close their payroll", cancellationToken);
        return Nil.Value;
    }
}
