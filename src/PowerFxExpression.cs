using Microsoft.PowerFx;
using Microsoft.PowerFx.Core.Public.Values;
using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace PowerFxDotnetInteractive
{
    public class PowerFxExpression
    {
        private readonly string[] _assignments = new string[] { "Set", "Collect", "ClearCollect" };
        private readonly RecalcEngine _engine;
        private readonly string _expression;
        public HashSet<(string variable, string formula, string result)> Result { get; set; } = new HashSet<(string variable, string formula, string result)>();
        public static List<string> _identifiers = new List<string>();
        public List<string> Identifiers { get => _identifiers; }
        public HashSet<(string name, string result)> StateCheck { get; set; } = new HashSet<(string name, string result)>();
        public PowerFxExpression(RecalcEngine engine, string expression)
        {
            _engine = engine;
            _expression = expression ?? throw new ArgumentNullException(nameof(expression));
        }
        void EvaluateSingleExpression(string originalExpression, string expression)
        {
            try
            {
                Match match = _assignments.Select(assignmentFunction => Regex.Match(expression, @$"^\s*{assignmentFunction}\(\s*(?<ident>\w+)\s*,\s*(?<expr>.*)\)\s*$")).FirstOrDefault(x => x.Success);

                // variable assignment: Set or Collect or ClearCollect( <ident>, <expr> )
                if (match != null)
                {
                    var r = _engine.Eval(match.Groups["expr"].Value);
                    _engine.UpdateVariable(match.Groups["ident"].Value, r);
                    if(!_identifiers.Contains(match.Groups["ident"].Value))
                        _identifiers.Add(match.Groups["ident"].Value);
                    Result.Add((match.Groups["ident"].Value, originalExpression, UpdateResult(r)));
                }

                // formula definition: <ident> = <formula>
                else if ((match = Regex.Match(expression, @"^\s*(?<ident>\w+)\s*=(?<formula>.*)$")).Success)
                {
                    _engine.SetFormula(match.Groups["ident"].Value, match.Groups["formula"].Value, (string name, FormulaValue newValue)=> 
                    {
                        SetExpressionResult(originalExpression, newValue);
                    });
                    if (!_identifiers.Contains(match.Groups["ident"].Value))
                        _identifiers.Add(match.Groups["ident"].Value);
                    Result.Add((match.Groups["ident"].Value, expression, match.Groups["formula"].Value));
                }

                // eval and print everything else, unless empty lines and single line comment (which do nothing)
                else if (!Regex.IsMatch(expression, @"^\s*//") && Regex.IsMatch(expression, @"\w"))
                {
                    var result = _engine.Eval(expression);

                    SetExpressionResult(originalExpression, result);
                }
            }
            catch(InvalidOperationException ex)
            {
                Result.Add(("",originalExpression, ex.Message));
            }
        }

        private void SetExpressionResult(string expression, FormulaValue result)
        {
            if (result is ErrorValue errorValue)
                Result.Add(("", expression, @$"{{""Error"": ""{errorValue.Errors[0].Message}""}}"));
            else
                Result.Add(("", expression, UpdateResult(result)));
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
            StateCheck = _identifiers.Select(x => (name: x, currentValue: UpdateResult(_engine.GetValue(x)))).ToHashSet();
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