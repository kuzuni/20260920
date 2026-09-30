using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

using BACKND.Database.Internal;
using BACKND.Database.Network;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using UnityEngine;

namespace BACKND.Database
{
    public class QueryBuilder<T> where T : BaseModel, new()
    {
        private readonly Client client;
        private readonly T modelInstance;
        private readonly ExpressionAnalyzer expressionAnalyzer;

        private readonly List<WhereCondition> whereConditions = new();
        private readonly List<OrderByInfo> orderByList = new();
        private readonly List<SetClause> setClauses = new();
        private int? limit;
        private int? offset;
        private bool isOfCurrentUser;

        internal QueryBuilder(Client client)
        {
            this.client = client;
            this.modelInstance = new();
            this.expressionAnalyzer = new ExpressionAnalyzer(modelInstance);
        }

        public QueryBuilder<T> Where(Expression<Func<T, bool>> predicate)
        {
            var condition = expressionAnalyzer.AnalyzeSingle(predicate, whereConditions);
            if (condition != null)
            {
                whereConditions.Add(condition);
            }
            return this;
        }

        public QueryBuilder<T> OrderBy<TKey>(Expression<Func<T, TKey>> keySelector, bool descending = false)
        {
            var columnName = expressionAnalyzer.GetColumnNameFromKeySelector(keySelector);
            if (!string.IsNullOrEmpty(columnName))
            {
                orderByList.Add(new()
                {
                    Column = columnName,
                    Descending = descending
                });
            }
            return this;
        }

        public QueryBuilder<T> OrderByDescending<TKey>(Expression<Func<T, TKey>> keySelector)
        {
            return OrderBy(keySelector, true);
        }

