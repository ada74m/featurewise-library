namespace SampleService;

// An employee, as the service stores them.
public record Employee(
    string Id,
    string Name,
    string? ManagerId,
    EmploymentStatus Status,
    DateOnly ReviewDue,
    DateOnly? LeftOn = null);
