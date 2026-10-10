namespace SampleService;

// Who this service is in the estate. Every link it writes names it, so the UI shell sends each link back
// to the service it came from, and its events name it as their source. Links and events outlive
// deployments, so the id must not change.
public static class Service
{
    public const string Id = "hr";
}
