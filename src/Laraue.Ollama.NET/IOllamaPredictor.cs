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