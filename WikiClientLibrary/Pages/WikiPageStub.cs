using System;
using System.Collections.Generic;
using System.Text;

namespace WikiClientLibrary.Pages
{
    /// <summary>
    /// Contains basic information for identifying a page.
    /// </summary>
    public struct WikiPageStub : IEquatable<WikiPageStub>
    {

        /// <summary>
        /// Initializes a new instance of <see cref="WikiPageStub"/>.
        /// </summary>
        /// <param name="id">Page ID.</param>
        /// <param name="title">Page full title.</param>
        /// <param name="namespaceId">Page namespace ID.</param>
        public WikiPageStub(int id, string title, int namespaceId) : this()
        {
            Id = id;
            Title = title;
            NamespaceId = namespaceId;
        }

        /// <summary>Page ID.</summary>
        public int Id { get; }

        /// <summary>Page full title.</summary>
        public string Title { get; }

        /// <summary>Page namespace ID.</summary>
        public int NamespaceId { get; }

        /// <inheritdoc />
        public bool Equals(WikiPageStub other)
        {
            return Id == other.Id && string.Equals(Title, other.Title) && NamespaceId == other.NamespaceId;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is WikiPageStub && Equals((WikiPageStub)obj);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Id;
                hashCode = (hashCode * 397) ^ (Title != null ? Title.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ NamespaceId;
                return hashCode;
            }
        }

        public static bool operator ==(WikiPageStub left, WikiPageStub right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(WikiPageStub left, WikiPageStub right)
        {
            return !left.Equals(right);
        }
    }
}
