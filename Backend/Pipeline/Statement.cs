using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LIAR_backend
{
    public enum TruthValue
    {
        Void,
        True,
        False,
        Unknown
    }

    public class Statement
    {
        public Statement(string text, string disambiguatedText)
        {
            Text = text;
            DisambiguatedText = disambiguatedText;
            CounterExample = false;
        }

        public string Text { get; set; }
        public string DisambiguatedText { get; set; }
        public string? NaturalFormula { get; set; }
        public string? Formula { get; set; }
        public bool CounterExample { get; set; }
        public bool ClosedWorldAssumption { get; set; }
        public string? Query { get; set; }
        public string? Proof { get; set; }
        public TruthValue TruthValue { get; set; }
    }
}
