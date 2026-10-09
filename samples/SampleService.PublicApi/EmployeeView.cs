namespace SampleService.PublicApi;

// An employee as the public API shows them: no review dates, which are internal, and a status in the
// API's own words rather than the domain's enum.
public record EmployeeView(string Id, string Name, string? ManagerId, string Status, DateOnly? LeftOn)
{
    public static EmployeeView From(Employee employee) =>
        new(
            employee.Id,
            employee.Name,
            employee.ManagerId,
            employee.Status switch
            {
                EmploymentStatus.Active => "active",
                EmploymentStatus.Left => "left",
                _ => throw new ArgumentOutOfRangeException(nameof(employee), employee.Status, null)
            },
            employee.LeftOn);
}
