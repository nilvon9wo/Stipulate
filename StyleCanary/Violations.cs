using System.Collections.Generic; // expect: format:IMPORTS
using System.Collections; // expect: inspect:RedundantUsingDirective

namespace ParameterValidation.Elsewhere; // expect: build:IDE0130

public sealed class Violations
{
    private int count; // expect: build:IDE1006 // expect: build:CS0649

    public int Next(int amount)
    {
        var doubled = amount * 2; // expect: build:IDE0008
        return Bump(doubled); // expect: build:IDE0009
    }

    public int Total(int first, int second, int third, int fourth, int fifth, int sixth) => this.count + first + sixth; // expect: layout:line-length

    public int Bump(int amount)
    {
        return this.count + amount; // expect: build:IDE0022
    }

    public int Sum(int first,
        int second) => this.count + first + second; // expect: layout:wrap-rpar

    public int Spaced() => this.count  +  1; // expect: format:WHITESPACE

    public int Constant() => 42; // expect: build:CA1822

    public List<int> Fresh() => new List<int>(); // expect: build:IDE0090

    public string Describe() => (string)this.Label(); // expect: inspect:RedundantCast

    private string Label() => $"count {this.count}";
}
