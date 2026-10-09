using System.Text.Json;
using Featurewise;
using Microsoft.Extensions.DependencyInjection;

namespace SampleService.Tests;

// Builds a standalone Features over the fakes, for testing core features without an entrypoint.
public static class ServiceFeatures
{
    public static Features Over(Fakes fakes, Action<Features> register)
    {
        var features = new Features();
        register(features);
        var json = features.ConfigureJson(new JsonSerializerOptions());
        return features
            .RegisterDependencies(services => services.AddSingleton(json))
            .RegisterDependencies(fakes.Register)
            .Build();
    }
}
