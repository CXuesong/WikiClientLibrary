using System;
using System.Collections.Generic;
using System.Text;

namespace WikiClientLibrary
{
    /// <summary>
    /// Represents basic information of a user.
    /// </summary>
    /// <remarks>Not all the fields are required to be available. But <see cref="Name"/> is mandatory.</remarks>
    public struct UserStub : IEquatable<UserStub>
    {

        public static readonly UserStub Empty = new UserStub();

        public UserStub(string name, int? id) : this(name, id, Gender.Unknown, null)
        {
        }

        public UserStub(string name, int? id, Gender gender) : this(name, id, gender, null)
        {
        }

        public UserStub(string name, int? id, Gender gender, string siteName)
        {
            Id = id;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Gender = gender;
            SiteName = siteName;
        }

        /// <summary>User name.</summary>
        public string Name { get; }

        /// <summary>User ID on the MediaWiki site.</summary>
        public int? Id { get; }

        /// <summary>User gender.</summary>
        public Gender Gender { get; }

        /// <summary>Site name the user comes from.</summary>
        public string SiteName { get; }

        /// <inheritdoc />
        public override string ToString()
        {
            if (Id == null) return Name;
            return Name + "[" + Id + "]";
        }

        /// <inheritdoc />
        public bool Equals(UserStub other)
        {
            return Id == other.Id && Name == other.Name && Gender == other.Gender && string.Equals(SiteName, other.SiteName);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is UserStub && Equals((UserStub)obj);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Id ?? 0;
                hashCode = (hashCode * 397) ^ (Name != null ? Name.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (int)Gender;
                hashCode = (hashCode * 397) ^ (SiteName != null ? SiteName.GetHashCode() : 0);
                return hashCode;
            }
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

}
