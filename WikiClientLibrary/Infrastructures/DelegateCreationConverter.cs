using Newtonsoft.Json.Converters;

namespace WikiClientLibrary.Infrastructures;

/// <summary>
/// Customize object creation by using a delegate to create the instance.
/// </summary>
internal class DelegateCreationConverter<T> : CustomCreationConverter<T>
{
    private readonly Func<Type, T> factory;

    public DelegateCreationConverter(Func<Type, T> factory)
    {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            this.factory = factory;
        }

    /// <summary>
    /// Creates an object which will then be populated by the serializer.
    /// </summary>
    /// <param name="objectType">Type of the object.</param>
    /// <returns>
    /// The created object.
    /// </returns>
    public override T Create(Type objectType)
    {
            return factory(objectType);
        }
}