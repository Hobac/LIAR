using LIAR_backend.Expressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LIAR_backend
{
    public enum WikidataType
    {
        Void,
        Item,
        Property
    }

    public static class Wikidata
    {
        public const string WikidataItemUrl = "wikipedia.org/wiki/";
        private static readonly HttpClient client = new HttpClient();

        static Wikidata()
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "LIAR (Liar interrogates AI responses) university project"
            );
        }

        public static async Task<List<string>> Resolve(string input, WikidataType type, int limit)
        {
            async Task<List<string>> Search(string input, string type, int limit)
            {
                string url =
                        "https://www.wikidata.org/w/api.php" +
                        "?action=wbsearchentities" +
                        "&search=" + Uri.EscapeDataString(input) +
                        "&language=en" +
                        "&type=" + type +
                        "&limit=" + limit +
                        "&format=json";

                HttpResponseMessage response;
                while (true)
                {
                    response = await client.GetAsync(url);

                    if (response.StatusCode != HttpStatusCode.TooManyRequests) break;
                    TimeSpan waitTime = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(5);

                    Info.Write($"Wikidata rate limit. Waiting {waitTime.TotalSeconds}s...");
                    await Task.Delay(waitTime);

                    response.Dispose();
                }

                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync();

                Info.Write("Resolving: " + input);

                JsonDocument document = JsonDocument.Parse(json);

                JsonElement results = document.RootElement.GetProperty("search");

                List<string> suggestions = new();

                foreach (JsonElement result in results.EnumerateArray())
                {
                    string id = result.GetProperty("id").GetString() ?? "";
                    string label = result.TryGetProperty("label", out JsonElement labelElement)
                        ? labelElement.GetString() ?? "" : "";
                    string description = result.TryGetProperty("description", out JsonElement descriptionElement)
                        ? descriptionElement.GetString() ?? "" : "";

                    suggestions.Add($"TYPE: {type} | LABEL: {label} | ID: {id} | DESCRIPTION: {description}");
                }

                return suggestions;
            }

            string NormalizeSearch(string input)
            {
                input = Regex.Replace(input, "([a-z])([A-Z])", "$1 $2");

                input = Regex.Replace(
                    input,
                    @"^(the|a|an)\s+",
                    "",
                    RegexOptions.IgnoreCase
                );

                return input.Trim();
            }

            input = NormalizeSearch(input);
            List<string> suggestions = await Search(input, type.ToString().ToLower(), limit);

            return suggestions;
        }

        public static async Task<bool> Query(Statement statement)
        {
            string url =
            "https://query.wikidata.org/sparql" +
            "?query=" + Uri.EscapeDataString(statement.Query!) +
            "&format=json";

            while (true)
            {
                HttpResponseMessage? response = null;

                try
                {
                    response = await client.GetAsync(url);
                    response.EnsureSuccessStatusCode();

                    string json = await response.Content.ReadAsStringAsync();
                    JsonDocument document = JsonDocument.Parse(json);
                    statement.Proof = String.Empty;

                    // ASK query
                    if (document.RootElement.TryGetProperty("boolean", out JsonElement boolean))
                    {
                        return boolean.GetBoolean();
                    }

                    // SELECT query
                    if (document.RootElement.TryGetProperty("results", out JsonElement results) &&
                    results.TryGetProperty("bindings", out JsonElement bindings))
                    {
                        if (bindings.GetArrayLength() == 0)
                        {
                            return false;
                        }

                        if (bindings.GetArrayLength() == 1)
                        {
                            foreach (JsonProperty property in bindings[0].EnumerateObject())
                            {
                                if (property.Value.TryGetProperty("value", out JsonElement value))
                                {
                                    string? result = value.GetString();

                                    if (result != null && Uri.IsWellFormedUriString(result, UriKind.Absolute))
                                    {
                                        statement.Proof = result;
                                    }
                                }
                            }
                        }

                        return true;
                    }

                    throw new Exception("Unexpected Wikidata response.");
                }
                catch (Exception e)
                {
                    if (response is not null && response.StatusCode == HttpStatusCode.GatewayTimeout)
                    {
                        Info.Write("Wikidata query timed out.");
                    }

                    Info.Write($"Wikidata exception: {e.Message}");

                    // if the query threw an exception due to timeout
                    // or if it was invalid just return false, which gets evaluated to UNKNOWN
                    return false;
                }
            }
        }
    }
}
