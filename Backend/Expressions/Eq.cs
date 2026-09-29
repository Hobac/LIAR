using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LIAR_backend.Expressions
{
    public class Eq : BinaryExpression
    {
        public Eq(Expression left, Expression right) : base(left, right) { }

        public override Expression Simplify()
        {
            return new And(
                new Imp(left.Simplify(), right.Simplify()).Simplify(),
                new Imp(right.Simplify(), left.Simplify()).Simplify()
            );
        }

        public override string Print()
        {
            return "(" + left.Print() + " <-> " + right.Print() + ")";
        }
    }
}
