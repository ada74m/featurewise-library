using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace SampleService;

// Keeps employees in a DynamoDB table keyed by Id, one item per employee.
public class DynamoEmployeeStore(IAmazonDynamoDB dynamo, string tableName) : IEmployeeStore
{
    public async Task<Employee?> Find(string id, CancellationToken cancellationToken)
    {
        var response = await dynamo.GetItemAsync(
            new GetItemRequest { TableName = tableName, Key = KeyOf(id), ConsistentRead = true }, cancellationToken);
        return response.Item is { Count: > 0 } item ? FromItem(item) : null;
    }

    public Task Save(Employee employee, CancellationToken cancellationToken) =>
        dynamo.PutItemAsync(new PutItemRequest { TableName = tableName, Item = ToItem(employee) }, cancellationToken);

    public Task Delete(string id, CancellationToken cancellationToken) =>
        dynamo.DeleteItemAsync(new DeleteItemRequest { TableName = tableName, Key = KeyOf(id) }, cancellationToken);

    public async Task<IReadOnlyList<Employee>> All(CancellationToken cancellationToken)
    {
        var employees = new List<Employee>();
        Dictionary<string, AttributeValue>? startKey = null;
        do
        {
            var page = await dynamo.ScanAsync(
                new ScanRequest { TableName = tableName, ExclusiveStartKey = startKey }, cancellationToken);
            employees.AddRange((page.Items ?? []).Select(FromItem));
            startKey = page.LastEvaluatedKey is { Count: > 0 } lastKey ? lastKey : null;
        } while (startKey is not null);

        return employees;
    }

    private static Dictionary<string, AttributeValue> KeyOf(string id) => new() { ["Id"] = new AttributeValue { S = id } };

    private static Dictionary<string, AttributeValue> ToItem(Employee employee)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["Id"] = new() { S = employee.Id },
            ["Name"] = new() { S = employee.Name },
            ["Status"] = new() { S = employee.Status.ToString() },
            ["ReviewDue"] = new() { S = employee.ReviewDue.ToString("O") }
        };
        if (employee.ManagerId is not null)
            item["ManagerId"] = new() { S = employee.ManagerId };
        if (employee.LeftOn is { } leftOn)
            item["LeftOn"] = new() { S = leftOn.ToString("O") };
        return item;
    }

    private static Employee FromItem(Dictionary<string, AttributeValue> item) =>
        new(
            item["Id"].S,
            item["Name"].S,
            item.TryGetValue("ManagerId", out var managerId) ? managerId.S : null,
            Enum.Parse<EmploymentStatus>(item["Status"].S),
            DateOnly.Parse(item["ReviewDue"].S),
            item.TryGetValue("LeftOn", out var leftOn) ? DateOnly.Parse(leftOn.S) : null);
}
