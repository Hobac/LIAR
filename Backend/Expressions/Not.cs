using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LIAR_backend.Expressions
{
    public class Not : UnaryExpression
    {
        public Not(Expression body) : base(body) { }

        public override Expression Simplify()
        {
            // double negation can be removed (NOT NOT P = P), but must never be introduced (P → NOT NOT P)
            // introducing it could turn missing Wikidata information into false counter-evidence
            // because NOT is evaluated using FILTER NOT EXISTS
            Expression simplifiedBody = body.Simplify();

            return simplifiedBody switch
            {
                // NOT NOT P = P
                Not not => not.Body,

                // negate comparisons
                GreaterThan x => new LessThanOrEqual(x.Left, x.Right),
                LessThan x => new GreaterThanOrEqual(x.Left, x.Right),
                GreaterThanOrEqual x => new LessThan(x.Left, x.Right),
                LessThanOrEqual x => new GreaterThan(x.Left, x.Right),
                Equal x => new NotEqual(x.Left, x.Right),
                NotEqual x => new Equal(x.Left, x.Right),

                _ => new Not(simplifiedBody)
            };
        }

        public override string Query()
        {
            return "FILTER NOT EXISTS { " + body.Query() + " }";
        }

        public override string Print()
        {
            return "NOT " + body.Print();
        }
    }
}
