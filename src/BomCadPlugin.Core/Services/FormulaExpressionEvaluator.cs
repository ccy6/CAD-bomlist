using System.Globalization;

namespace BomCadPlugin.Core.Services;

public sealed class FormulaExpressionEvaluator
{
    private string _expression = "";
    private int _position;
    private IReadOnlyDictionary<string, decimal> _variables = new Dictionary<string, decimal>();

    public decimal Evaluate(string expression, IReadOnlyDictionary<string, decimal> variables)
    {
        _expression = expression ?? "";
        _position = 0;
        _variables = variables;

        var value = ParseExpression();
        SkipWhiteSpace();
        if (_position != _expression.Length)
        {
            throw new FormatException($"无法识别公式内容：{_expression[_position]}");
        }

        return value;
    }

    private decimal ParseExpression()
    {
        var value = ParseTerm();
        while (true)
        {
            SkipWhiteSpace();
            if (Match('+'))
            {
                value += ParseTerm();
            }
            else if (Match('-'))
            {
                value -= ParseTerm();
            }
            else
            {
                return value;
            }
        }
    }

    private decimal ParseTerm()
    {
        var value = ParseFactor();
        while (true)
        {
            SkipWhiteSpace();
            if (Match('*'))
            {
                value *= ParseFactor();
            }
            else if (Match('/'))
            {
                var divisor = ParseFactor();
                if (divisor == 0)
                {
                    throw new DivideByZeroException("公式中出现除以 0。");
                }

                value /= divisor;
            }
            else
            {
                return value;
            }
        }
    }

    private decimal ParseFactor()
    {
        SkipWhiteSpace();
        if (Match('+')) return ParseFactor();
        if (Match('-')) return -ParseFactor();

        if (Match('('))
        {
            var value = ParseExpression();
            Expect(')');
            return value;
        }

        if (IsIdentifierStart(Current))
        {
            var identifier = ParseIdentifier();
            SkipWhiteSpace();
            if (Match('('))
            {
                var args = ParseArguments();
                return EvaluateFunction(identifier, args);
            }

            if (_variables.TryGetValue(identifier, out var variableValue))
            {
                return variableValue;
            }

            throw new FormatException($"参数未定义或未赋值：{identifier}");
        }

        return ParseNumber();
    }

    private List<decimal> ParseArguments()
    {
        var args = new List<decimal>();
        SkipWhiteSpace();
        if (Match(')'))
        {
            return args;
        }

        while (true)
        {
            args.Add(ParseExpression());
            SkipWhiteSpace();
            if (Match(')'))
            {
                return args;
            }

            Expect(',');
        }
    }

    private decimal ParseNumber()
    {
        SkipWhiteSpace();
        var start = _position;
        while (char.IsDigit(Current) || Current == '.')
        {
            _position++;
        }

        if (start == _position)
        {
            throw new FormatException("公式格式不正确。");
        }

        var text = _expression[start.._position];
        return decimal.Parse(text, CultureInfo.InvariantCulture);
    }

    private string ParseIdentifier()
    {
        var start = _position;
        while (char.IsLetterOrDigit(Current) || Current == '_')
        {
            _position++;
        }

        return _expression[start.._position];
    }

    private static decimal EvaluateFunction(string name, List<decimal> args)
    {
        return name.ToLowerInvariant() switch
        {
            "ceil" when args.Count == 1 => Math.Ceiling(args[0]),
            "floor" when args.Count == 1 => Math.Floor(args[0]),
            "round" when args.Count == 1 => Math.Round(args[0], MidpointRounding.AwayFromZero),
            "min" when args.Count >= 1 => args.Min(),
            "max" when args.Count >= 1 => args.Max(),
            _ => throw new FormatException($"不支持的函数：{name}")
        };
    }

    private void Expect(char expected)
    {
        SkipWhiteSpace();
        if (!Match(expected))
        {
            throw new FormatException($"公式缺少：{expected}");
        }
    }

    private bool Match(char value)
    {
        if (Current != value)
        {
            return false;
        }

        _position++;
        return true;
    }

    private void SkipWhiteSpace()
    {
        while (char.IsWhiteSpace(Current))
        {
            _position++;
        }
    }

    private char Current => _position < _expression.Length ? _expression[_position] : '\0';

    private static bool IsIdentifierStart(char value) => char.IsLetter(value) || value == '_';
}
