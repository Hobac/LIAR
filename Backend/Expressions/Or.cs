using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LIAR_backend.Expressions
{
    public class Or : BinaryExpression
    {
        public Or(Expression left, Expression right) : base(left, right) { }

        public override Expression Simplify()
        {
            return new Or(left.Simplify(), right.Simplify());
        }

        public override string Query()
        {
            return "{ " + left.Query() + " } \n UNION \n { " + right.Query() + " }";
        }

        public override string Print()
        {
            return "(" + left.Print() + " OR " + right.Print() + ")";
        }
    }
}
