using Dumpify;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.DotNet.Interactive;
using Microsoft.PowerFx;
using Microsoft.PowerFx.Types;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text.RegularExpressions;

namespace PowerFxDotnetInteractive
{
    public class PowerFxExpression(RecalcEngine engine, string expression, KernelInvocationContext context)
    {
        private readonly string[] _assignments = ["Set", "Collect", "ClearCollect"];
        public static List<string> _identifiers = [];
        public static List<string> Identifiers => _identifiers;

        void EvaluateSingleExpression(string originalExpression, string expression, ReadOnlySymbolValues symbolValues = null)
        {
            try
            {
                Match match = _assignments.Select(assignmentFunction => Regex.Match(expression, @$"^\s*{assignmentFunction}\(\s*(?<ident>\w+)\s*,\s*(?<expr>.*)\)\s*$")).FirstOrDefault(x => x.Success);

                // variable assignment: Set or Collect or ClearCollect( <ident>, <expr> )
                if (match != null)
                {
                    var result = symbolValues == null ? engine.Eval(match.Groups["expr"].Value) : engine.EvalAsync(match.Groups["expr"].Value, default, symbolValues).Result;
                    engine.UpdateVariable(match.Groups["ident"].Value, result);
                    if (!_identifiers.Contains(match.Groups["ident"].Value))
                        _identifiers.Add(match.Groups["ident"].Value);
                    DisplayResult(context, result);
                }

                // formula definition: <ident> = <formula>
                else if ((match = Regex.Match(expression, @"^\s*(?<ident>\w+)\s*=(?<formula>.*)$")).Success)
                {
                    engine.SetFormula(match.Groups["ident"].Value, match.Groups["formula"].Value, (string name, FormulaValue result)=> 
                    {
                        DisplayResult(context, result);
                    });
                    if (!_identifiers.Contains(match.Groups["ident"].Value))
                        _identifiers.Add(match.Groups["ident"].Value);
                }

                // eval and print everything else, unless empty lines and single line comment (which do nothing)
                else if (!Regex.IsMatch(expression, @"^\s*//") && Regex.IsMatch(expression, @"\w"))
                {
                    var result = symbolValues == null ? engine.Eval(expression) : engine.EvalAsync(expression, default, symbolValues).Result;
                    DisplayResult(context, result);
                }
            }
            catch(InvalidOperationException ex)
            {
                context.DisplayStandardError(ex.Message);
            }
        }

        private static void DisplayResult(KernelInvocationContext context, FormulaValue result)
        {
            var entityObject = result.ToObject();
            switch (entityObject)
            {
                case IEnumerable<object> e:
                    if (!e.Any()) break;

                    if (e.First() is ExpandoObject)
                    {
                        context.Display(e, "application/json");
                    }
                    else
                    {
                        context.DisplayAs(e.DumpText(), "text/plain");
                    }
                    break;
                default:
                    context.DisplayAs(entityObject.DumpText(), "text/plain");
                    break;
            }
        }

        public PowerFxExpression Evaluate(ReadOnlySymbolValues symbolValues = null)
        {
            string[] expressions = null;
            if (expression.Contains(';'))
            {
                expressions = expression.Split($";\n");
            }
            else
                expressions = expression.Split("\n");
            if (expressions.Length == 1)
                expressions = expression.Split($";\r\n");
            foreach (var expression in expressions)
            {
                var cleanedExpression = string.Join("",expression.Trim().Split("\n")).TrimEnd(';');
                EvaluateSingleExpression(expression, cleanedExpression, symbolValues);
            }
            return this;
        }

        static string UpdateResult(object value)
        {
            string resultString = "";

            switch (value)
            {
                case RecordValue record:
                    {
                        resultString = $@"{{{string.Join(",", record.Fields.Select(field => $@"""{field.Name}"": {UpdateResult(field.Value)}"))}}}".FormatJson();
                        break;
                    }

                case TableValue table:
                    {
                        int valueSeen = 0, recordsSeen = 0;

                        foreach (var row in table.Rows)
                        {
                            recordsSeen++;

                            if (row.Value is not RecordValue scanRecord) continue;
                            var cells = scanRecord.Fields.Where(field => field.Name == "Value").Select(field => UpdateResult(field.Value));
                            resultString += string.Join(", ", cells);
                        }

                        // check if the table can be represented in simpler [ ] notation,
                        //   where each element is a record with a field named Value.
                        if (valueSeen == recordsSeen)
                            return $"[{resultString}]";
                        else
                        {
                            // no, table is more complex that a single column of Value fields,
                            //   requires full treatment
                            var rows = string.Join(", ", table.Rows.Select(row => UpdateResult(row.Value)));
                            var formattedRows = $"[{rows}]".FormatJson();
                            resultString = formattedRows;
                        }

                        break;
                    }

                case ErrorValue errorValue:
                    resultString = $@"{{""Error"": ""{errorValue.Errors[0].Message}""}}";
                    break;
                case StringValue str:
                    resultString = $@"""{str.ToObject()}""";
                    break;
                case UntypedObjectValue uov:
                    resultString = $@"""{uov}""";
                    break;
                case FormulaValue fv:
                    resultString = fv.ToObject()?.ToString();
                    break;
                default:
                    throw new Exception("unexpected type in PrintResult");
            }

            return (resultString);
        }
    }
}