using System.Text.Json;
using Featurewise;

namespace SampleService.QueueWorker;

// Adapts the directory service's events, such as {"type":"AccountDeactivated","accountId":"e1"}. Account
// ids are employee ids.
public class DirectoryEventsAdapter : ITopicAdapter
{
    public string Source => "directory-events";

    public Link? Adapt(string message)
    {
        using var document = JsonDocument.Parse(message);
        var root = document.RootElement;
        return root.GetProperty("type").GetString() switch
        {
            "AccountDeactivated" => new HandleAccountDeactivated.Args(
                root.GetProperty("accountId").GetString() ?? throw new JsonException("accountId is null")),
            _ => null
        };
    }
}
