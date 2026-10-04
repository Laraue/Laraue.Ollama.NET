using System.Text.Json;
using System.Text.Json.Serialization;
using Laraue.Ollama.NET.Schema;

namespace Laraue.Ollama.NET.IntegrationTests;

public class SchemaGeneratorTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static JsonElement Generate(Type type)
        => JsonSerializer.SerializeToElement(SchemaGenerator.GetSchema(type), typeof(OllamaSchemaProperty), Options);

    [Fact]
    public void ObjectSchema_ContainsRequiredList()
    {
        var schema = Generate(typeof(RatingDto));

        var required = schema.GetProperty("required").EnumerateArray().Select(x => x.GetString()).ToArray();
        Assert.Equal(["RenovationRating", "Kind"], required);
    }

    [Fact]
    public void ArrayOfObjects_ItemsContainRequiredList()
    {
        var schema = Generate(typeof(RatingDto[]));

        var required = schema.GetProperty("items").GetProperty("required").EnumerateArray().Select(x => x.GetString()).ToArray();
        Assert.Equal(["RenovationRating", "Kind"], required);
    }

    [Fact]
    public void Enum_IsMappedToStringWithValues()
    {
        var schema = Generate(typeof(RatingDto)).GetProperty("properties").GetProperty("Kind");

        Assert.Equal("string", schema.GetProperty("type")[0].GetString());
        Assert.Equal(["Flat", "House"], schema.GetProperty("enum").EnumerateArray().Select(x => x.GetString()).ToArray());
    }

    public enum Kind { Flat, House }

    public class RatingDto
    {
        public int RenovationRating { get; set; }
        public Kind Kind { get; set; }
    }
}
