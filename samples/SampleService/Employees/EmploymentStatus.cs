using System.Text.Json.Serialization;

namespace SampleService;

// Whether an employee still works here. Written to JSON by name, so clients needn't know the numbers.
[JsonConverter(typeof(JsonStringEnumConverter<EmploymentStatus>))]
public enum EmploymentStatus
{
    Active,
    Left
}
