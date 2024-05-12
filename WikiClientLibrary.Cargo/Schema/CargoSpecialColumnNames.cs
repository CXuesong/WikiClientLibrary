namespace WikiClientLibrary.Cargo.Schema;

/// <summary>
/// Special column names as listed on <a href="https://www.mediawiki.org/wiki/Extension:Cargo/Storing_data#Database_storage_details">mw:Extension:Cargo/Storing data#Database storage details</a>
/// </summary>
public static class CargoSpecialColumnNames
{

    /// <summary>Holds the name of the page from which this row of values was stored.</summary>
    public const string PageName = "_pageName";

    /// <summary>Similar to <see cref="PageName"/>, but leaves out the namespace, if there is one.</summary>
    public const string PageTitle = "_pageTitle";

    /// <summary>Holds the numerical ID of the namespace of the page from which this row of values was stored.</summary>
    public const string PageNamespace = "_pageNamespace";

    /// <summary>Holds the internal MediaWiki ID for that page.</summary>
    public const string PageId = "_pageID";

    /// <summary>Holds a unique ID for this row.</summary>
    public const string Id = "_ID";

    /// <summary>(<c>__[ListColumnName]</c> table) Holds the ID of the row (i.e., _ID) in the main table that this value corresponds to.</summary>
    public const string RowId = "_rowID";

    /// <summary>
    /// (<c>__[ListColumnName]</c> table) Holds the actual, individual value.
    /// (<c>__[ColumnName]_hierarchy</c> table) The allowed value.
    /// </summary>
    public const string Value = "_value";

    /// <summary>(<c>__[ListColumnName]</c> table) Holds the position of this value in the list (can be 1, 2, etc.)</summary>
    public const string Position = "_position";

    /// <summary>(<c>__[ColumnName]_hierarchy</c> table) The number of the leftmost node represented by this value.</summary>
    public const string Left = "_left";

    /// <summary>(<c>__[ColumnName]_hierarchy</c> table) The number of the rightmost node represented by this value.</summary>
    public const string Right = "_right";

    /// <summary>(<c>__files</c> table) The name of the relevant field of type "File".</summary>
    public const string FieldName = "_fieldName";

    /// <summary>(<c>__files</c> table) The value of the field, i.e. the name of an uploaded file.</summary>
    public const string FileName = "_fileName";

}