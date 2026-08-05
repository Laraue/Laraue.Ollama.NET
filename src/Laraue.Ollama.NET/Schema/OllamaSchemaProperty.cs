using System.Text.Json.Serialization;

namespace Laraue.Ollama.NET.Schema;

[JsonDerivedType(typeof(SchemaGenerator.OllamaSchemaObjectProperty))]
[JsonDerivedType(typeof(SchemaGenerator.OllamaSchemaArrayProperty))]
public class OllamaSchemaProperty
{
    [JsonPropertyName("type")]
    public required SchemaPropertyType[] Type { get; init; }
}