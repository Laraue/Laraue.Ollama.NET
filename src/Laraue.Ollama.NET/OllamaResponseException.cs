using System;

namespace Laraue.Ollama.NET;

public class OllamaResponseException : Exception
{
    public OllamaResponseException(string message) : base(message) { }
}