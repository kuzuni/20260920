using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BACKND.Database.Internal
{
    /// <summary>
    /// SQL 쿼리 생성 유틸리티
    /// </summary>
    public static class SqlBuilder
    {
        #region WHERE 절 생성

        /// <summary>
        /// WhereCondition 목록에서 WHERE 절 생성
        /// </summary>
        public static string BuildWhereClause(
            List<WhereCondition> whereConditions, 
            bool isOfCurrentUser = false, 
            TableType? tableType = null)
        {
            if (whereConditions.Count == 0 && !isOfCurrentUser)
                return null;

            var sb = new StringBuilder();

            var hasUserUuidCondition = whereConditions.Any(c => c.ColumnName == "user_uuid");
            if (isOfCurrentUser && tableType == TableType.UserTable && !hasUserUuidCondition)
            {
                sb.Append("user_uuid = @current_user_uuid");
                if (whereConditions.Count > 0)
                    sb.Append(" AND ");
            }

            for (int i = 0; i < whereConditions.Count; i++)
            {
                var condition = whereConditions[i];

                // 논리 연산자 (첫 번째 조건 제외)
                if (i > 0)
                {
                    sb.Append(condition.LogicalOperator == LogicalOperator.And ? " AND " : " OR ");
                }

                if (condition.IsGroupStart)
                {
                    sb.Append("(");
                }

                sb.Append(BuildConditionString(condition));

                if (condition.IsGroupEnd)
                {
                    sb.Append(")");
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// 단일 WhereCondition을 SQL 조건 문자열로 변환
        /// </summary>
        public static string BuildConditionString(WhereCondition condition)
        {
            // NULL 비교 시 자동으로 IS NULL / IS NOT NULL로 변환
            if (condition.Value == null)
            {
                return condition.Operator switch
                {
                    CompareOperator.Equal => $"{condition.ColumnName} IS NULL",
                    CompareOperator.NotEqual => $"{condition.ColumnName} IS NOT NULL",
                    CompareOperator.IsNull => $"{condition.ColumnName} IS NULL",
                    CompareOperator.IsNotNull => $"{condition.ColumnName} IS NOT NULL",
                    _ => throw new NotSupportedException($"NULL comparison with operator {condition.Operator} is not supported. Use 'IS NULL' or 'IS NOT NULL'")
                };
            }

            return condition.Operator switch
            {
                CompareOperator.Equal => $"{condition.ColumnName} = {ValueFormatter.FormatValueForQuery(condition.Value)}",
                CompareOperator.NotEqual => $"{condition.ColumnName} != {ValueFormatter.FormatValueForQuery(condition.Value)}",
                CompareOperator.GreaterThan => $"{condition.ColumnName} > {ValueFormatter.FormatValueForQuery(condition.Value)}",
                CompareOperator.GreaterThanOrEqual => $"{condition.ColumnName} >= {ValueFormatter.FormatValueForQuery(condition.Value)}",
                CompareOperator.LessThan => $"{condition.ColumnName} < {ValueFormatter.FormatValueForQuery(condition.Value)}",
                CompareOperator.LessThanOrEqual => $"{condition.ColumnName} <= {ValueFormatter.FormatValueForQuery(condition.Value)}",
                CompareOperator.Between => $"{condition.ColumnName} BETWEEN {ValueFormatter.FormatValueForQuery(condition.Value)} AND {ValueFormatter.FormatValueForQuery(condition.SecondValue)}",
                CompareOperator.In => BuildInClause(condition),
                CompareOperator.IsNull => $"{condition.ColumnName} IS NULL",
                CompareOperator.IsNotNull => $"{condition.ColumnName} IS NOT NULL",
                _ => throw new NotSupportedException($"Operator {condition.Operator} is not supported")
            };
        }

        /// <summary>
        /// IN 절 생성
        /// </summary>
        public static string BuildInClause(WhereCondition condition)
        {
            if (condition.Value is IEnumerable enumerable && !(condition.Value is string))
            {
                var values = new List<string>();
                foreach (var item in enumerable)
                {
                    values.Add(ValueFormatter.FormatValueForQuery(item));
                }

                if (values.Count == 0)
                    throw new InvalidOperationException("IN clause requires at least one value.");

                return $"{condition.ColumnName} IN ({string.Join(", ", values)})";
            }

            return $"{condition.ColumnName} = {ValueFormatter.FormatValueForQuery(condition.Value)}";
        }

        #endregion

        #region ORDER BY 절 생성

        /// <summary>
        /// OrderByInfo 목록에서 ORDER BY 절 생성
        /// </summary>
        public static string BuildOrderByClause(List<OrderByInfo> orderByList)
        {
            if (orderByList == null || orderByList.Count == 0)
                return null;

            var clauses = orderByList.Select(o => $"{o.Column} {(o.Descending ? "DESC" : "ASC")}");
            return string.Join(", ", clauses);
        }

        #endregion

        #region INSERT 쿼리 생성

        /// <summary>
        /// INSERT 쿼리 생성
        /// </summary>
        public static string BuildInsertQuery(BaseModel model, out Dictionary<string, object> parameters)
        {
            parameters = new Dictionary<string, object>();
            var columns = new List<string>();
            var values = new List<string>();

            var tableName = model.GetTableName();
            var columnList = model.GetColumnList().Split(',').Select(c => c.Trim()).ToArray();
            var autoIncrementColumn = model.GetAutoIncrementColumnName();

            foreach (var columnName in columnList)
            {
                // AutoIncrement 컬럼은 INSERT에서 제외
                if (!string.IsNullOrEmpty(autoIncrementColumn) &&
                    autoIncrementColumn.Equals(columnName, StringComparison.OrdinalIgnoreCase))
                    continue;

                var value = model.GetValue(columnName);
                var isNullableType = model.IsPropertyNullableType(columnName);
                var isColumnNullable = model.IsColumnNullable(columnName);

                // Nullable<T> 타입(int?, DateTime? 등)이고 값이 null인 경우
                if (isNullableType && value == null)
                {
                    if (isColumnNullable)
                    {
                        // NULL 허용 컬럼이면 NULL 값으로 INSERT
                        columns.Add(columnName);
                        values.Add("NULL");
                        continue;
                    }
                    else
                    {
                        // NotNull 컬럼인데 값이 null이면 기본값 확인
                        var defaultValue = model.GetColumnDefaultValue(columnName);
                        if (!string.IsNullOrEmpty(defaultValue))
                            continue;

                        throw new InvalidOperationException($"Column '{columnName}' cannot be null");
                    }
                }

                // 참조 타입(string, class 등)이 null인 경우
                if (!isColumnNullable && value == null)
                {
                    var defaultValue = model.GetColumnDefaultValue(columnName);
                    if (!string.IsNullOrEmpty(defaultValue))
                        continue;

                    throw new InvalidOperationException($"Column '{columnName}' cannot be null");
                }

                columns.Add(columnName);
                values.Add(ValueFormatter.FormatValueForQuery(value));
            }

            if (columns.Count == 0)
                throw new InvalidOperationException("No columns to insert");

            return $"INSERT INTO {tableName} ({string.Join(", ", columns)}) VALUES ({string.Join(", ", values)})";
        }

        #endregion

        #region UPDATE 쿼리 생성

        /// <summary>
        /// 모델 기반 UPDATE 쿼리 생성
        /// </summary>
        public static string BuildUpdateQuery(BaseModel model, string whereClause, out Dictionary<string, object> parameters)
        {
            parameters = new Dictionary<string, object>();
            var setClauses = new List<string>();

            var tableName = model.GetTableName();
            var columnList = model.GetColumnList().Split(',').Select(c => c.Trim()).ToArray();
            var primaryKeyColumns = model.GetPrimaryKeyColumnNames();

            foreach (var columnName in columnList)
            {
                // PK 컬럼은 UPDATE SET 절에서 제외
                if (primaryKeyColumns.Any(pk => pk.Equals(columnName, StringComparison.OrdinalIgnoreCase)))
                    continue;

                var value = model.GetValue(columnName);
                var isNullableType = model.IsPropertyNullableType(columnName);
                var isColumnNullable = model.IsColumnNullable(columnName);

                // Nullable<T> 타입(int?, DateTime? 등)이고 값이 null인 경우
                if (isNullableType && value == null)
                {
                    if (isColumnNullable)
                    {
                        // NULL 허용 컬럼이면 NULL로 UPDATE
                        setClauses.Add($"{columnName} = NULL");
                        continue;
                    }
                    else
                    {
                        throw new InvalidOperationException($"Column '{columnName}' cannot be null");
                    }
                }

                // 참조 타입(string, class 등)이 null인 경우
                if (!isColumnNullable && value == null)
                    throw new InvalidOperationException($"Column '{columnName}' cannot be null");

                setClauses.Add($"{columnName} = {ValueFormatter.FormatValueForQuery(value)}");
            }

            if (setClauses.Count == 0)
                throw new InvalidOperationException("No columns to update");

            return $"UPDATE {tableName} SET {string.Join(", ", setClauses)} WHERE {whereClause}";
        }

        /// <summary>
        /// SetClause 목록 기반 UPDATE 쿼리 생성 (Set/Inc/Dec용)
        /// </summary>
        public static string BuildUpdateQueryFromSetClauses(string tableName, List<SetClause> setClauses, string whereClause)
        {
            if (setClauses == null || setClauses.Count == 0)
                throw new InvalidOperationException("No set clauses specified");

            var setStatements = setClauses.Select(clause =>
            {
                var formattedValue = ValueFormatter.FormatValueForQuery(clause.Value);
                // Operator가 null이면 직접 값 설정 (Set), 아니면 산술 연산 (Inc/Dec)
                if (string.IsNullOrEmpty(clause.Operator))
                    return $"{clause.ColumnName} = {formattedValue}";

                if (clause.Operator != "+" && clause.Operator != "-")
                    throw new InvalidOperationException(
                        $"Invalid SetClause operator: '{clause.Operator}'. Only '+' or '-' are allowed.");

                return $"{clause.ColumnName} = {clause.ColumnName} {clause.Operator} {formattedValue}";
            });

            return $"UPDATE {tableName} SET {string.Join(", ", setStatements)} WHERE {whereClause}";
        }

        #endregion

        #region DELETE 쿼리 생성

        /// <summary>
        /// DELETE 쿼리 생성
        /// </summary>
        public static string BuildDeleteQuery(string tableName, string whereClause)
        {
            if (string.IsNullOrEmpty(whereClause))
                throw new InvalidOperationException("DELETE requires a WHERE clause");

            return $"DELETE FROM {tableName} WHERE {whereClause}";
        }

        #endregion

        #region SELECT 쿼리 생성

        /// <summary>
        /// SELECT 쿼리 생성
        /// </summary>
        public static string BuildSelectQuery(
            string tableName,
            string columnList,
            List<WhereCondition> whereConditions,
            List<OrderByInfo> orderByList,
            int? limit = null,
            int? offset = null,
            bool isOfCurrentUser = false,
            TableType? tableType = null)
        {
            var sb = new StringBuilder();
            sb.Append("SELECT ");
            sb.Append(columnList);
            sb.Append($" FROM {tableName}");

            var whereClause = BuildWhereClause(whereConditions, isOfCurrentUser, tableType);
            if (!string.IsNullOrEmpty(whereClause))
                sb.Append($" WHERE {whereClause}");

            var orderByClause = BuildOrderByClause(orderByList);
            if (!string.IsNullOrEmpty(orderByClause))
                sb.Append($" ORDER BY {orderByClause}");

            if (limit.HasValue)
                sb.Append($" LIMIT {limit.Value}");

            if (offset.HasValue)
                sb.Append($" OFFSET {offset.Value}");

            return sb.ToString();
        }

        /// <summary>
        /// COUNT 쿼리 생성
        /// </summary>
        public static string BuildCountQuery(
            string tableName,
            List<WhereCondition> whereConditions,
            bool isOfCurrentUser = false,
            TableType? tableType = null)
        {
            var sb = new StringBuilder();
            sb.Append($"SELECT COUNT(1) FROM {tableName}");

            var whereClause = BuildWhereClause(whereConditions, isOfCurrentUser, tableType);
            if (!string.IsNullOrEmpty(whereClause))
                sb.Append($" WHERE {whereClause}");

            return sb.ToString();
        }

        #endregion

        #region Primary Key WHERE 절 생성

        /// <summary>
        /// 모델의 Primary Key로 WHERE 조건 목록 생성
        /// </summary>
        public static List<WhereCondition> BuildPrimaryKeyConditions(BaseModel model)
        {
            var conditions = new List<WhereCondition>();
            var primaryKeyColumns = model.GetPrimaryKeyColumnNames();

            if (primaryKeyColumns.Length == 0)
                throw new InvalidOperationException("Model has no Primary Key defined");

            foreach (var primaryKeyColumn in primaryKeyColumns)
            {
                var primaryKeyValue = model.GetValue(primaryKeyColumn);
                if (primaryKeyValue == null)
                    throw new InvalidOperationException($"Primary Key '{primaryKeyColumn}' has no value");

                conditions.Add(new WhereCondition
                {
                    ColumnName = primaryKeyColumn,
                    Operator = CompareOperator.Equal,
                    Value = primaryKeyValue,
                    LogicalOperator = LogicalOperator.And
                });
            }

            return conditions;
        }

        #endregion
    }
}
