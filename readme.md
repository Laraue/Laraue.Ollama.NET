## Laraue.Ollama.NET

A typed .NET adapter over the native [Ollama](https://ollama.com) HTTP API. It generates the JSON Schema used for structured output automatically from your C# class via reflection, so you don't have to hand-write and maintain the `format` schema for every response type.

[![latest version](https://img.shields.io/nuget/v/Laraue.Ollama.NET)](https://www.nuget.org/packages/Laraue.Ollama.NET)
[![latest version](https://img.shields.io/nuget/dt/Laraue.Ollama.NET)](https://www.nuget.org/packages/Laraue.Ollama.NET)

Ollama runs a local HTTP service (port `11434` by default) that exposes open-source language and vision models — no cloud API keys, no per-call cost, no data leaving your server. This is useful when:

- you're processing personal data and can't send it to an external provider
- your uptime can't depend on a third-party API's SLA
- you're working in an export-restricted country or an offline/air-gapped environment
- per-call cloud pricing gets too expensive at volume

Because switching models is just changing a string, Ollama is also a fast way to prototype: swap `model` names to compare quality, then move to a fine-tuned model later without touching the integration code.

### Installation

```
dotnet add package Laraue.Ollama.NET
```

### Setup

Register the predictor via Microsoft DI:

```csharp
services.AddHttpClient<IOllamaPredictor, OllamaPredictor>((serviceProvider, client) =>
{
    client.BaseAddress = new Uri("http://localhost:11434/");
    // Replace with your Ollama host if it runs on a separate machine
});
```

### The Interface

`IOllamaPredictor` (namespace `Laraue.Ollama.NET`) exposes three overloads:

```csharp
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Laraue.Ollama.NET;

/// <summary>
/// The class to run ollama predictions.
/// </summary>
public interface IOllamaPredictor
{
    /// <summary>
    /// Run prediction with the <see cref="TModel"/> response schema using the image.
    /// Requires a model that supports image processing.
    /// </summary>
    public Task<TModel> PredictAsync<TModel>(
        string modelName,
        string prompt,
        string base64EncodedImage,
        Dictionary<string, object>? additionalParameters = null,
        CancellationToken ct = default)
        where TModel : class;

    /// <summary>
    /// Run prediction with the <see cref="TModel"/> response schema.
    /// </summary>
    public Task<TModel> PredictAsync<TModel>(
        string modelName,
        string prompt,
        Dictionary<string, object>? additionalParameters = null,
        CancellationToken ct = default)
        where TModel : class;

    /// <summary>
    /// Run prediction and return response string.
    /// </summary>
    public Task<string> PredictAsync(
        string modelName,
        string prompt,
        Dictionary<string, object>? additionalParameters = null,
        CancellationToken ct = default);
}
```

Use the generic overloads when you need the response parsed into a C# type. Use the raw overload when you want the model's free-text response directly, e.g. for freeform generation or when you'll parse it yourself.

`additionalParameters` is passed straight through to the underlying Ollama request (e.g. `temperature`, `top_p`, `num_ctx`) alongside `model`, `prompt`, and `format`, so you don't need to drop to the raw HTTP API just to tune generation options:

```csharp
var predictionResult = await ollamaPredictor.PredictAsync<PredictionResult>(
    model: "gemma3:12b",
    prompt: "Classify the following text and return structured output.",
    additionalParameters: new Dictionary<string, object>
    {
        ["temperature"] = 0.2,
    },
    ct);
```

### Define Your Response Contract

Any C# class or record works — properties map to the generated JSON Schema (e.g. `double` → `number`, `string[]` → array of `string`):

```csharp
public record PredictionResult
{
    public required string[] Objects { get; set; }
}
```

The adapter reflects over the type at call time, builds the `format` JSON Schema, sends the request, and deserializes the response back into that type. Adding or renaming a property immediately affects the next request — no manual schema editing.

### Text Analysis

```csharp
var predictionResult = await ollamaPredictor.PredictAsync<PredictionResult>(
    model: "gemma3:12b",
    prompt: "Classify the following text and return structured output.",
    ct: ct);
```

### Image Analysis

Pass a base64-encoded image to run a vision model against a picture:

```csharp
var imageBytes = File.ReadAllBytes("image.jpg");
var base64EncodedImage = Convert.ToBase64String(imageBytes);

var predictionResult = await ollamaPredictor.PredictAsync<PredictionResult>(
    model: "qwen2.5vl:3b",
    prompt: "Return info about objects on the picture",
    base64EncodedImage: base64EncodedImage,
    ct: ct);
```

Only vision-capable models use the image — for text-only models it's ignored. Check a model's page on [ollama.com/library](https://ollama.com/library) to confirm vision support before downloading.

### Raw Text Response

When you don't need a typed/structured result:

```csharp
var rawResponse = await ollamaPredictor.PredictAsync(
    model: "gemma3:12b",
    prompt: "Translate the following text to French: 'The apartment has two rooms and a large balcony.'",
    ct: ct);
```

### Model Selection Notes

- **Text tasks:** `gemma3:12b` and `qwen2.5:7b` are good starting points. Larger parameter counts reason better; smaller ones run faster.
- **Vision tasks:** `qwen2.5vl:3b` handles image analysis well at modest hardware requirements.
- **First call latency:** Ollama downloads a model on first use if it isn't cached locally. Calls within the default 5-minute idle window load the model from memory and are noticeably faster.
- **Model switching:** changing the `model` string is the only code change needed to compare models — this is the main reason to reach for Ollama over a fixed hosted model.

### Production Notes

Ollama works well in production for latency-tolerant, GPU-bound workloads where data privacy or cost constraints rule out cloud APIs. For latency-sensitive or high-concurrency systems, benchmark throughput on your target hardware first. Decoupling inference from the rest of the app — e.g. an isolated worker host draining a queue — is a practical pattern for production use.

### Real-World Usage

The [real estate aggregator](https://github.com/win7user10/Laraue.Apps.RealEstate/blob/main/src/Laraue.Apps.RealEstate.Prediction.AppServices/OllamaRealEstatePredictor.cs) uses `IOllamaPredictor` with `qwen2.5vl` to score apartment photos for renovation quality — every listing photo gets a `RenovationRating` between 0 and 1 plus tag arrays (`Advantages`, `Problems`) that feed into the final listing ranking.

See the full write-up: [Using Ollama in C# and .NET](https://laraue.com/blog/ollama-dotnet) for more background on the native HTTP API and why this adapter exists.