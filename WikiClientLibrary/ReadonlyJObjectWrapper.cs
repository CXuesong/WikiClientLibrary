using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace WikiClientLibrary
{
    public abstract class ReadonlyJObjectWrapper
    {
        private JObject UnderlyingObject;

        internal ReadonlyJObjectWrapper(JObject jobj)
        {
            if (jobj == null) throw new ArgumentNullException(nameof(jobj));
            this.UnderlyingObject = new JObject(jobj);
        }

        public string GetString(string name) => (string) UnderlyingObject[name];

        public int GetInt32(string name) => (int)UnderlyingObject[name];

        public long GetInt64(string name) => (long)UnderlyingObject[name];

        public DateTime GetDateTime(string name) => (DateTime) UnderlyingObject[name];
    }
}
