using Microsoft.PowerFx;
using Microsoft.PowerFx.Core.Public.Values;
using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace PowerFxDotnetInteractive
{
    public class PowerFxExpression
    {
        private readonly RecalcEngine _engine;
        private readonly string _expression;
        public HashSet<(string formula, string result)> Result { get; set; } = new HashSet<(string formula, string result)>();

        public PowerFxExpression(RecalcEngine engine, string expression)
        {
            _engine = engine;
            _expression = expression ?? throw new ArgumentNullException(nameof(expression));
        }
        void EvaluateSingleExpression(string originalExpression, string expression)
        {
            try
            {
                Match match;

                // variable assignment: Set( <ident>, <expr> )
                if ((match = Regex.Match(expression, @"^\s*Set\(\s*(?<ident>\w+)\s*,\s*(?<expr>.*)\)\s*$")).Success)
                {
                    var r = _engine.Eval(match.Groups["expr"].Value);
                    _engine.UpdateVariable(match.Groups["ident"].Value, r);
                    Result.Add((originalExpression, match.Groups["ident"].Value + ": " + PrintResult(r)));
                }

                // formula definition: <ident> = <formula>
                else if ((match = Regex.Match(expression, @"^\s*(?<ident>\w+)\s*=(?<formula>.*)$")).Success)
                {
                    _engine.SetFormula(match.Groups["ident"].Value, match.Groups["formula"].Value, OnUpdate);
                    Result.Add((expression, match.Groups["ident"].Value + " = " + match.Groups["formula"]));
                }

                // eval and print everything else, unless empty lines and single line comment (which do nothing)
                else if (!Regex.IsMatch(expression, @"^\s*//") && Regex.IsMatch(expression, @"\w"))
                {
                    var result = _engine.Eval(expression);

                    if (result is ErrorValue errorValue)
                        Result.Add((originalExpression, "Error: " + errorValue.Errors[0].Message));
                    else
                        Result.Add((originalExpression, PrintResult(result)));
                }
            }
            catch(InvalidOperationException ex)
            {
                Result.Add((originalExpression, ex.Message));
            }
        }

        public PowerFxExpression Evaluate()
        {
            string[] expressions = null;
            if (_expression.Contains(";"))
            {
                expressions = _expression.Split($";\n");
            }
            else
                expressions = _expression.Split("\n");
            if (expressions.Length == 1)
                expressions = _expression.Split($";\r\n");
            foreach (var expression in expressions)
            {
                var cleanedExpression = string.Join("",expression.Trim().Split("\n")).TrimEnd(';');
                EvaluateSingleExpression(expression, cleanedExpression);
            }

            return this;
        }
        static void OnUpdate(string name, FormulaValue newValue)
        {
            Console.Write($"{name}: ");
            if (newValue is ErrorValue errorValue)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error: " + errorValue.Errors[0].Message);
                Console.ResetColor();
            }
            else
                Console.WriteLine(PrintResult(newValue));
        }

        static string PrintResult(object value)
        {
            string resultString = "";

            switch (value)
            {
                case RecordValue record:
                    {
                        var separator = "";
                        resultString = "{ ";
                        foreach (var field in record.Fields)
                        {
                            resultString += separator + $"\"{field.Name}\": ";
                            resultString += PrintResult(field.Value);
                            separator = ", ";
                        }
                        resultString += " }";
                        break;
                    }

                case TableValue table:
                    {
                        int valueSeen = 0, recordsSeen = 0;
                        string separator = "";

                        // check if the table can be represented in simpler [ ] notation,
                        //   where each element is a record with a field named Value.
                        foreach (var row in table.Rows)
                        {
                            recordsSeen++;
                            if (row.Value is RecordValue scanRecord)
                            {
                                foreach (var field in scanRecord.Fields)
                                    if (field.Name == "Value")
                                    {
                                        valueSeen++;
                                        resultString += separator + PrintResult(field.Value);
                                        separator = ", ";
                                    }
                                    else
                                        valueSeen = 0;
                            }
                            else
                                valueSeen = 0;
                        }

                        if (valueSeen == recordsSeen)
                            return "[ " + resultString + " ]";
                        else
                        {
                            // no, table is more complex that a single column of Value fields,
                            //   requires full treatment
                            resultString = "Table(";
                            separator = "";
                            foreach (var row in table.Rows)
                            {
                                resultString += separator + PrintResult(row.Value);
                                separator = ", ";
                            }
                            resultString += ")";
                        }

                        break;
                    }

                case ErrorValue errorValue:
                    resultString = "<Error: " + errorValue.Errors[0].Message + ">";
                    break;
                case StringValue str:
                    resultString = "\"" + str.ToObject().ToString().Replace("\"", "\"\"") + "\"";
                    break;
                case FormulaValue fv:
                    resultString = fv.ToObject().ToString();
                    break;
                default:
                    throw new Exception("unexpected type in PrintResult");
            }

            return (resultString);
        }
    }
}