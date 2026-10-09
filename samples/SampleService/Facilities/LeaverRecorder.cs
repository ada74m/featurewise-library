namespace SampleService;

// Records that an employee has left, whichever feature learns of it, and queues the work that follows.
// Fire and HandleAccountDeactivated share this rather than one invoking the other.
public class LeaverRecorder(IEmployeeStore employees, IWorkQueue queue, TimeProvider time)
{
    public async Task Record(Employee employee, CancellationToken cancellationToken)
    {
        await employees.Save(employee with { Status = EmploymentStatus.Left, LeftOn = time.Today() }, cancellationToken);

        // Saving and sending aren't atomic: a real service would send through an outbox, so a failure
        // between the two can't lose the follow-up.
        await queue.Send(new HandleEmployeeLeft.Args(employee.Id), cancellationToken);
    }
}
