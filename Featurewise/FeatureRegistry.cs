using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Featurewise;

// What Features needs to know about registered features beyond the container: their names and result
// types, which JSON serialization uses. One registry is shared by a service collection's builders and
// its Features.
internal sealed class FeatureRegistry
{
    private readonly Dictionary<Type, string> _featureNamesByArgsType = new();
    private readonly HashSet<Type> _resultTypes = new();

    public void Add(Type argsType, string name, Type resultType)
    {
        if (_featureNamesByArgsType.ContainsValue(name))
            throw new InvalidOperationException($"another registered feature is already named {name}");

        _featureNamesByArgsType.Add(argsType, name);
        _resultTypes.Add(resultType);
    }

    public JsonSerializerOptions ConfigureJson(JsonSerializerOptions options)
    {
        options.TypeInfoResolver = (options.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver())
            .WithAddedModifier(AddFeatureNamesToLinks)
            .WithAddedModifier(RequireLinkArguments)
            .WithAddedModifier(AddCaseNamesToResults);

        // Clients and other serializers needn't put the feature or case name first.
        options.AllowOutOfOrderMetadataProperties = true;
        return options;
    }

    // Makes every link type, concrete or a base such as Link or Link<TResult>, polymorphic over the registered
    // links it could be, using feature names as type discriminators. A concrete link counts as one of
    // its own derived types, so it carries its feature name however it's declared.
    private void AddFeatureNamesToLinks(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object || !typeof(Link).IsAssignableFrom(typeInfo.Type))
            return;

        var derivedTypes = _featureNamesByArgsType
            .Where(entry => typeInfo.Type.IsAssignableFrom(entry.Key))
            .Select(entry => new JsonDerivedType(entry.Key, entry.Value))
            .ToList();

        // Without this, a link to an unregistered feature would be written without its arguments
        // when declared as a base type, or without its feature name when declared as itself.
        if (derivedTypes.Count == 0)
        {
            typeInfo.OnSerializing = link => throw new NotSupportedException(
                $"can't serialize {link.GetType().Name}: it isn't a link to a registered feature");
            return;
        }

        typeInfo.PolymorphismOptions = new JsonPolymorphismOptions { TypeDiscriminatorPropertyName = Features.FeatureNameProperty };
        foreach (var derivedType in derivedTypes)
            typeInfo.PolymorphismOptions.DerivedTypes.Add(derivedType);
    }

    // Links come back from clients, so a link's JSON must supply every non-nullable argument that has
    // no default, and mustn't make any of them null. This is .NET's RespectRequiredConstructorParameters
    // and RespectNullableAnnotations, applied to links alone so that a host's other types are read as
    // they always were, and leaving nullable arguments optional.
    private static void RequireLinkArguments(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object || !typeof(Link).IsAssignableFrom(typeInfo.Type))
            return;

        var nullability = new NullabilityInfoContext();
        var nonNullableReferences = new List<PropertyInfo>();
        foreach (var property in typeInfo.Properties)
        {
            if (property.AttributeProvider is not PropertyInfo member || !IsNonNullable(member, nullability))
                continue;

            if (property.AssociatedParameter is { HasDefaultValue: false })
                property.IsRequired = true;
            if (!member.PropertyType.IsValueType)
                nonNullableReferences.Add(member);
        }

        if (nonNullableReferences.Count == 0)
            return;

        // The serializer doesn't check nullability of constructor arguments unless told to for every
        // type, so check after deserializing, keeping whatever already runs then, such as Link's own.
        var onDeserialized = typeInfo.OnDeserialized;
        typeInfo.OnDeserialized = link =>
        {
            onDeserialized?.Invoke(link);
            foreach (var member in nonNullableReferences)
                if (member.GetValue(link) is null)
                    throw new JsonException($"{typeInfo.Type.Name}.{member.Name} can't be null");
        };
    }

    private static bool IsNonNullable(PropertyInfo member, NullabilityInfoContext nullability) =>
        member.PropertyType.IsValueType
            ? Nullable.GetUnderlyingType(member.PropertyType) is null
            : nullability.Create(member).WriteState == NullabilityState.NotNull;

    // Makes a registered feature's result type polymorphic over its cases, if it has any, using case
    // names as type discriminators. A case counts as one of its own derived types, so it carries its
    // case name however it's declared, unless it's sealed: sealed types can't be polymorphic, so a
    // sealed case names itself only when written through its result type.
    private void AddCaseNamesToResults(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object || typeInfo.Type.IsSealed)
            return;

        var resultType = _resultTypes.Contains(typeInfo.Type) ? typeInfo.Type
            : typeInfo.Type.DeclaringType is { } declaringType && _resultTypes.Contains(declaringType) ? declaringType
            : null;
        if (resultType is null)
            return;

        var cases = CasesOf(resultType).Where(typeInfo.Type.IsAssignableFrom).ToList();
        if (cases.Count == 0)
            return;

        typeInfo.PolymorphismOptions = new JsonPolymorphismOptions { TypeDiscriminatorPropertyName = Features.CaseProperty };
        foreach (var @case in cases)
            typeInfo.PolymorphismOptions.DerivedTypes.Add(new JsonDerivedType(@case, @case.Name));
    }

    // The cases of a result with cases: concrete public types nested in an abstract result type and
    // derived from it. Empty for any other type.
    private static IEnumerable<Type> CasesOf(Type resultType) =>
        resultType.IsAbstract
            ? resultType.GetNestedTypes().Where(nested => !nested.IsAbstract && resultType.IsAssignableFrom(nested))
            : [];
}
