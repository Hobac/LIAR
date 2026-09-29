using LIAR_backend.Expressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LIAR_backend
{
    public static class QueryCompiler
    {
        // all vars are implicitly existentially quantified

        // checking if a triple is in the DB is much easier then checking if a triple is not there
        // thus we allways ask the positive question (exists -> simple query, forall -> counter example)
        // if a formula is purely negative like ¬P(x) we would have to check the entire DB to make sure such a triple
        // does not exits, thus we optimze via DNF and try to confiorm teh formula is true with some conjunction
        // if only purely negative triples are left we say "Unknow" since we cant scan everything and also the DB is
        // not complete anyway thus scanning it wouldnt yield any certanty anyway

        public const string UNSAT = "UNSAT";

        public static string Process(Expression expression)
        {
            Expression simple = expression.Simplify();
            Expression nnf = Normalizer.ToNNF(simple);
            Expression dnf = Normalizer.ToDNF(nnf);

            if (Unsat(expression))
            {
                return UNSAT;
            }

            List<List<Expression>> conjunctions = GetConjunctions(dnf);
            conjunctions = Optimize(conjunctions);
            conjunctions = AddFallbackBindings(conjunctions);

            Expression final = BuildExpression(conjunctions);

            if (HasVariable(final))
            {
                return "SELECT * WHERE { " + final.Query() + " } LIMIT 1";
            }
            else
            {
                return "ASK { " + final.Query() + " }";
            }
        }

        private static bool Unsat(Expression expression)
        {
            // TODO: Check if formula is sat at all
            return false;
        }

        private static List<List<Expression>> AddFallbackBindings(List<List<Expression>> conjunctions)
        {
            foreach (List<Expression> conjunction in conjunctions)
            {
                // Only conjunctions consisting entirely of negative literals
                if (!conjunction.All(e => e is Not))
                {
                    continue;
                }

                HashSet<string> variables = new();

                // Collect all variables inside the negative literals
                foreach (Expression expression in conjunction)
                {
                    if (expression is Not not && not.Body is Predicate predicate)
                    {
                        if (predicate.Source.StartsWith("?"))
                        {
                            variables.Add(predicate.Source);
                        }

                        if (predicate.Target.StartsWith("?"))
                        {
                            variables.Add(predicate.Target);
                        }
                    }
                }

                // Add a generic Wikidata domain binder for every variable
                foreach (string variable in variables)
                {
                    conjunction.Insert(
                        0,
                        new Predicate("P31", variable, variable + "Type")
                    );
                }
            }

            return conjunctions;
        }

        private static List<List<Expression>> Optimize(List<List<Expression>> conjunctions)
        {
            // Remove duplicate literals inside each conjunction
            conjunctions = conjunctions
                .Select(c => c.DistinctBy(e => e.Print()).ToList())
                .ToList();

            // Remove unsatisfiable conjunctions: P ∧ ¬P
            conjunctions = conjunctions
                .Where(c => !c.Any(e =>
                    e is Not not &&
                    c.Any(other => other == not.Body)))
                .ToList();

            // Remove duplicate conjunctions
            conjunctions = conjunctions
                .DistinctBy(c => string.Join("|", c.Select(e => e.Print()).OrderBy(x => x)))
                .ToList();

            return conjunctions;
        }

        private static Expression BuildExpression(List<List<Expression>> conjunctions)
        {
            if (conjunctions.Count == 0)
            {
                throw new ArgumentException("No conjunctions.");
            }

            Expression BuildConjunction(List<Expression> literals)
            {
                if (literals.Count == 0)
                {
                    throw new ArgumentException("Empty conjunction.");
                }

                Expression result = literals[0];

                for (int i = 1; i < literals.Count; i++)
                {
                    result = new And(result, literals[i]);
                }

                return result;
            }

            Expression expression = BuildConjunction(conjunctions[0]);

            for (int i = 1; i < conjunctions.Count; i++)
            {
                expression = new Or(
                    expression,
                    BuildConjunction(conjunctions[i])
                );
            }

            return expression;
        }

        private static List<List<Expression>> GetConjunctions(Expression expression)
        {
            List<List<Expression>> conjunctions = new();

            void CollectDisjunctions(Expression current)
            {
                // A ∨ B -> separate conjunctions
                if (current is Or or)
                {
                    CollectDisjunctions(or.Left);
                    CollectDisjunctions(or.Right);
                }
                else
                {
                    List<Expression> literals = new();
                    CollectLiterals(current, literals);

                    // Positive literals first, negative literals last
                    literals = literals.OrderBy(l => l is Not).ToList();

                    conjunctions.Add(literals);
                }
            }

            void CollectLiterals(Expression current, List<Expression> literals)
            {
                // A ∧ B -> literals of the same conjunction
                if (current is And and)
                {
                    CollectLiterals(and.Left, literals);
                    CollectLiterals(and.Right, literals);
                }
                else
                {
                    literals.Add(current);
                }
            }

            CollectDisjunctions(expression);

            // Conjunctions with fewer negative literals first
            return conjunctions.OrderBy(c => c.Count(l => l is Not)).ToList();
        }

        private static bool HasVariable(Expression expression)
        {
            if (expression is Predicate p)
                return p.Source.StartsWith("?") || p.Target.StartsWith("?");

            if (expression is UnaryExpression u)
                return HasVariable(u.Body);

            if (expression is BinaryExpression b)
                return HasVariable(b.Left) || HasVariable(b.Right);

            return false;
        }
    }
}
