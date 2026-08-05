using Microsoft.Extensions.Logging.Abstractions;

namespace Laraue.Ollama.NET.IntegrationTests;

public class PredictorTests
{
    [Fact]
    public async Task PredictorTest()
    {
        var predictor = new OllamaPredictor(
            new HttpClient { BaseAddress = new Uri("http://localhost:11434/") },
            new NullLogger<OllamaPredictor>());

        var response = await predictor.PredictAsync<HumanDto[]>(
            "gemma3:1b",
            "Generate 3 humans data please");
        
        Assert.Equal(3, response.Length);
    }

    public class HumanDto
    {
        public required string Name { get; set; }
        public required string Gender { get; set; }
    }
}