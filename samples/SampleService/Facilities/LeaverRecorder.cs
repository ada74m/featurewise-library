namespace SampleService;

// Records that an employee has left, whichever feature learns of it, and publishes that they have.
// Fire and HandleAccountDeactivated share this rather than one invoking the other.
public class LeaverRecorder(IEmployeeStore employees, IEventPublisher events, TimeProvider time)
{
    public async Task Record(Employee employee, CancellationToken cancellationToken)
    {
        var leftOn = time.Today();
        await employees.Save(employee with { Status = EmploymentStatus.Left, LeftOn = leftOn }, cancellationToken);

        // Saving and publishing aren't atomic: a failure between the two loses the event. A real service
        // would publish through an outbox, from a stream of the table's changes.
        await events.Publish(new EmployeeLeft(employee.Id, leftOn), cancellationToken);
    }
}
