using LIAR_backend.Expressions;
using System.Text.Json;

namespace LIAR_backend
{
    public static class ExpressionTranslator
    {
        public static Expression FromJson(string json)
        {
            JsonDocument document = JsonDocument.Parse(json);
            return FromJson(document.RootElement);
        }

        public static Expression FromJson(JsonElement element)
        {
            string type = element.GetProperty("type").ToString();

            return type switch
            {
                "predicate" => new Predicate(
                    element.GetProperty("property").ToString(),
                    element.GetProperty("source").ToString(),
                    element.GetProperty("target").ToString()
                ),

                "comparison" => FromComparisonJson(element),

                "not" => new Not(
                    FromJson(element.GetProperty("body"))
                ),

                "and" => new And(
                    FromJson(element.GetProperty("left")),
                    FromJson(element.GetProperty("right"))
                ),

                "or" => new Or(
                    FromJson(element.GetProperty("left")),
                    FromJson(element.GetProperty("right"))
                ),

                "imp" => new Imp(
                    FromJson(element.GetProperty("left")),
                    FromJson(element.GetProperty("right"))
                ),

                "eq" => new Eq(
                    FromJson(element.GetProperty("left")),
                    FromJson(element.GetProperty("right"))
                ),

                "xor" => new Xor(
                    FromJson(element.GetProperty("left")),
                    FromJson(element.GetProperty("right"))
                ),

                "nand" => new Nand(
                    FromJson(element.GetProperty("left")),
                    FromJson(element.GetProperty("right"))
                ),

                "nor" => new Nor(
                    FromJson(element.GetProperty("left")),
                    FromJson(element.GetProperty("right"))
                ),

                _ => throw new ArgumentException($"Unknown expression type '{type}'.")
            };
        }
        private static Expression FromComparisonJson(JsonElement element)
        {
            string operation = element.GetProperty("operation").ToString();
            string left = element.GetProperty("left").ToString();
            string right = element.GetProperty("right").ToString();

            return operation switch
            {
                "greaterThan" => new GreaterThan(left, right),
                "lessThan" => new LessThan(left, right),
                "greaterThanOrEqual" => new GreaterThanOrEqual(left, right),
                "lessThanOrEqual" => new LessThanOrEqual(left, right),
                "equal" => new Equal(left, right),
                "notEqual" => new NotEqual(left, right),

                _ => throw new ArgumentException($"Unknown comparison operation '{operation}'.")
            };
        }
    }
}
