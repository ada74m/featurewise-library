namespace SampleService;

// Someone left, whether they were fired or the directory closed their account. Published to the
// service's topic; payroll, for one, subscribes to close their pay.
public record EmployeeLeft(string EmployeeId, DateOnly LeftOn);
