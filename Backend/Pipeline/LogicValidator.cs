using LIAR_backend.Expressions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LIAR_backend
{
    public static class LogicValidator
    {
        public const string ERROR = "<error>";
        private static readonly Regex itemRegex = new(@"^(Q[0-9]+|\?[a-zA-Z]+|" + ERROR + ")$");
        private static readonly Regex propertyRegex = new(@"^(P[0-9]+|" + ERROR + ")$");
        private static readonly Regex numberRegex = new(@"^([0-9]+(\.[0-9]+)?|" + ERROR + ")$");

        public static string Evaluate(string json, List<string> complexPredicates, bool evaluateSemantics)
        {
            // 1. Check if the provided string is valid JSON
            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(json);
            }
            catch(Exception e)
            {
                return e.Message;
            }

            // 2. Check if there even is a expression
            if(document.RootElement.TryGetProperty("expression", out JsonElement expression))
            {
                // 3. Check if quantifiers of the expression were provided
                string quantifierError = QuantifierCheck(document.RootElement);
                if (quantifierError == String.Empty)
                {
                    // 4. Validate local constraints of the expression 
                    string validatorError = ValidateExpression(expression, 0, complexPredicates, evaluateSemantics);
                    if(validatorError == String.Empty)
                    {
                        // 5. Validate global constraints of the expression
                        string bindingError = BindingCheck(document.RootElement);
                        return bindingError;
                    }
                    else
                    {
                        return validatorError;
                    }
                }
                else
                {
                    return quantifierError;
                }
            }
            else
            {
                return "No expression was provided!";
            }
        }

        /// <summary>
        /// Checks whether all variables used in comparisons are bound by a predicate.
        /// Otherwise, a comparison could fail because it uses an unbound variable.
        /// Each OR expression opens a new branch because it is compiled using UNION.
        /// </summary>
        private static string BindingCheck(JsonElement root)
        {
            // Recursively track variable bindings by conjunction scope.
            // At each AND level, collect all variables bound by predicates in that scope,
            // regardless of their order. OR and NOT create nested scopes, so binding
            // collection stops at those expressions. The bindings collected so far are then
            // passed into the nested scope, where the same process is repeated recursively.
            // Comparisons are valid only if all referenced variables were bound in the
            // current scope or inherited from a higher scope.

            Expression? expression = null;
            try
            {
                expression = ExpressionTranslator.FromJson(root.GetProperty("expression").GetRawText());
            }
            catch(Exception e)
            {
                return e.Message;
            }

            HashSet<string> GetBindings(Expression expression)
            {
                if (expression is Predicate predicate)
                {
                    HashSet<string> bindings = new();

                    if (predicate.Source.StartsWith("?"))
                    {
                        bindings.Add(predicate.Source);
                    }
                    if (predicate.Target.StartsWith("?"))
                    {
                        bindings.Add(predicate.Target);
                    }

                    return bindings;
                }

                // only collect bindings from the current conjunction scope
                // bindings introduced inside OR or NOT expressions are local to those expressions
                // and must not propagate to the surrounding branch

                if (expression is And and)
                {
                    HashSet<string> bindings = GetBindings(and.Left);
                    bindings.UnionWith(GetBindings(and.Right));
                    return bindings;
                }

                return new();
            }

            string Check(Expression expression, HashSet<string> boundVariables)
            {
                if (expression is Comparison comparison)
                {
                    if (comparison.Left.StartsWith("?") && !boundVariables.Contains(comparison.Left))
                    {
                        return $"Comparison variable '{comparison.Left}' is not bound by any predicate " +
                               $"in its current scope or in a higher scope. Variables introduced inside " +
                               $"OR or NOT expressions are local to those expressions and cannot be used " +
                               $"outside them.";
                    }
                    if (comparison.Right.StartsWith("?") && !boundVariables.Contains(comparison.Right))
                    {
                        return $"Comparison variable '{comparison.Right}' is not bound by any predicate " +
                               $"in its current scope or in a higher scope. Variables introduced inside " +
                               $"OR or NOT expressions are local to those expressions and cannot be used " +
                               $"outside them.";
                    }

                    return String.Empty;
                }

                if (expression is And and)
                {
                    HashSet<string> bindings = [.. boundVariables];
                    bindings.UnionWith(GetBindings(and.Left));
                    bindings.UnionWith(GetBindings(and.Right));

                    string error = Check(and.Left, bindings);

                    if (!String.IsNullOrEmpty(error))
                    {
                        return error;
                    }

                    return Check(and.Right, bindings);
                }

                if (expression is Or or)
                {
                    string error = Check(or.Left, [.. boundVariables]);

                    if (!String.IsNullOrEmpty(error))
                    {
                        return error;
                    }

                    return Check(or.Right, [.. boundVariables]);
                }

                if (expression is Not not)
                {
                    return Check(not.Body, [.. boundVariables]);
                }

                return String.Empty;
            }

            return Check(expression.Simplify(), new());
        }

        private static string QuantifierCheck(JsonElement root)
        {
            if (root.TryGetProperty("quantifier", out JsonElement quantifier))
            {
                string[] validQuantifiers = new string[] { "forall", "exists", "none" };
                if(!validQuantifiers.Contains(quantifier.GetString()))
                {
                    return "The provided quantifier is invalid! ";
                }
                else
                {
                    if (quantifier.GetString() == "none" && root.GetRawText().Contains("?"))
                    {
                        return "You set the quantor to \"none\", yet there is a variable in the formula! " +
                        "Remember there are no free variables. ";
                    }
                    else 
                    {
                        return String.Empty;
                    }
                }
            }
            else
            {
                return "There was no quantifier element found! ";
            }
        }

        /// <summary>
        /// Recursively validates the structure and local constraints of an expression.
        /// </summary>
        private static string ValidateExpression(JsonElement expression, int layer, List<string> complexPredicates, bool evaluateSemantics)
        {
            string missing = "<missing>";

            if (!expression.TryGetProperty("type", out JsonElement typeElement))
            {
                return $"Expression at layer {layer} is missing property 'type'.";
            }
            if (typeElement.ValueKind != JsonValueKind.String)
            {
                return $"Expression at layer {layer} has property 'type' that is not a string.";
            }

            string? type = typeElement.GetString();
            if (type is null)
            {
                return $"Expression at layer {layer} has property 'type' that is null, it must be a string.";
            }

            switch (type)
            {
                case "comparison":
                    string operation = expression.TryGetProperty("operation", out JsonElement operationElement) ? operationElement.ToString() : missing;
                    string leftValue = expression.TryGetProperty("left", out JsonElement leftValueElement) ? leftValueElement.ToString() : missing;
                    string rightValue = expression.TryGetProperty("right", out JsonElement rightValueElement) ? rightValueElement.ToString() : missing;

                    if (operation == missing)
                    {
                        return $"Comparison with left '{leftValue}' and right '{rightValue}' at layer {layer} is missing property 'operation'.";
                    }
                    if (leftValue == missing)
                    {
                        return $"Comparison with operation '{operation}' and right '{rightValue}' at layer {layer} is missing property 'left'.";
                    }
                    if (rightValue == missing)
                    {
                        return $"Comparison with operation '{operation}' and left '{leftValue}' at layer {layer} is missing property 'right'.";
                    }

                    if(!(leftValue.Contains("?") || numberRegex.IsMatch(leftValue)))
                    {
                        return $"Comparison with operation '{operation}', " +
                        $"left '{leftValue}' and right '{rightValue}' " +
                        $"at layer {layer} has a invalid left value. " +
                        $"The value must be a variable or a number.";
                    }
                    if (!(rightValue.Contains("?") || numberRegex.IsMatch(rightValue)))
                    {
                        return $"Comparison with operation '{operation}', " +
                        $"left '{leftValue}' and right '{rightValue}' " +
                        $"at layer {layer} has a invalid right value. " +
                        $"The value must be a variable or a number.";
                    }

                    string[] comparisonOperations = 
                    { 
                        "greaterThan", 
                        "lessThan", 
                        "greaterThanOrEqual", 
                        "lessThanOrEqual", 
                        "equal", 
                        "notEqual" 
                    };
                    if (!comparisonOperations.Contains(operation))
                    {
                        string closest = FindClosestMatch(operation, comparisonOperations);
                        return $"Comparison with left '{leftValue}' and right '{rightValue}' at layer {layer} " +
                        $"has invalid operation '{operation}'. Did you mean '{closest}'?";
                    }

                    return String.Empty;

                case "predicate":
                    string source = expression.TryGetProperty("source", out JsonElement sourceElement) ? sourceElement.ToString() : missing;
                    string target = expression.TryGetProperty("target", out JsonElement targetElement) ? targetElement.ToString() : missing;
                    string property = expression.TryGetProperty("property", out JsonElement propertyElement) ? propertyElement.ToString() : missing;

                    if (property == missing)
                    {
                        return $"Predicate with source '{source}' and target '{target}' at layer {layer} is missing property 'property'.";
                    }
                    if (source == missing)
                    {
                        return $"Predicate with property '{property}' and target '{target}' at layer {layer} is missing property 'source'.";
                    }
                    if (target == missing)
                    {
                        return $"Predicate with source '{source}' and property '{property}' at layer {layer} is missing property 'target'.";
                    }

                    if(evaluateSemantics)
                    {
                        if (!itemRegex.IsMatch(source))
                        {
                            return $"Invalid source '{source}' at layer {layer}. Use a resolution suggestion or a variable.";
                        }
                        if (!itemRegex.IsMatch(target))
                        {
                            return $"Invalid target '{target}' at layer {layer}. Use a resolution suggestion or a variable.";
                        }
                        if (!propertyRegex.IsMatch(property) && 
                        !complexPredicates.Any(x => x.Contains(property)))
                        {
                            return $"Invalid property '{property}' at layer {layer}. Use a resolution suggestion.";
                        }
                    }

                    return String.Empty;

                case "not":
                    if (!expression.TryGetProperty("body", out JsonElement body))
                    {
                        return $"'Not' at layer {layer} is missing property 'body'.";
                    }
                    else
                    {
                        return ValidateExpression(body, layer + 1, complexPredicates, evaluateSemantics);
                    }
                case "and":
                case "or":
                case "imp":
                case "eq":
                case "xor":
                case "nand":
                case "nor":
                    string left = expression.TryGetProperty("left", out JsonElement leftElement) ? leftElement.ToString() : missing;
                    string right = expression.TryGetProperty("right", out JsonElement rightElement) ? rightElement.ToString() : missing;

                    if (left == missing)
                    {
                        return $"Binary operator '{type}' at layer {layer} with right element '{right}' is missing a left element.";
                    }
                    if (right == missing)
                    {
                        return $"Binary operator '{type}' at layer {layer} with right element '{left}' is missing a right element.";
                    }

                    string leftResult = ValidateExpression(leftElement, layer + 1, complexPredicates, evaluateSemantics);
                    if (leftResult != String.Empty)
                    {
                        return leftResult;
                    }
                    else
                    {
                        return ValidateExpression(rightElement, layer + 1, complexPredicates, evaluateSemantics);
                    }

                default:
                    return $"Unknown expression type '{type}' at layer {layer}.";
            }
        }

        private static string FindClosestMatch(string input, string[] options)
        {
            string closest = options[0];
            int highScore = int.MinValue;

            foreach (string option in options)
            {
                int score = input.Count(c => option.Contains(c));

                if (score > highScore)
                {
                    closest = option;
                    highScore = score;
                }
            }

            return closest;
        }
    }
}
