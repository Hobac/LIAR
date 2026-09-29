using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LIAR_backend.Expressions
{
    public class Imp : BinaryExpression
    {
        public Imp(Expression left, Expression right) : base(left, right) { }

        public override Expression Simplify()
        {
            return new Or(new Not(left.Simplify()).Simplify(), right.Simplify());
        }

        public override string Print()
        {
            return "(" + left.Print() + " -> " + right.Print() + ")";
        }
    }
}
