using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LIAR_backend.Expressions
{
    public class Equal : Comparison
    {
        public Equal(string left, string right) : base(left, right) { }
        public override string Query() => $"FILTER({left} = {right})";
        public override string Print() => "(" + left + " = " + right + ")";
    }
}
