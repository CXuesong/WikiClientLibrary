using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Cargo.Schema;

namespace WikiClientLibrary.Cargo.Linq
{

    public interface ICargoTableRecordConverter
    {

        /// <summary>
        /// Converts JSON record object from MediaWiki Cargo API response into CLR model type.
        /// </summary>
        object DeserializeRecord(JObject record, CargoModel model);

    }

    public class CargoModelConverter : ICargoTableRecordConverter
    {

        public object DeserializeRecord(JObject record, CargoModel model)
        {
            return record.ToObject(model.ClrType);
        }

    }

}
