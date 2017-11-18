using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace WikiClientLibrary.Pages.Queries.Properties
{
    public class CategoryInfoPropertyProvider : WikiPagePropertyProvider<CategoryInfoPropertyGroup>
    {
        /// <inheritdoc />
        public override IEnumerable<KeyValuePair<string, object>> EnumParameters()
        {
            return Enumerable.Empty<KeyValuePair<string, object>>();
        }

        /// <inheritdoc />
        public override CategoryInfoPropertyGroup ParsePropertyGroup(JObject json)
        {
            return CategoryInfoPropertyGroup.Create(json);
        }

        /// <inheritdoc />
        public override string PropertyName => "categoryinfo";
    }

    public class CategoryInfoPropertyGroup : WikiPagePropertyGroup
    {

        public static CategoryInfoPropertyGroup Create(JObject jPage)
        {
            var cat = jPage["categoryinfo"];
            // jpage["imageinfo"] == null indicates the page may not be a valid Category.
            if (cat == null) return null;
            return new CategoryInfoPropertyGroup(cat);
        }

        private CategoryInfoPropertyGroup(JToken jCategoryInfo)
        {
            MembersCount = (int)jCategoryInfo["size"];
            PagesCount = (int)jCategoryInfo["pages"];
            FilesCount = (int)jCategoryInfo["files"];
            SubcategoriesCount = (int)jCategoryInfo["subcats"];
        }

        public int MembersCount { get;  }

        public int PagesCount { get;  }

        public int FilesCount { get;  }

        public int SubcategoriesCount { get;  }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"M:{MembersCount}, P:{PagesCount}, S:{SubcategoriesCount}, F:{FilesCount}";
        }
    }

}
