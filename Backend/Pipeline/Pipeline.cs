using LIAR_backend.Expressions;
using LIAR_backend.LLM;
using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LIAR_backend
{
    public class Pipeline
    {
        private static readonly string statement_extraction = File.ReadAllText("statement_extraction.txt").Trim();
        private static readonly string statement_to_logic = File.ReadAllText("statement_to_logic.txt").Trim();
        private static readonly string resolve_formula = File.ReadAllText("resolve_formula.txt").Trim();
        private static readonly string synonym_finder = File.ReadAllText("synonym_finder.txt").Trim();
        private static readonly Dictionary<string, string> complex_predicates = LoadPredicates();

        private const int maxIterations = 5;

        private static Dictionary<string, string> LoadPredicates()
        {
            Dictionary<string, string> dict = new();

            string[] lines = File.ReadAllLines("complex_predicates.txt");
            lines = lines.Where(l => l != "").ToArray();

            for (int i = 0; i + 2 < lines.Length; i += 3)
            {
                string[] names = lines[i].Split(',');
                string path = lines[i + 1].Trim();
                string description = lines[i + 2].Trim();

                foreach (string name in names)
                {
                    dict[NormalizePredicate(name)] = "SQL: " + path + " | DESCRIPTION: " + description;
                }
            }

            return dict;
        }

        private static string NormalizePredicate(string input)
        {
            string normalized = input.ToLowerInvariant();

            normalized = Regex.Replace(
                normalized,
                @"\b(in|at|on|to|from|of|for|with|by|into|within|over|under|a|an|the)\b",
                " "
            );

            return Regex.Replace(normalized, @"\s+", " ").Trim();
        }

        private class ExtractedStatement
        {
            public string Exact { get; set; } = "";
            public string Disambiguated { get; set; } = "";
            public string Logic { get; set; } = "";
        }

        // Could return a empty list
        public static async Task<List<Statement>> ExtractStatements(string input)
        {
            // check if the statement can be found in the text
            List<Statement> CheckExtractedStatements(List<Statement> statements, string input)
            {
                int removed = 0;
                for (int i = 0; i < statements.Count; i++)
                {
                    if (!input.Contains(statements[i].Text))
                    {
                        statements.RemoveAt(i);
                        removed++;
                        i--;
                    }
                }

                Info.Write("Statements not found: " + removed);
                return statements;
            }

            List<Statement> extracted = new();
            string json = await Model.Prompt(input, statement_extraction, "", "");

            for(int i = 0; i < maxIterations; i++)
            {
                try
                {
                    List<ExtractedStatement>? result =
                    JsonSerializer.Deserialize<List<ExtractedStatement>>(json);

                    if (result != null)
                    {
                        foreach (ExtractedStatement? statement in result)
                        {
                            if (statement != null)
                            {
                                extracted.Add(new Statement(statement.Exact, statement.Disambiguated));
                            }
                            else
                            {
                                extracted.Clear();
                                new Exception("NULL value was returned!");
                            }
                        }
                        if(extracted.Count > 0)
                        {
                            break;
                        }
                    }
                    else
                    {
                        new Exception("NULL value was returned!");
                    }

                }
                catch(Exception e)
                {
                    string feedback = "The JSON you returned is invalid. Exception: " + e.Message;
                    json = await Model.Prompt(input, statement_extraction, json, feedback);
                }
            }

            return CheckExtractedStatements(extracted, input);
        }

        public static async Task GetFirstOrderLogic(List<Statement> statements)
        {
            string unsupported = "UNSUPPORTED";
            async Task<string> GetFirstOrderLogic(Statement statement)
            {
                string input = $"STATEMENT: {statement.DisambiguatedText}";
                string logic = await Model.Prompt(input, statement_to_logic, "", "");
                if(logic == unsupported)
                {
                    return unsupported;
                }

                bool evaluateSemantics = false;
                string errors = LogicValidator.Evaluate(logic, complex_predicates.Values.ToList(), evaluateSemantics);

                for(int i = 0; i < maxIterations; i++)
                {
                    if(errors == String.Empty)
                    {
                        break;
                    }

                    input = $"STATEMENT: {statement.DisambiguatedText}";
                    logic = await Model.Prompt(input, statement_to_logic, logic, errors);
                    if (logic == unsupported)
                    {
                        return unsupported;
                    }

                    errors = LogicValidator.Evaluate(logic, complex_predicates.Values.ToList(), evaluateSemantics);
                }

                if(errors != String.Empty)
                {
                    return unsupported;
                }
                else
                {
                    return logic;
                }
            }

            // get logic
            foreach (Statement statement in statements)
            {
                statement.Formula = await GetFirstOrderLogic(statement);
                if(statement.Formula == unsupported)
                {
                    continue;
                }

                try
                {
                    statement.NaturalFormula = statement.Formula;
                }
                catch
                {
                    statement.NaturalFormula = unsupported;
                }
            }

            // filter unsupported statements
            statements.RemoveAll(statement => statement.Formula == unsupported);
        }

        public static async Task ResolveFirstOrderLogic(List<Statement> statements)
        {
            string failed = "RESOLUTION FAILED";

            async Task<string> ResolveFormula(Statement statement)
            {
                JsonDocument document = JsonDocument.Parse(statement.Formula!);
                HashSet<string> properties = new(StringComparer.OrdinalIgnoreCase);
                HashSet<string> items = new(StringComparer.OrdinalIgnoreCase);

                void ExtractTerms(JsonElement element)
                {
                    if (element.ValueKind == JsonValueKind.Object)
                    {
                        if (element.TryGetProperty("type", out JsonElement typeElement) &&
                        typeElement.GetString() == "predicate")
                        {
                            if (element.TryGetProperty("property", out JsonElement propertyElement))
                            {
                                string? property = propertyElement.GetString();
                                if (!String.IsNullOrWhiteSpace(property) && !property.StartsWith("?"))
                                {
                                    properties.Add(property);
                                }
                            }
                            if (element.TryGetProperty("source", out JsonElement sourceElement))
                            {
                                string? source = sourceElement.GetString();
                                if (!String.IsNullOrWhiteSpace(source) && !source.StartsWith("?"))
                                {
                                    items.Add(source);
                                }
                            }
                            if (element.TryGetProperty("target", out JsonElement targetElement))
                            {
                                string? target = targetElement.GetString();
                                if (!String.IsNullOrWhiteSpace(target) && !target.StartsWith("?"))
                                {
                                    items.Add(target);
                                }
                            }
                        }

                        foreach (JsonProperty property in element.EnumerateObject())
                        {
                            ExtractTerms(property.Value);
                        }
                    }
                    else if (element.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement child in element.EnumerateArray())
                        {
                            ExtractTerms(child);
                        }
                    }
                }
                ExtractTerms(document.RootElement);

                int propertySuggestions = 0;
                int itemSuggestions = 0;
                StringBuilder resolution = new();

                foreach (string property in properties)
                {
                    resolution.AppendLine($"\"{property}\":");

                    List<string> synonyms = await GetContextSynonyms(property, statement.DisambiguatedText, 2);
                    synonyms.Insert(0, property);

                    // check the property and the found synonyms
                    foreach (string synonym in synonyms)
                    {
                        List<string> suggestions = await Wikidata.Resolve(synonym, WikidataType.Property, 5);

                        // check if a complex predicate like "relative of" is available
                        if (complex_predicates.TryGetValue(NormalizePredicate(synonym), out string? entry))
                        {
                            suggestions.Add($"TYPE: path | LABEL: {synonym} | {entry}");
                        }
                        foreach (string suggestion in suggestions)
                        {
                            resolution.AppendLine(suggestion);
                        }
                        resolution.AppendLine();

                        propertySuggestions += suggestions.Count;
                        if(suggestions.Count > 0)
                        {
                            break;
                        }
                    }
                }
                if (propertySuggestions == 0)
                {
                    statement.TruthValue = TruthValue.Unknown;
                    return failed;
                }

                foreach (string item in items)
                {
                    resolution.AppendLine($"\"{item}\":");

                    List<string> suggestions = await Wikidata.Resolve(item, WikidataType.Item, 5);
                    suggestions = suggestions.Take(5).ToList();

                    itemSuggestions += suggestions.Count;
                    foreach (string suggestion in suggestions)
                    {
                        resolution.AppendLine(suggestion);
                    }

                    resolution.AppendLine();
                }
                if(itemSuggestions == 0)
                {
                    statement.TruthValue = TruthValue.Unknown;
                    return failed;
                }

                string input = 
                $"FORMULA: {statement.Formula}\n\n" +
                $"RESOLUTION SUGGESTIONS:\n{resolution}";

                bool evaluateSemantics = true;
                string logic = await Model.Prompt(input, resolve_formula, "", "");
                string errors = LogicValidator.Evaluate(logic, complex_predicates.Values.ToList(), evaluateSemantics);

                for(int i = 0; i < maxIterations; i++)
                {
                    if(errors == String.Empty)
                    {
                        break;
                    }

                    logic = await Model.Prompt(input, resolve_formula, logic, errors);
                    errors = LogicValidator.Evaluate(logic, complex_predicates.Values.ToList(), evaluateSemantics);
                }
                if(logic.Contains(LogicValidator.ERROR) || errors != String.Empty)
                {
                    statement.TruthValue = TruthValue.Unknown;
                    return failed;
                }

                return logic;
            }

            // resolve all formulas
            foreach (Statement statement in statements)
            {
                statement.Formula = await ResolveFormula(statement);
            }

            statements.RemoveAll(statement => statement.Formula == failed);
        }

        /// <summary>
        /// ∀x P(x) becomes ∃x ¬P(x) (counterexample search)
        /// </summary>
        public static void EliminateForall(List<Statement> statements)
        {
            foreach (Statement statement in statements)
            {
                JsonDocument document = JsonDocument.Parse(statement.Formula!);
                JsonElement root = document.RootElement;

                string quantifier = root.GetProperty("quantifier").GetString()!;
                string expression = root.GetProperty("expression").GetRawText();

                if (quantifier == "forall")
                {
                    statement.Formula = $$"""{"type":"not","body":{{expression}}}""";
                    statement.CounterExample = true;
                    statement.ClosedWorldAssumption = true;
                }
                else
                {
                    statement.Formula = expression;
                    statement.CounterExample = false;
                    statement.ClosedWorldAssumption = false;
                }
            }
        }

        public static async Task EvaluateStatements(List<Statement> statements)
        {
            foreach (Statement statement in statements)
            {
                // Intended logic:
                // Albert Einstein was a physicist.         -> TRUE
                // Albert Einstein was not a physicist.     -> FALSE
                // Albert Einstein was an astronaut.        -> UNKNOWN
                // Albert Einstein was not an astronaut.    -> UNKNOWN

                // Normal statements use an open-world assumption: missing Wikidata information is UNKNOWN.
                // For universal statements, counterexample searches use a closed-world assumption so that
                // a missing required fact may serve as a counterexample.

                bool result = false;
                Expression expression = ExpressionTranslator.FromJson(statement.Formula!);

                // no negations or a closed world assumption are ok
                if (!Normalizer.ContainsNot(expression.Simplify()) || statement.ClosedWorldAssumption)
                {
                    // compile positive or universal counter example query
                    statement.Query = QueryCompiler.Process(expression);
                    result = await Wikidata.Query(statement);
                }
                else
                {
                    // if we can rewrite to outer not
                    // and there are no comparisons, since a failed comparison may indicate
                    // either a false condition or an evaluation error
                    Not? outerNot = Normalizer.ToOuterNot(expression);
                    if (outerNot is not null && !Normalizer.ContainsNot(outerNot.Body) && !Normalizer.ContainsComparison(outerNot.Body))
                    {
                        // safe counter-evidence query
                        statement.Query = QueryCompiler.Process(outerNot.Body);
                        statement.CounterExample = true;
                        result = await Wikidata.Query(statement);
                    }
                    else
                    {
                        statement.TruthValue = TruthValue.Unknown;
                    }
                }

                // get truth value
                if (result == true)
                {
                    if (statement.CounterExample)
                    {
                        statement.TruthValue = TruthValue.False;
                    }
                    else
                    {
                        statement.TruthValue = TruthValue.True;
                    }
                }
                else
                {
                    statement.TruthValue = TruthValue.Unknown;
                }

                // write proof
                if (statement.TruthValue != TruthValue.Unknown)
                {
                    JsonDocument doc = JsonDocument.Parse(statement.NaturalFormula!);
                    JsonElement body = doc.RootElement.GetProperty("expression");
                    JsonElement quantifier = doc.RootElement.GetProperty("quantifier");

                    string quantifierSymbol = quantifier.GetString() switch
                    {
                        "forall" => "∀ ",
                        "exists" => "∃ ",
                        "none" => "",
                        _ => ""
                    };

                    Expression value = ExpressionTranslator.FromJson(body);
                    string variables = string.Join(", ", value.GetVariables());

                    string content = "";
                    if(value is Not not)
                    {
                        content = not.Body.Print();
                    }
                    else
                    {
                        content = value.Print();
                    }

                    string formula = "";
                    if (quantifierSymbol != "")
                    {
                        formula = quantifierSymbol + variables + " " + content;
                    }
                    else
                    {
                        formula = content;
                    }

                    formula += " evaluated to " + statement.TruthValue.ToString().ToUpperInvariant() + " over Wikidata.";
                    formula = formula.Replace("?", "var:");
                    if (value is Not)
                    {
                        formula = "NOT " + formula;
                    }

                    if (statement.Proof == String.Empty)
                    {
                        statement.Proof = "PROOF:\n" + formula + "\n□";
                    }
                    else
                    {
                        statement.Proof = "PROOF:\n" + formula + "\n□\n\nEXAMPLE: " + statement.Proof;
                    }
                }
                else
                {
                    statement.Proof = "The claim could not be proven or disproven.";
                }

                if (statement.ClosedWorldAssumption && statement.TruthValue != TruthValue.Unknown)
                {
                    statement.Proof += "\n\nREMARK: A closed-world assumption was used for this proof. " +
                    "This means we assume that all relevant knowledge is stored in Wikidata, and therefore " +
                    "a statement that cannot be found in Wikidata is considered FALSE.";
                }
            }
        }

        /// <summary>
        /// Returns a list of words that mean the same thing in a given context.
        /// </summary>
        private static async Task<List<string>> GetContextSynonyms(string word, string context, int limit)
        {
            string input = $"WORD: {word} | CONTEXT:  {context}";
            string json = "";
            string feedback = "";

            while (true)
            {
                json = await Model.Prompt(input, synonym_finder, json, feedback);

                try
                {
                    // check if a json list was returned
                    List<string> words = JsonSerializer.Deserialize<List<string>>(json) ?? new();
                    return words.Take(limit).ToList();
                }
                catch (JsonException)
                {
                    feedback = "You returned invalid JSON. RETURN ONLY THE REQUIRED JSON ARRAY.";
                }
            }
        }
    }
}
