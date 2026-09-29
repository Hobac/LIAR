using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LIAR_backend.Expressions
{
    public abstract class Expression
    {
        public virtual string Query()
        {
            throw new Exception("Can not resolve this expression to SPARQL!");
        }

        public virtual Expression Simplify()
        {
            return this;
        }

        public virtual string Print()
        {
            throw new Exception("Can not print this expression: " + this.ToString());
        }

        public virtual HashSet<string> GetVariables()
        {
            return new();
        }

        public static bool operator == (Expression? left, Expression? right)
        {
            if (left is null)
                return false;

            return left.Equals(right);
        }

        public static bool operator != (Expression? left, Expression? right)
        {
            return !(left == right);
        }

        public override bool Equals(object? obj)
        {
            if (obj is null)
                return false;

            if (obj is not Expression other)
                return false;

            return Print() == other.Print();
        }

        public override int GetHashCode()
        {
            return Print().GetHashCode();
        }
    }
}
