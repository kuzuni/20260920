using System;

namespace BACKND.Database
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class PrimaryKeyAttribute : Attribute
    {
        public bool AutoIncrement { get; set; } = false;

        public PrimaryKeyAttribute() { }
    }
}