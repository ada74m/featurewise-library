using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Featurewise;

// Reads and writes one link type for LinkJsonConverterFactory: the property naming the feature first, then
// the link's arguments, written as a plain object.
internal sealed class LinkJsonConverter<TLink>(LinkJsonConverterFactory factory) : JsonConverter<TLink> where TLink : Link
{
    public override TLink? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var json = JsonNode.Parse(ref reader) as JsonObject ?? throw new JsonException("a link must be a JSON object");
        var linkType = factory.LinkTypeIn(json, typeToConvert);
        return (TLink?)json.Deserialize(linkType, factory.PlainOptions(options));
    }

    public override void Write(Utf8JsonWriter writer, TLink value, JsonSerializerOptions options)
    {
        var linkType = value.GetType();
        var (property, name) = factory.NameOf(linkType);
        writer.WriteStartObject();
        writer.WriteString(property, name);
        foreach (var argument in JsonSerializer.SerializeToElement(value, linkType, factory.PlainOptions(options)).EnumerateObject())
            argument.WriteTo(writer);
        writer.WriteEndObject();
    }
}
