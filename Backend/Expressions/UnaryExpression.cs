using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LIAR_backend.Expressions
{
    public abstract class UnaryExpression : Expression
    {
        protected Expression body;
        public Expression Body => body;

        public UnaryExpression(Expression body)
        {
            this.body = body;
        }

        public override HashSet<string> GetVariables()
        {
            return body.GetVariables();
        }
    }
}
