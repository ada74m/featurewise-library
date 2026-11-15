using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Featurewise;

// Reads and writes every link type, concrete or a base such as Link or Link<TResult>. A link's JSON names
// its feature, qualified by the service, in a property that also says the feature's family, as in
// {"$command":"hr/Fire","EmployeeId":"e1"}: $query, $command or $worker. Reading accepts the property
// anywhere in the object, and refuses a link that names another service's feature, an unregistered one,
// or a feature of another family. The older $feature is read too, so links stored before the family was
// named still work, but it's never written.
internal sealed class LinkJsonConverterFactory(FeatureRegistry registry) : JsonConverterFactory
{
    // The property links were read and written with before they named their family.
    private const string OlderFeatureProperty = "$feature";

    private static readonly string[] FamilyProperties =
        [Features.FeatureProperty(FeatureFamily.Query), Features.FeatureProperty(FeatureFamily.Command), Features.FeatureProperty(FeatureFamily.Worker)];

    // For each options this factory serves, the same options without it, which read and write a link's
    // arguments as a plain object. Links among the arguments still come back here.
    private readonly ConditionalWeakTable<JsonSerializerOptions, JsonSerializerOptions> _plainOptions = new();

    public override bool CanConvert(Type typeToConvert) => typeof(Link).IsAssignableFrom(typeToConvert);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(typeof(LinkJsonConverter<>).MakeGenericType(typeToConvert), this)!;

    internal JsonSerializerOptions PlainOptions(JsonSerializerOptions options) =>
        _plainOptions.GetValue(options, outer =>
        {
            var plain = new JsonSerializerOptions(outer);
            plain.Converters.Remove(this);
            plain.TypeInfoResolver = (outer.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver())
                .WithAddedModifier(typeInfo =>
                {
                    if (typeInfo.Kind != JsonTypeInfoKind.Object)
                        return;
                    foreach (var property in typeInfo.Properties)
                        if (CanConvert(property.PropertyType))
                            property.CustomConverter = CreateConverter(property.PropertyType, outer);
                });
            return plain;
        });

    // The property and the qualified name a link of this type is written with.
    internal (string Property, string Name) NameOf(Type linkType)
    {
        if (!registry.TryGet(linkType, out var name, out var family))
            throw new NotSupportedException($"can't serialize {linkType.Name}: it isn't a link to a registered feature");
        return (Features.FeatureProperty(family), $"{registry.Service}/{name}");
    }

    // The link type a link's JSON names, with the naming property taken out of it. A link that names
    // nothing can still be read as a concrete link type.
    internal Type LinkTypeIn(JsonObject json, Type declared)
    {
        var named = FamilyProperties.Append(OlderFeatureProperty).Where(json.ContainsKey).ToList();
        if (named.Count > 1)
            throw new JsonException($"a link names its feature once, but this one has {string.Join(" and ", named)}");
        if (named.Count == 0)
        {
            if (!declared.IsAbstract && registry.TryGet(declared, out _, out _))
                return declared;
            throw new JsonException("a link names its feature in $query, $command or $worker");
        }

        var property = named[0];
        var qualified = json[property] is JsonValue value && value.TryGetValue<string>(out var text) ? text
            : throw new JsonException($"a link's {property} must be a string, such as {registry.Service}/SomeFeature");
        json.Remove(property);

        var prefix = registry.Service + "/";
        var linkType = qualified.StartsWith(prefix, StringComparison.Ordinal)
            ? registry.ArgsTypeNamed(qualified[prefix.Length..])
            : null;
        if (linkType is null || !registry.TryGet(linkType, out _, out var family))
            throw new JsonException($"{qualified} isn't one of {registry.Service}'s features");

        var expected = Features.FeatureProperty(family);
        if (property != OlderFeatureProperty && property != expected)
            throw new JsonException($"{qualified} is a {family.ToString().ToLowerInvariant()}, so its link names it in {expected}, not {property}");
        if (!declared.IsAssignableFrom(linkType))
            throw new JsonException($"a link to {qualified} isn't a {declared.Name}");
        return linkType;
    }
}
