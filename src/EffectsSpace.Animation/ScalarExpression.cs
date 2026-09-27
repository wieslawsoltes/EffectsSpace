using System.Globalization;

namespace EffectsSpace.Animation;

/// <summary>A bounded, side-effect-free scalar grammar. This is not JavaScript or ExtendScript.</summary>
public sealed class ScalarExpression
{
    private readonly string _text;
    private readonly double _time, _value;
    private readonly int _index;
    private int _position, _operations, _depth;
    private ScalarExpression(string text, double time, double value, int index) { _text = text; _time = time; _value = value; _index = index; }

    public static bool TryEvaluate(string expression, double time, double value, int index, out double result, out string? error)
    {
        result = value; error = null;
        try
        {
            if (expression.Length > 1024) throw new FormatException("Expression exceeds 1024 characters.");
            var parser = new ScalarExpression(expression, time, value, index);
            var evaluated = parser.Expression(); parser.White();
            if (parser._position != expression.Length) throw new FormatException($"Unexpected token at {parser._position}.");
            if (!double.IsFinite(evaluated) || Math.Abs(evaluated) > 1e12) throw new FormatException("Expression result is not finite or is outside the supported range.");
            result = evaluated; return true;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or OverflowException)
        { error = ex.Message; return false; }
    }

    private void Budget()
    {
        if (++_operations > 256 || _depth > 32) throw new FormatException("Expression complexity limit exceeded.");
    }
    private void White() { while (_position < _text.Length && char.IsWhiteSpace(_text[_position])) _position++; }
    private bool Eat(char c) { White(); if (_position < _text.Length && _text[_position] == c) { _position++; return true; } return false; }
    private double Expression()
    {
        Budget(); var a = Term();
        while (true) { if (Eat('+')) a += Term(); else if (Eat('-')) a -= Term(); else return a; }
    }
    private double Term()
    {
        var a = Unary();
        while (true) { if (Eat('*')) a *= Unary(); else if (Eat('/')) a /= Unary(); else if (Eat('%')) a %= Unary(); else return a; }
    }
    private double Unary()
    {
        Budget(); _depth++;
        try { if (Eat('+')) return Unary(); if (Eat('-')) return -Unary(); return Atom(); }
        finally { _depth--; }
    }
    private double Atom()
    {
        White(); Budget();
        if (Eat('(')) { var n = Expression(); if (!Eat(')')) throw new FormatException("Missing closing parenthesis."); return n; }
        var start = _position;
        if (_position < _text.Length && (char.IsDigit(_text[_position]) || _text[_position] == '.'))
        {
            while (_position < _text.Length && (char.IsDigit(_text[_position]) || _text[_position] == '.')) _position++;
            return double.Parse(_text[start.._position], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
        }
        while (_position < _text.Length && (char.IsLetter(_text[_position]) || _text[_position] == '_')) _position++;
        if (start == _position) throw new FormatException($"Expected a value at {_position}.");
        var name = _text[start.._position];
        if (!Eat('(')) return name switch { "time" => _time, "value" => _value, "index" => _index, "pi" => Math.PI, _ => throw new FormatException($"Unknown variable: {name}") };
        var args = new List<double>();
        if (!Eat(')')) { do { if (args.Count >= 5) throw new FormatException("Too many arguments."); args.Add(Expression()); } while (Eat(',')); if (!Eat(')')) throw new FormatException("Missing closing parenthesis."); }
        double Arg(int i) => i < args.Count ? args[i] : throw new FormatException($"Missing argument for {name}.");
        void Count(int n) { if (args.Count != n) throw new FormatException($"{name} requires {n} arguments."); }
        switch (name)
        {
            case "sin": Count(1); return Math.Sin(Arg(0));
            case "cos": Count(1); return Math.Cos(Arg(0));
            case "abs": Count(1); return Math.Abs(Arg(0));
            case "sqrt": Count(1); return Math.Sqrt(Arg(0));
            case "floor": Count(1); return Math.Floor(Arg(0));
            case "ceil": Count(1); return Math.Ceiling(Arg(0));
            case "min": Count(2); return Math.Min(Arg(0), Arg(1));
            case "max": Count(2); return Math.Max(Arg(0), Arg(1));
            case "clamp": Count(3); return Math.Clamp(Arg(0), Arg(1), Arg(2));
            case "linear": case "ease":
                Count(5); var span = Arg(2) - Arg(1); if (span == 0) throw new FormatException("Interpolation interval is empty.");
                var u = Math.Clamp((Arg(0) - Arg(1)) / span, 0, 1); if (name == "ease") u = u * u * (3 - 2 * u);
                return Arg(3) + (Arg(4) - Arg(3)) * u;
            case "wiggle":
                Count(2); var t = _time * Arg(0); var k = Math.Floor(t); var f = t - k; f = f * f * (3 - 2 * f);
                return _value + Arg(1) * (Noise(k, _index) * (1 - f) + Noise(k + 1, _index) * f);
            default: throw new FormatException($"Unknown function: {name}");
        }
    }
    private static double Noise(double n, int seed) { var x = Math.Sin(n * 127.1 + seed * 311.7) * 43758.5453; return (x - Math.Floor(x)) * 2 - 1; }
}
