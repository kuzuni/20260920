using System;

namespace BACKND.Database.Exceptions
{
    public class DatabaseException : Exception
    {
        public string Query { get; }
        public string Operation { get; }
        public string TableName { get; }

        public DatabaseException(string message) : base(message)
        {
        }

        public DatabaseException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public DatabaseException(string message, string query, string operation = null, string tableName = null)
            : base(message)
        {
            Query = query;
            Operation = operation;
            TableName = tableName;
        }

        public DatabaseException(string message, string query, Exception innerException)
            : base(message, innerException)
        {
            Query = query;
        }
    }
}