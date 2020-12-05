using System;
using System.Collections.Generic;
using System.Text;

namespace WikiClientLibrary.Cargo
{

    public class CargoQueryParameters
    {

        private static string[] emptyStrings = { };

        /// <summary>The query offset.</summary>
        public int Offset { get; set; }

        /// <summary>A limit on the number of results returned.</summary>
        public int Limit { get; set; } = 50;

        /// <summary>The Cargo database table or tables on which to search.</summary>
        public string[] Tables { get; set; } = emptyStrings;

        /// <summary>The table field(s) to retrieve.</summary>
        public string[] Fields { get; set; } = emptyStrings;

        /// <summary>The conditions for the query, corresponding to an SQL WHERE clause.</summary>
        public string Where { get; set; }

        /// <summary>Conditions for joining multiple tables, corresponding to an SQL JOIN ON clause.</summary>
        public string JoinOn { get; set; }

        /// <summary>Field(s) on which to group results, corresponding to an SQL GROUP BY clause.</summary>
        public string GroupBy { get; set; }

        /// <summary>Conditions for grouped values, corresponding to an SQL HAVING clause.</summary>
        public string Having { get; set; }

        /// <summary>The order of results, corresponding to an SQL ORDER BY clause.</summary>
        public string[] OrderBy { get; set; } = emptyStrings;

    }

}
