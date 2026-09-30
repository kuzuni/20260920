namespace BACKND.Database
{
    public abstract class BaseModel
    {
        public virtual string GetTableName() { return string.Empty; }
        public virtual TableType GetTableType() { return TableType.FlexibleTable; }
        public virtual bool GetClientAccess() { return false; }
        public virtual string[] GetReadPermissions() { return new string[] { "SELF", "OTHERS" }; }
        public virtual string[] GetWritePermissions() { return new string[] { "SELF", "OTHERS" }; }
        public virtual string[] GetPrimaryKeyColumnNames() { return new string[0]; }
        public virtual string GetAutoIncrementColumnName() { return string.Empty; }
        public virtual string GetPrimaryKey() { return string.Empty; }
        public virtual string GetColumnList() { return string.Empty; }
        public virtual string GetColumnDataType(string columnName) { return string.Empty; }
        public virtual bool IsColumnNullable(string columnName) { return false; }
        public virtual bool IsPropertyNullableType(string columnName) { return false; }
        public virtual string GetColumnDefaultValue(string columnName) { return string.Empty; }
        public virtual string GetColumnName(string propertyName) { return string.Empty; }
        public virtual object GetValue(string columnName) { return null; }
        public virtual void SetValue(string columnName, object value) { }
    }
}