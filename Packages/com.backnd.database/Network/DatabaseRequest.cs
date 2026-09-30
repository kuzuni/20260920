using System.Collections.Generic;

namespace BACKND.Database.Network
{
    public class DatabaseRequest
    {
        public string Query { get; set; }
        public Dictionary<string, object> Parameters { get; set; }

        public DatabaseRequest()
        {
            Query = string.Empty;
            Parameters = new Dictionary<string, object>();
        }
    }
}