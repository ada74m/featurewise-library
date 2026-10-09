using System.Collections.Concurrent;

namespace SampleService.Tests;

// Keeps employees in memory in place of DynamoDB.
public class InMemoryEmployeeStore : IEmployeeStore
{
    private readonly ConcurrentDictionary<string, Employee> _employees = new();

    public void Add(Employee employee) => _employees[employee.Id] = employee;

    public Employee? this[string id] => _employees.GetValueOrDefault(id);

    public int Count => _employees.Count;

    public Task<Employee?> Find(string id, CancellationToken cancellationToken) => Task.FromResult(this[id]);

    public Task Save(Employee employee, CancellationToken cancellationToken)
    {
        Add(employee);
        return Task.CompletedTask;
    }

    public Task Delete(string id, CancellationToken cancellationToken)
    {
        _employees.TryRemove(id, out _);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Employee>> All(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Employee>>(_employees.Values.ToList());
}
