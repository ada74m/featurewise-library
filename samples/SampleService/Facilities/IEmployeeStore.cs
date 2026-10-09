namespace SampleService;

// Where employees are kept.
public interface IEmployeeStore
{
    Task<Employee?> Find(string id, CancellationToken cancellationToken);

    Task Save(Employee employee, CancellationToken cancellationToken);

    Task Delete(string id, CancellationToken cancellationToken);

    // Every employee. A sample-sized service can afford to scan; a larger one would query an index.
    Task<IReadOnlyList<Employee>> All(CancellationToken cancellationToken);
}