        public QueryBuilder<T> Take(int count)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), "Take count must be non-negative.");

            limit = count;
            return this;
        }

        public QueryBuilder<T> Skip(int count)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), "Skip count must be non-negative.");

            offset = count;
            return this;
        }

        public QueryBuilder<T> OfCurrentUser()
        {
            if (modelInstance.GetTableType() != TableType.UserTable)
                throw new InvalidOperationException("OfCurrentUser() can only be used with UserTable");

            isOfCurrentUser = true;
            return this;
        }

        public async BTask<T> First()
        {
            var list = await Take(1).ToList();
            if (list.Count == 0)
                throw new InvalidOperationException("Sequence contains no elements");
                
            return list[0];
        }

        public async BTask<T> FirstOrDefault()
        {
            var list = await Take(1).ToList();
            return list.Count > 0 ? list[0] : null;
        }

        public async BTask<List<T>> ToList()
        {
            var query = SqlBuilder.BuildSelectQuery(
                modelInstance.GetTableName(),
                modelInstance.GetColumnList(),
                whereConditions,
                orderByList,
                limit,
                offset,
                isOfCurrentUser,
                modelInstance.GetTableType());
            var parameters = GetQueryParameters();

            var request = new DatabaseRequest
            {
                Query = query,
                Parameters = parameters
            };

            var response = await client.ExecuteQuery(request);
            return ParseResponse<T>(response);
        }

        public async BTask<int> Count()
        {
            var query = SqlBuilder.BuildCountQuery(
                modelInstance.GetTableName(),
                whereConditions,
                isOfCurrentUser,
                modelInstance.GetTableType());
            var parameters = GetQueryParameters();

            var request = new DatabaseRequest
            {
                Query = query,
                Parameters = parameters
            };

            var response = await client.ExecuteQuery(request);
            return ParseCountResponse(response);
        }

        public async BTask<bool> Any()
        {
            return await Count() > 0;
        }

        public async BTask<InsertResult> Insert(T model)
        {
            ValidateModel(model);

            var query = SqlBuilder.BuildInsertQuery(model, out var parameters);
            var request = new DatabaseRequest { Query = query, Parameters = parameters };
            var response = await client.ExecuteMutation(request);

            if (!response.Success)
                throw new Exception($"Insert failed: {response.Error}");

            if (string.IsNullOrEmpty(response.Result))
                throw new Exception("Insert succeeded but server returned empty result");

            var result = ParseInsertResult(response.Result);
            UpdateAutoIncrementId(model, result);

            return result;
        }

        public async BTask<MutationResult> Update(T model)
        {
            ValidateModel(model);

            // 실행 시 빌더 상태를 오염시키지 않기 위해 로컬 복사본 사용
            var effectiveConditions = new List<WhereCondition>(whereConditions);

            // WHERE 절이 없으면 PrimaryKey로 자동 생성 시도
            if (effectiveConditions.Count == 0 && !isOfCurrentUser)
            {
                var primaryKeyColumns = model.GetPrimaryKeyColumnNames();
                if (primaryKeyColumns.Length == 0)
                    throw new InvalidOperationException("Update requires a WHERE clause or a Primary Key");

                // 복합 키 지원: 모든 PK 컬럼에 대해 WHERE 조건 생성
                foreach (var primaryKeyColumn in primaryKeyColumns)
                {
                    var primaryKeyValue = model.GetValue(primaryKeyColumn);
                    if (primaryKeyValue == null)
                        throw new InvalidOperationException($"Primary Key '{primaryKeyColumn}' has no value. Either set the Primary Key or provide a WHERE clause");

                    effectiveConditions.Add(new WhereCondition
                    {
                        ColumnName = primaryKeyColumn,
                        Operator = CompareOperator.Equal,
                        Value = primaryKeyValue,
                        LogicalOperator = LogicalOperator.And
                    });
                }
            }

            var whereClause = SqlBuilder.BuildWhereClause(effectiveConditions, isOfCurrentUser, modelInstance.GetTableType());
            var query = SqlBuilder.BuildUpdateQuery(model, whereClause, out var parameters);

            foreach (var param in GetQueryParameters())
                parameters[param.Key] = param.Value;

            var request = new DatabaseRequest { Query = query, Parameters = parameters };
            var response = await client.ExecuteMutation(request);

            if (!response.Success)
                throw new Exception($"Update failed: {response.Error}");

            if (string.IsNullOrEmpty(response.Result))
                throw new Exception("Update succeeded but server returned empty result");

            return ParseMutationResult(response.Result);
        }

        public async BTask<MutationResult> Delete()
        {
            if (whereConditions.Count == 0 && !isOfCurrentUser)
                throw new InvalidOperationException("Delete requires a WHERE clause");

            var whereClause = SqlBuilder.BuildWhereClause(whereConditions, isOfCurrentUser, modelInstance.GetTableType());
            var query = SqlBuilder.BuildDeleteQuery(modelInstance.GetTableName(), whereClause);
            var parameters = GetQueryParameters();

            var request = new DatabaseRequest { Query = query, Parameters = parameters };
            var response = await client.ExecuteMutation(request);

            if (!response.Success)
                throw new Exception($"Delete failed: {response.Error}");

            if (string.IsNullOrEmpty(response.Result))
                throw new Exception("Delete succeeded but server returned empty result");

            return ParseMutationResult(response.Result);
        }

        public QueryBuilder<T> Inc<TField>(Expression<Func<T, TField>> selector, TField value)
        {
            var columnName = expressionAnalyzer.GetColumnNameFromKeySelector(selector);
            ValidateNoDuplicateSetClause(columnName);
            setClauses.Add(new SetClause
            {
                ColumnName = columnName,
                Operator = "+",
                Value = value
            });
            return this;
        }

        public QueryBuilder<T> Dec<TField>(Expression<Func<T, TField>> selector, TField value)
        {
            var columnName = expressionAnalyzer.GetColumnNameFromKeySelector(selector);
            ValidateNoDuplicateSetClause(columnName);
            setClauses.Add(new SetClause
            {
                ColumnName = columnName,
                Operator = "-",
                Value = value
            });
            return this;
        }

        public QueryBuilder<T> Set<TField>(Expression<Func<T, TField>> selector, TField value)
        {
            var columnName = expressionAnalyzer.GetColumnNameFromKeySelector(selector);
            ValidateNoDuplicateSetClause(columnName);
            setClauses.Add(new SetClause
            {
                ColumnName = columnName,
                Operator = null,
                Value = value
            });
            return this;
        }

        private void ValidateNoDuplicateSetClause(string columnName)
        {
            if (setClauses.Any(c => c.ColumnName == columnName))
                throw new InvalidOperationException($"'{columnName}' is already being modified. Each field can only be modified once.");
        }

        public async BTask<MutationResult> Update()
        {
            if (setClauses.Count == 0)
                throw new InvalidOperationException("No modifications specified. Use Set(), Inc() or Dec() before Update()");

            if (whereConditions.Count == 0)
                throw new InvalidOperationException("Update requires a Where() condition");

            var whereClause = SqlBuilder.BuildWhereClause(whereConditions, isOfCurrentUser, modelInstance.GetTableType());
            var query = SqlBuilder.BuildUpdateQueryFromSetClauses(modelInstance.GetTableName(), setClauses, whereClause);
            var parameters = GetQueryParameters();

            var request = new DatabaseRequest { Query = query, Parameters = parameters };
            var response = await client.ExecuteMutation(request);

            if (!response.Success)
                throw new Exception($"Update failed: {response.Error}");

            if (string.IsNullOrEmpty(response.Result))
                throw new Exception("Update succeeded but server returned empty result");

            return ParseMutationResult(response.Result);
        }

        private Dictionary<string, object> GetQueryParameters()
        {
            var parameters = new Dictionary<string, object>();

            if (isOfCurrentUser && modelInstance.GetTableType() == TableType.UserTable && client.UserUUID != null)
            {
                parameters["@current_user_uuid"] = client.UserUUID;
            }

            return parameters;
        }

        private void ValidateModel(T model)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));
        }

        private void UpdateAutoIncrementId(T model, InsertResult result)
        {
            var autoIncrementColumn = model.GetAutoIncrementColumnName();
            if (string.IsNullOrEmpty(autoIncrementColumn))
                return;

            try
            {
                if (result?.LastInsertId != null)
                {
                    model.SetValue(autoIncrementColumn, result.LastInsertId);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to update auto-increment ID: {ex.Message}");
            }
        }

        private List<TModel> ParseResponse<TModel>(Response response) where TModel : BaseModel, new()
        {
            if (!response.Success)
                throw new Exception($"Query failed: {response.Error}");

            if (string.IsNullOrEmpty(response.Result))
                return new List<TModel>();

            var result = ParseQueryResult(response.Result);
            if (result?.Data == null) return new List<TModel>();

            var list = new List<TModel>();
            foreach (var row in result.Data)
            {
                try
                {
                    var model = new TModel();
                    foreach (var kvp in row)
                    {
                        model.SetValue(kvp.Key, kvp.Value);
                    }
                    list.Add(model);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Row parsing failed: {ex.Message}");
                }
            }
            return list;
        }

        private int ParseCountResponse(Response response)
        {
            if (!response.Success)
                throw new Exception($"Query failed: {response.Error}");

            if (string.IsNullOrEmpty(response.Result))
                return 0;

            var result = ParseQueryResult(response.Result);
            if (result?.Data != null && result.Data.Count > 0)
            {
                var firstRow = result.Data[0];
                if (firstRow.Count > 0)
                {
                    var value = firstRow.Values.First();
                    return value != null ? Convert.ToInt32(value) : 0;
                }
            }

            return 0;
        }

        private static InsertResult ParseInsertResult(string json)
        {
            var jObj = JObject.Parse(json);
            return new InsertResult
            {
                AffectedRows = jObj["affected_rows"]?.Value<int>() ?? 0,
                LastInsertId = jObj["last_insert_id"]?.ToObject<object>(),
                Operation = jObj["operation"]?.Value<string>(),
                Message = jObj["message"]?.Value<string>()
            };
        }

        private static MutationResult ParseMutationResult(string json)
        {
            var jObj = JObject.Parse(json);
            return new MutationResult
            {
                AffectedRows = jObj["affected_rows"]?.Value<int>() ?? 0,
                Operation = jObj["operation"]?.Value<string>(),
                Message = jObj["message"]?.Value<string>()
            };
        }

        private static QueryResult ParseQueryResult(string json)
        {
            var jObj = JObject.Parse(json);
            var result = new QueryResult
            {
                Operation = jObj["operation"]?.Value<string>(),
                RowsCount = jObj["rows_count"]?.Value<int>() ?? 0,
                Message = jObj["message"]?.Value<string>()
            };

            var columnsToken = jObj["columns"];
            if (columnsToken != null)
            {
                result.Columns = new List<string>();
                foreach (var col in columnsToken)
                {
                    result.Columns.Add(col.Value<string>());
                }
            }

            var dataToken = jObj["data"];
            if (dataToken != null)
            {
                result.Data = new List<Dictionary<string, object>>();
                foreach (var row in dataToken)
                {
                    var dict = new Dictionary<string, object>();
                    foreach (var prop in ((JObject)row).Properties())
                    {
                        dict[prop.Name] = ConvertJToken(prop.Value);
                    }
                    result.Data.Add(dict);
                }
            }

            return result;
        }

        private static object ConvertJToken(JToken token)
        {
            switch (token.Type)
            {
                case JTokenType.Null:
                    return null;
                case JTokenType.Object:
                case JTokenType.Array:
                    return token.ToString(Formatting.None);
                default:
                    return ((JValue)token).Value;
            }
        }
    }
}