using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LIAR_backend.Expressions
{
    public class And : BinaryExpression
    {
        public And(Expression left, Expression right) : base(left, right) { }

        public override Expression Simplify()
        {
            return new And(left.Simplify(), right.Simplify());
        }

        public override string Query()
        {
            return left.Query() + "\n" + right.Query();
        }

        public override string Print()
        {
            return "(" + left.Print() + " AND " + right.Print() + ")";
        }
    }
}
