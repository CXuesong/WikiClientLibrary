using System.Collections.Immutable;
using System.Linq.Expressions;

namespace WikiClientLibrary.Cargo.Linq.IntermediateExpressions;

internal sealed class CargoFunctionExpression : CargoSqlExpression
{

    public CargoFunctionExpression(string name, Type type, params Expression[] arguments)
        : this(name, type, (IEnumerable<Expression>)arguments)
    {
    }

    public CargoFunctionExpression(string name, Type type, IEnumerable<Expression> arguments)
    {
        Name = name;
        Type = type;
        Arguments = arguments.ToImmutableList();
    }

    public string Name { get; }

    /// <inheritdoc />
    public override Type Type { get; }

    public IImmutableList<Expression> Arguments { get; }

    /// <inheritdoc />
    protected override Expression VisitChildren(ExpressionVisitor visitor)
    {
        var argBuilder = Arguments.ToImmutableList().ToBuilder();
        for (int i = 0; i < Arguments.Count; i++)
        {
            var a = visitor.Visit(Arguments[i]);
            if (a != Arguments[i]) argBuilder[i] = a;
        }
        return Update(argBuilder.ToImmutable());
    }

    public CargoFunctionExpression Update(IEnumerable<Expression> arguments)
    {
        if (ReferenceEquals(Arguments, arguments))
            return this;
        return new CargoFunctionExpression(Name, Type, arguments);
    }

}
