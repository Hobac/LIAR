using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LIAR_backend.Expressions
{
    public class Nor : BinaryExpression
    {
        public Nor(Expression left, Expression right) : base(left, right) { }

        public override Expression Simplify()
        {
            return new Not(new Or(left.Simplify(), right.Simplify()));
        }

        public override string Print()
        {
            return "(" + left.Print() + " NOR " + right.Print() + ")";
        }
    }
}
