using LIAR_backend.Expressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LIAR_backend
{
    public static class Normalizer
    {
        public static Expression ToNNF(Expression expression)
        {
            if (expression is And and)
            {
                return new And(ToNNF(and.Left), ToNNF(and.Right));
            }
            if (expression is Or or)
            {
                return new Or(ToNNF(or.Left), ToNNF(or.Right));
            }
            if (expression is Not not)
            {
                Expression body = not.Body;

                // ¬¬A -> A
                if (body is Not innerNot)
                {
                    return ToNNF(innerNot.Body);
                }

                // ¬(A ∧ B) -> ¬A ∨ ¬B
                if (body is And andBody)
                {
                    return new Or(
                        ToNNF(new Not(andBody.Left)),
                        ToNNF(new Not(andBody.Right))
                    );
                }

                // ¬(A ∨ B) -> ¬A ∧ ¬B
                if (body is Or orBody)
                {
                    return new And(
                        ToNNF(new Not(orBody.Left)),
                        ToNNF(new Not(orBody.Right))
                    );
                }

                // already in NNF
                return new Not(ToNNF(body));
            }

            return expression;
        }

        public static Expression ToDNF(Expression expression)
        {
            if (expression is Or or)
            {
                return new Or(ToDNF(or.Left), ToDNF(or.Right));
            }

            if (expression is And and)
            {
                Expression left = ToDNF(and.Left);
                Expression right = ToDNF(and.Right);

                // (A ∨ B) ∧ C -> (A ∧ C) ∨ (B ∧ C)
                if (left is Or leftOr)
                {
                    return ToDNF(
                        new Or(
                            new And(leftOr.Left, right),
                            new And(leftOr.Right, right)
                        )
                    );
                }

                // A ∧ (B ∨ C) -> (A ∧ B) ∨ (A ∧ C)
                if (right is Or rightOr)
                {
                    return ToDNF(
                        new Or(
                            new And(left, rightOr.Left),
                            new And(left, rightOr.Right)
                        )
                    );
                }

                // already a conjunction of DNF expressions
                return new And(left, right);
            }

            // literal -> already in DNF
            return expression;
        }

        /// <summary>
        /// Pulls an existing negation to the outside of the expression.
        /// A new negation must not be introduced because missing Wikidata facts represent unknown information,
        /// so introducing NOT NOT could incorrectly turn missing information into evidence for false.
        /// Returns null if no existing negation can be pulled outward.
        /// Example: "Einstein was an astronaut" is unknown because Wikidata contains no such fact.
        /// Rewriting it as "NOT (NOT (Einstein was an astronaut))" would allow the inner NOT to succeed
        /// merely because the fact is missing, incorrectly making the original statement false instead of unknown.
        /// </summary>
        public static Not? ToOuterNot(Expression expression)
        {
            expression = expression.Simplify();

            if (expression is Not not)
            {
                return not;
            }

            if (expression is And and)
            {
                Not? leftNot = ToOuterNot(and.Left);
                Not? rightNot = ToOuterNot(and.Right);

                if (leftNot is not null && rightNot is not null)
                {
                    // NOT A AND NOT B = NOT (A OR B)
                    return new Not(new Or(leftNot.Body, rightNot.Body));
                }
            }

            if (expression is Or or)
            {
                Not? leftNot = ToOuterNot(or.Left);
                Not? rightNot = ToOuterNot(or.Right);

                if (leftNot is not null && rightNot is not null)
                {
                    // NOT A OR NOT B = NOT (A AND B)
                    return new Not(new And(leftNot.Body, rightNot.Body));
                }
            }

            return null;
        }

        public static bool ContainsNot(Expression expression)
        {
            if (expression is Not)
            {
                return true;
            }

            if (expression is BinaryExpression binary)
            {
                return ContainsNot(binary.Left) || ContainsNot(binary.Right);
            }

            return false;
        }

        public static bool ContainsComparison(Expression expression)
        {
            if (expression is Comparison)
            {
                return true;
            }
            if (expression is UnaryExpression unary)
            {
                return ContainsComparison(unary.Body);
            }
            if (expression is BinaryExpression binary)
            {
                return ContainsComparison(binary.Left) || ContainsComparison(binary.Right);
            }

            return false;
        }
    }
}
