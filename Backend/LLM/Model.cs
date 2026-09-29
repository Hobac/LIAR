using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace LIAR_backend.LLM
{
    public static class Model
    {
        private static readonly HttpClient client = new();

        // api_key.txt is a per-developer, gitignored config file with "key=value" lines, e.g.:
        //   provider=deepseek
        //   model=deepseek-chat
        //   api_key=sk-...
        // See LLM/api_key.example.txt for the full format.
        private static readonly Dictionary<string, string> config = LoadConfig("api_key.txt");

        private static Dictionary<string, string> LoadConfig(string path)
        {
            var config = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (string rawLine in File.ReadAllLines(path))
            {
                string line = rawLine.Trim();
                if (line == "" || line.StartsWith('#'))
                {
                    continue;
                }

                int separator = line.IndexOf('=');
                if (separator == -1)
                {
                    continue;
                }

                string key = line[..separator].Trim();
                string value = line[(separator + 1)..].Trim();
                config[key] = value;
            }

            return config;
        }

        private static string GetRequired(string key)
        {
            if (!config.TryGetValue(key, out string? value) || value == "")
            {
                throw new Exception($"Missing '{key}' entry in api_key.txt.");
            }

            return value;
        }

        public static async Task<string> Prompt(string input, string instructions, string prevOutput, string feedback)
        {
            string provider = GetRequired("provider");
            string model = GetRequired("model");
            string apiKey = GetRequired("api_key");

            if(!string.IsNullOrEmpty(prevOutput))
            {
                input = "YOUR LAST OUTPUT: " + prevOutput + Environment.NewLine + input;
            }
            if (!string.IsNullOrEmpty(feedback))
            {
                input = "FEEDBACK: " + feedback + Environment.NewLine + input;
            }

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            return provider.ToLowerInvariant() switch
            {
                "openai" => await PromptOpenAi(model, input, instructions),
                "deepseek" => await PromptDeepSeek(model, input, instructions),
                _ => throw new Exception($"Unknown provider '{provider}' in api_key.txt (expected 'openai' or 'deepseek').")
            };
        }

        private static async Task<string> PromptOpenAi(string model, string input, string instructions)
        {
            var request = new
            {
                model,
                instructions,
                input,
                max_output_tokens = 1000,
                reasoning = new
                {
                    effort = "none"
                }
            };

            string json = JsonSerializer.Serialize(request);

            HttpResponseMessage response = await client.PostAsync(
                "https://api.openai.com/v1/responses",
                new StringContent(json, Encoding.UTF8, "application/json")
            );

            string responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception(responseJson);

            using JsonDocument document = JsonDocument.Parse(responseJson);
            JsonElement output = document.RootElement.GetProperty("output");

            foreach (JsonElement item in output.EnumerateArray())
            {
                if (item.TryGetProperty("content", out JsonElement content))
                {
                    foreach (JsonElement part in content.EnumerateArray())
                    {
                        if (part.TryGetProperty("type", out JsonElement type) &&
                        type.GetString() == "output_text")
                        {
                            return part.GetProperty("text").GetString()!.Trim();
                        }
                    }
                }
            }

            throw new Exception("No text response returned!");
        }

        private static async Task<string> PromptDeepSeek(string model, string input, string instructions)
        {
            var request = new
            {
                model = model,
                messages = new object[]
                {
                    new { role = "system", content = instructions },
                    new { role = "user", content = input }
                },
                max_tokens = 1000,
                stream = false
            };

            string json = JsonSerializer.Serialize(request);

            HttpResponseMessage response = await client.PostAsync(
                "https://api.deepseek.com/chat/completions",
                new StringContent(json, Encoding.UTF8, "application/json")
            );

            string responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception(responseJson);

            using JsonDocument document = JsonDocument.Parse(responseJson);
            JsonElement choices = document.RootElement.GetProperty("choices");

            foreach (JsonElement choice in choices.EnumerateArray())
            {
                if (choice.TryGetProperty("message", out JsonElement message) &&
                    message.TryGetProperty("content", out JsonElement content))
                {
                    return content.GetString()!.Trim();
                }
            }

            throw new Exception("No text response returned!");
        }
    }
}
