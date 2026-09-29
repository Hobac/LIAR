using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LIAR_backend.Expressions
{
    public abstract class BinaryExpression : Expression
    {
        protected Expression left;
        protected Expression right;

        public Expression Left => left;
        public Expression Right => right;

        public BinaryExpression(Expression left, Expression right)
        {
            this.left = left;
            this.right = right;
        }

        public override HashSet<string> GetVariables()
        {
            HashSet<string> variables = left.GetVariables();
            variables.UnionWith(right.GetVariables());
            return variables;
        }
    }
}
