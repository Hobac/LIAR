using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LIAR_backend.Expressions
{
    public class Predicate : Expression
    {
        private string property;
        private string source;
        private string target;

        public string Property => property;
        public string Source => source;
        public string Target => target;

        public Predicate(string property, string source, string target)
        {
            this.property = property;
            this.source = source;
            this.target = target;
        }

        public override string Query()
        {
            string resolvedProperty = Regex.IsMatch(property, @"^P[0-9]+$")
                ? $"wdt:{property}"
                : ExpandBoundedPath(property);

            string resolvedSource = source.StartsWith("?") ? source : $"wd:{source}";
            string resolvedTarget = target.StartsWith("?") ? target : $"wd:{target}";

            return $"{resolvedSource} {resolvedProperty} {resolvedTarget} .";
        }

        private static string ExpandBoundedPath(string property)
        {
            Match match = Regex.Match(property, @"^(?:(.*?)/)?\{(\d+),(\d+)\}(.+)$");

            if (!match.Success)
            {
                return property;
            }

            string prefix = match.Groups[1].Success ? match.Groups[1].Value : "";

            int min = int.Parse(match.Groups[2].Value);
            int max = int.Parse(match.Groups[3].Value);
            string path = match.Groups[4].Value;

            List<string> alternatives = new();

            for (int i = min; i <= max; i++)
            {
                string repeated = string.Join("/", Enumerable.Repeat(path, i));

                if (!string.IsNullOrEmpty(prefix))
                {
                    repeated = $"{prefix}/{repeated}";
                }

                alternatives.Add(repeated);
            }

            return $"({string.Join("|", alternatives)})";
        }

        public override string Print()
        {
            return "[" + source + "-" + property + "-" + target + "]";
        }

        public override HashSet<string> GetVariables()
        {
            HashSet<string> variables = new();

            if (source.StartsWith("?"))
                variables.Add(source);

            if (target.StartsWith("?"))
                variables.Add(target);

            return variables;
        }

        public bool ConstantOnly()
        {
            return !Print().Contains("?");
        }
    }
}
