namespace WikiClientLibrary;

/// <summary>
/// Represents basic information of a user.
/// </summary>
/// <remarks>Not all the fields are required to be available. But <see cref="Name"/> is mandatory.</remarks>
public readonly struct UserStub : IEquatable<UserStub>
{

    public static readonly UserStub Empty = new UserStub();

    public UserStub(string name, long id) : this(name, id, Gender.Unknown, null)
    {
    }

    public UserStub(string name, long id, Gender gender) : this(name, id, gender, null)
    {
    }

    public UserStub(string name, long id, Gender gender, string? siteName)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Id = id;
        Gender = gender;
        SiteName = siteName;
    }

    /// <summary>Gets user name.</summary>
    public string Name { get; }

    /// <summary>Gets user ID on the MediaWiki site.</summary>
    /// <value>User ID on the MediaWiki site, or <c>0</c> for anonymous users.</value>
    public long Id { get; }

    /// <summary>Gets user's gender.</summary>
    public Gender Gender { get; }

    /// <summary>Gets the site name the user comes from.</summary>
    /// <value>The site designation. For example, Wikimedia sites has designations such as <c>enwiki</c>, <c>zhwiki</c>, <c>enwikisource</c>, etc.</value>
    public string? SiteName { get; }

    /// <summary>Determines whether the user has ID.</summary>
    /// <remarks>An anonymous user does not have an ID, and <see cref="Id"/> will be <c>0</c> in this case.</remarks>
    public bool HasId => Id != 0;

    /// <inheritdoc />
    public override string ToString()
    {
        if (Name == null) return "<Empty>";
        if (Id == 0) return Name;
        return Name + "[" + Id + "]";
    }

    /// <inheritdoc />
    public bool Equals(UserStub other)
    {
        return Id == other.Id && Name == other.Name && Gender == other.Gender && SiteName == other.SiteName;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        if (obj is null) return false;
        return obj is UserStub other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(Id, Name, Gender, SiteName);
    }

    public static bool operator ==(UserStub left, UserStub right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(UserStub left, UserStub right)
    {
        return !left.Equals(right);
    }

}

/// <summary>
/// Gender of a MediaWiki site contributor.
/// </summary>
public enum Gender
{

    /// <summary>No gender preference.</summary>
    Unknown = 0,

    /// <summary>Male.</summary>
    Male,

    /// <summary>Female.</summary>
    Female

}
