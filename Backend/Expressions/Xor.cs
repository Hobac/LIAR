using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LIAR_backend.Expressions
{
    public class Xor : BinaryExpression
    {
        public Xor(Expression left, Expression right) : base(left, right) { }

        public override Expression Simplify()
        {
            return new Or(
                new And(left.Simplify(), new Not(right.Simplify())),
                new And(new Not(left.Simplify()), right.Simplify())
            );
        }

        public override string Print()
        {
            return "(" + left.Print() + " XOR " + right.Print() + ")";
        }
    }
}
