using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace LIAR_backend
{
    public static class Info
    {
        public static void Write(string text)
        {
            Console.WriteLine(text);
        }

        public static void WriteUpdate(List<Statement> statements)
        {
            if(statements.Count > 0)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine("UPDATE");
                Console.ForegroundColor = ConsoleColor.White;

                foreach (Statement statement in statements)
                {
                    Console.WriteLine($"Text:           {statement.Text}");
                    Console.WriteLine($"Clean text:     {statement.DisambiguatedText ?? "-"}");
                    Console.WriteLine($"Truth value:    {statement.TruthValue}");
                    Console.WriteLine($"Formula:        {statement.Formula}" ?? "-");
                    Console.WriteLine($"Nat. formula:   {statement.NaturalFormula}" ?? "-");
                    Console.WriteLine($"Query:          {statement.Query ?? "-"}");
                    Console.WriteLine($"Proof:          {statement.Proof ?? "-"}");


                    Console.WriteLine();
                }
            }
        }

        public static void WriteStart(string name)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine();
            Console.WriteLine(name + "...");
            Console.ForegroundColor = ConsoleColor.White;
        }

        public static void WriteStop()
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("DONE");
            Console.ForegroundColor = ConsoleColor.White;
        }

        public static void PrintColoredText(string input, List<Statement> statements)
        {
            Console.WriteLine();
            Console.WriteLine("Verified Text: (red = false, green = true, yellow = unknown)");
            int position = 0;

            while (position < input.Length)
            {
                Statement? match = null;

                foreach (Statement statement in statements)
                {
                    if (position + statement.Text.Length <= input.Length &&
                    input.Substring(position, statement.Text.Length) == statement.Text)
                    {
                        match = statement;
                        break;
                    }
                }

                if (match == null)
                {
                    Console.ResetColor();
                    Console.Write(input[position]);
                    position++;
                    continue;
                }

                Console.ForegroundColor = match.TruthValue switch
                {
                    TruthValue.True => ConsoleColor.Green,
                    TruthValue.False => ConsoleColor.Red,
                    TruthValue.Unknown => ConsoleColor.Yellow,
                    _ => ConsoleColor.White
                };

                Console.Write(match.Text);
                Console.ResetColor();

                position += match.Text.Length;
            }

            Console.ResetColor();
            Console.WriteLine();
        }
    }
}
