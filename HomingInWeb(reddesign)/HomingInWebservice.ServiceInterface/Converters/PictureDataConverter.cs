using System;
using ServiceStack.OrmLite;
using ServiceStack;
using HomingInWebservice.ServiceModel.Models;

namespace HomingInWebservice.ServiceInterface
{
    public class PictureDataConverter : OrmLiteConverter
    {


        /// <summary>
        /// SQL Column Definiton used in CREATE Table. 
        /// </summary>
        public override string ColumnDefinition
        {
            get
            {
                return "VARCHAR(1024)";
            }
        }

        /// <summary>
        /// Quoted Value in SQL Statement
        /// </summary>
        public override string ToQuotedString(Type fieldType, object value)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Parameterized value in parameterized queries
        /// </summary>
        public override object ToDbValue(Type fieldType, object value)
        {
            var pictureData = value as PictureData;
            if (pictureData != null)
                return pictureData.ToJson();

            return null;
        }

        /// <summary>
        /// Value from DB to Populate on POCO Data Model with
        /// </summary>
        public override object FromDbValue(Type fieldType, object value)
        {
            var jsonString = value as string;
            if (jsonString != null)
                return jsonString.FromJson<PictureData>();

            return null;
        }
    }
}
