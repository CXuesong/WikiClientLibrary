using WikiClientLibrary.Sites;

namespace WikiClientLibrary.Cargo.Linq;

/// <summary>
/// Provides LINQ to Cargo query ability.
/// </summary>
public interface ICargoQueryContext
{
    /// <summary>
    /// Starts a Linq query expression on the specified Cargo model and Cargo table.
    /// </summary>
    /// <typeparam name="T">type of the model.</typeparam>
    /// <param name="name">name of the Cargo table. Specify <c>null</c> to use default table name corresponding to the model.</param>
    /// <returns>LINQ root.</returns>
    ICargoRecordSet<T> Table<T>(string? name);

    /// <summary>
    /// Starts a Linq query expression on the specified table.
    /// </summary>
    ICargoRecordSet<T> Table<T>();

}

public class CargoQueryContext : ICargoQueryContext
{

    private int _PaginationSize = 10;

    public CargoQueryContext(WikiSite wikiSite)
    {
        WikiSite = wikiSite ?? throw new ArgumentNullException(nameof(wikiSite));
    }

    public WikiSite WikiSite { get; }

    /// <summary>
    /// Gets/sets the default pagination size used when requesting
    /// for the records from MediaWiki server. (Default value: <c>10</c>.)
    /// </summary>
    public int PaginationSize
    {
        get => _PaginationSize;
        set
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value));
            _PaginationSize = value;
        }
    }

    /// <inheritdoc />
    public ICargoRecordSet<T> Table<T>(string? name)
    {
        return new CargoRecordSet<T>(CargoModel.FromClrType(typeof(T), name), new CargoQueryProvider(WikiSite) { PaginationSize = _PaginationSize });
    }

    /// <inheritdoc />
    public ICargoRecordSet<T> Table<T>() => Table<T>(null);

}