using System.Collections;
using System.Diagnostics;
using System.Linq.Expressions;
using WikiClientLibrary.Cargo.Linq.IntermediateExpressions;

namespace WikiClientLibrary.Cargo.Linq;

/// <summary>
/// A queryable Cargo table instance.
/// </summary>
/// <typeparam name="T">type of the model.</typeparam>
public interface ICargoRecordSet<T> : IQueryable<T>
{

    string Name { get; }

}

internal class CargoRecordSet<T> : ICargoRecordSet<T>
{

    private readonly CargoRecordQueryable _rootQueryable;

    public CargoRecordSet(CargoModel model, CargoQueryProvider provider)
    {
        Debug.Assert(model != null);
        Debug.Assert(provider != null);
        Debug.Assert(model.ClrType == typeof(T));
        _rootQueryable = new CargoRecordQueryable<T>(provider, new CargoQueryExpression(model));
        Model = model;
        Provider = provider;
    }

    public CargoModel Model { get; }

    /// <inheritdoc />
    Type IQueryable.ElementType => _rootQueryable.ElementType;

    public CargoQueryProvider Provider { get; }

    /// <inheritdoc />
    public string Name => Model.Name;

    /// <inheritdoc />
    Expression IQueryable.Expression => _rootQueryable.Expression;

    /// <inheritdoc />
    IQueryProvider IQueryable.Provider => _rootQueryable.Provider;

    /// <inheritdoc />
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => ((IEnumerable<T>)_rootQueryable).GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<T>)_rootQueryable).GetEnumerator();

}
