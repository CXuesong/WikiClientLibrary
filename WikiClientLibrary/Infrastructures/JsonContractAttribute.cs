using System.Text.Json;

namespace WikiClientLibrary.Infrastructures;

/// <summary>
/// Infrastructure. Not intended to be used directly in your code.
/// Indicates the specified class will be used as JSON contract class with <see cref="JsonSerializer"/> API.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class JsonContractAttribute : Attribute
{
}
