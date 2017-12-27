using System;
using System.Collections.Generic;
using System.Text;
using WikiClientLibrary.Wikibase.DataTypes;

namespace WikiClientLibrary.Wikibase
{
    /// <summary>
    /// Provides basic access to Wikibase entities.
    /// </summary>
    public interface IEntity
    {
        /// <summary>
        /// Id of the entity.
        /// </summary>
        /// <value>Item or Property ID, OR <c>null</c> if this is a new entity that has not made any changes.</value>
        string Id { get; }

        /// <summary>
        /// For property entity, gets the data type of the property.
        /// </summary>
        /// <value>The data type of the value when this property is used in a <see cref="Snak"/>, or <c>null</c> if not applicable.</value>
        WikibaseDataType DataType { get; }

        /// <summary>Gets the labels (aka. names) of the entity.</summary>
        WbMonolingualTextCollection Labels { get; }

        /// <summary>Gets the descriptions of the entity.</summary>
        WbMonolingualTextCollection Descriptions { get; }

        /// <summary>Gets the aliases of the entity.</summary>
        WbMonolingualTextsCollection Aliases { get; }

        /// <summary>Gets the sitelinks of the entity.</summary>
        EntitySiteLinkCollection SiteLinks { get; }

        /// <summary>Gets the claims of the entity.</summary>
        ClaimCollection Claims { get; }

        /// <summary>Wikibase entity type.</summary>
        EntityType Type { get; }
    }
}
