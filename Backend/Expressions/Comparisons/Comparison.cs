using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LIAR_backend.Expressions
{
    public abstract class Comparison : Expression
    {
        protected string left;
        protected string right;

        public string Left => left;
        public string Right => right;

        public Comparison(string left, string right)
        {
            this.left = left;
            this.right = right;
        }
    }
}
