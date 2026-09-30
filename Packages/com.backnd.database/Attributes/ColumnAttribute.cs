using System;

namespace BACKND.Database
{
    public enum DatabaseType
    {
        None,
        Int32,
        UInt32,
        Int64,
        UInt64,
        Float,
        Bool,
        String,
        DateTime,
        UUID,
        Json
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class ColumnAttribute : Attribute
    {
        public string ColumnName { get; } = string.Empty;
        public DatabaseType DataType { get; } = DatabaseType.None;
        public bool NotNull { get; set; } = false;
        public string DefaultValue { get; set; } = string.Empty;

        public ColumnAttribute() { }

        public ColumnAttribute(string columnName)
        {
            ColumnName = columnName;
        }

        public ColumnAttribute(DatabaseType dataType)
        {
            DataType = dataType;
        }

        public ColumnAttribute(string columnName, DatabaseType dataType)
        {
            ColumnName = columnName;
            DataType = dataType;
        }
    }
}