using System;

namespace BACKND.Database
{
    public enum TableType
    {
        UserTable = 1,
        FlexibleTable = 2
    }

    public enum TablePermission
    {
        SELF,
        OTHERS
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class TableAttribute : Attribute
    {
        public string TableName { get; } = string.Empty;
        public TableType TableType { get; } = TableType.FlexibleTable;
        public bool ClientAccess { get; set; } = true;
        public TablePermission[] ReadPermissions { get; set; } = { TablePermission.SELF, TablePermission.OTHERS };
        public TablePermission[] WritePermissions { get; set; } = { TablePermission.SELF, TablePermission.OTHERS };

        public TableAttribute() { }

        public TableAttribute(string tableName)
        {
            TableName = tableName;
        }

        public TableAttribute(TableType tableType)
        {
            TableType = tableType;
            ApplyDefaultPermissionsForTableType(tableType);
        }

        public TableAttribute(string tableName, TableType tableType)
        {
            TableName = tableName;
            TableType = tableType;
            ApplyDefaultPermissionsForTableType(tableType);
        }

        private void ApplyDefaultPermissionsForTableType(TableType tableType)
        {
            if (tableType == TableType.UserTable)
            {
                ReadPermissions = new[] { TablePermission.SELF };
                WritePermissions = new[] { TablePermission.SELF };
            }
        }
    }
}
