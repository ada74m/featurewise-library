namespace Featurewise.Tests;

// A worker, which has no caller waiting for an answer and so returns Nil.
public class HandleEmployeeLeft : Worker<HandleEmployeeLeft, HandleEmployeeLeft.Args>
{
    public record Args(string EmployeeId) : Link<HandleEmployeeLeft, Nil>;

    public override Task<Nil> Invoke(Args args, CancellationToken cancellationToken) =>
        Task.FromResult(Nil.Value);
}
