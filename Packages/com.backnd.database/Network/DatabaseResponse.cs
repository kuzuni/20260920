using System.Collections.Generic;

using Newtonsoft.Json;

namespace BACKND.Database.Network
{
    public class Response
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("result")]
        public string Result { get; set; }

        [JsonProperty("error")]
        public string Error { get; set; }

        [JsonProperty("analysis")]
        public AnalysisResult Analysis { get; set; }
    }

    public class AnalysisResult
    {
        [JsonProperty("query")]
        public string Query { get; set; }

        [JsonProperty("operation")]
        public string Operation { get; set; }

        [JsonProperty("valid")]
        public bool Valid { get; set; }

        [JsonProperty("tables")]
        public string[] Tables { get; set; }
    }

    public class QueryResult
    {
        [JsonProperty("operation")]
        public string Operation { get; set; }

        [JsonProperty("rows_count")]
        public int RowsCount { get; set; }

        [JsonProperty("columns")]
        public List<string> Columns { get; set; }

        [JsonProperty("data")]
        public List<Dictionary<string, object>> Data { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
    }

    public class InsertResult
    {
        [JsonProperty("affected_rows")]
        public int AffectedRows { get; set; }

        [JsonProperty("last_insert_id")]
        public object LastInsertId { get; set; }

        [JsonProperty("operation")]
        public string Operation { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
    }

    public class MutationResult
    {
        [JsonProperty("affected_rows")]
        public int AffectedRows { get; set; }

        [JsonProperty("operation")]
        public string Operation { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
    }
}