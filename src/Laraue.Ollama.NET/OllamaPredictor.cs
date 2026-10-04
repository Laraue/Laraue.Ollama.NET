using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Laraue.Ollama.NET.Schema;
using Microsoft.Extensions.Logging;

namespace Laraue.Ollama.NET;

public class OllamaPredictor(HttpClient client, ILogger<OllamaPredictor> logger)
    : IOllamaPredictor
{
    private readonly JsonSerializerOptions _options = new()
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly ConcurrentDictionary<Type, SemaphoreSlim?> _semaphores = new ();
    private readonly ConcurrentDictionary<Type, OllamaSchemaProperty?> _schemas = new ();
    
    /// <inheritdoc />
    public Task<TModel> PredictAsync<TModel>(
        string model,
        string prompt,
        string base64EncodedImage,
        Dictionary<string, object>? additionalParameters = null,
        CancellationToken ct = default)
        where TModel : class
    {
        return PredictInternalAsync<TModel>(model, prompt, base64EncodedImage, additionalParameters, ct);
    }

    /// <inheritdoc />
    public Task<TModel> PredictAsync<TModel>(
        string model,
        string prompt,
        Dictionary<string, object>? additionalParameters = null,
        CancellationToken ct = default)
        where TModel : class
    {
        return PredictInternalAsync<TModel>(model, prompt, null, additionalParameters, ct);
    }

    /// <inheritdoc />
    public Task<string> PredictAsync(
        string model,
        string prompt,
        Dictionary<string, object>? additionalParameters = null,
        CancellationToken ct = default)
    {
        return RequestOllamaAsync(model, prompt, null, null, additionalParameters, ct);
    }

    private async Task<TModel> PredictInternalAsync<TModel>(
        string model,
        string prompt,
        string? base64EncodedImage,
        Dictionary<string, object>? additionalParameters = null,
        CancellationToken ct = default)
        where TModel : class
    {
        var semaphore = _semaphores.GetOrAdd(typeof(TModel), _ => new SemaphoreSlim(1, 1))!;
        await semaphore.WaitAsync(ct);

        if (!_schemas.TryGetValue(typeof(TModel), out var schema))
        {
            schema = SchemaGenerator.GetSchema(typeof(TModel));
            _schemas.TryAdd(typeof(TModel), schema);
            
            logger.LogInformation(
                "Ollama schema of type {Type} is {Schema}",
                typeof(TModel),
                JsonSerializer.Serialize(schema, _options));
        }

        semaphore.Release();

        var stringResponse = await RequestOllamaAsync(
            model,
            prompt,
            base64EncodedImage,
            schema,
            additionalParameters,
            ct);

        try
        {
            return JsonSerializer.Deserialize<TModel>(stringResponse, new JsonSerializerOptions(JsonSerializerDefaults.General) { Converters = { new JsonStringEnumConverter() } })!;
        }
        catch (Exception e)
        {
            throw new InvalidOperationException(stringResponse, e);
        }
    }

    private async Task<string> RequestOllamaAsync(
        string model,
        string prompt,
        string? base64EncodedImage,
        object? schema,
        Dictionary<string, object>? additionalParameters = null,
        CancellationToken ct = default)
    {
        var request = new Dictionary<string, object>
        {
            ["model"] = model,
            ["prompt"] = prompt,
            ["stream"] = false,
            ["format"] = schema!,
        };

        if (base64EncodedImage is not null)
            request["images"] = new[] { base64EncodedImage };

        var options = new Dictionary<string, object> { ["temperature"] = 0 };

        if (additionalParameters != null)
        {
            foreach (var additionalParameter in additionalParameters)
            {
                switch (additionalParameter.Key)
                {
                    case "options":
                        foreach (var option in ToDictionary(additionalParameter.Value))
                            options[option.Key] = option.Value;
                        break;
                    case "temperature":
                        options["temperature"] = additionalParameter.Value;
                        break;
                    default:
                        request[additionalParameter.Key] = additionalParameter.Value;
                        break;
                }
            }
        }

        request["options"] = options;
        
        var requestJson = JsonSerializer.Serialize(request, _options);
        using var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
        
        using var response = await client.PostAsync("api/generate", content, ct);
        
        try
        {
            response.EnsureSuccessStatusCode();
            var responseStream = await response.Content.ReadAsStreamAsync();
            var ollamaResult = await JsonSerializer.DeserializeAsync<OllamaResult>(responseStream, _options, ct);
            
            var data = 
                ollamaResult!.Response != string.Empty
                    ? ollamaResult.Response
                    : ollamaResult.Thinking
                      ?? throw new OllamaResponseException("No 'response' or 'thinking' properties are returned");
            
            return data;
            
        }
        catch (Exception e)
        {
            var message = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(message, e);
        }
    }
    
    private Dictionary<string, object> ToDictionary(object value)
    {
        var element = JsonSerializer.SerializeToElement(value, _options);
        if (element.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("The 'options' parameter must be an object", nameof(value));

        var result = new Dictionary<string, object>();
        foreach (var property in element.EnumerateObject())
            result[property.Name] = property.Value.Clone();

        return result;
    }

    private class OllamaResult
    {
        public required string Response { get; set; }
        public string? Thinking { get; set; }
    }
}