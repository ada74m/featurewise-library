namespace SampleService;

// Someone joined. Published to the service's topic; other services subscribe to it.
public record EmployeeHired(string EmployeeId, string Name, string? ManagerId, DateOnly HiredOn);
