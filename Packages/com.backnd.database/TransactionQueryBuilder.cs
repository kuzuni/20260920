using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

using BACKND.Database.Internal;

namespace BACKND.Database
{
    /// <summary>
    /// 트랜잭션용 쿼리 빌더
    /// </summary>
    public class TransactionQueryBuilder<T> : TransactionQueryBuilderBase where T : BaseModel, new()
    {
        private readonly TransactionBuilder transaction;
        private readonly Client client;
        private readonly T modelInstance;
        private readonly ExpressionAnalyzer expressionAnalyzer;

        private readonly List<WhereCondition> whereConditions = new();
        private readonly List<SetClause> setClauses = new();
        private bool isOfCurrentUser;

        internal TransactionQueryBuilder(TransactionBuilder transaction, Client client)
        {
            this.transaction = transaction;
            this.client = client;
            this.modelInstance = new T();
            this.expressionAnalyzer = new ExpressionAnalyzer(modelInstance);
        }

        internal override bool HasPendingSetClauses => setClauses.Count > 0;

        internal override void FlushPendingSetClauses()
        {
            if (setClauses.Count == 0)
                return;

            if (whereConditions.Count == 0 && !isOfCurrentUser)
                throw new InvalidOperationException("Set/Inc/Dec requires a WHERE clause");

            var whereClause = SqlBuilder.BuildWhereClause(
                whereConditions,
                isOfCurrentUser,
                modelInstance.GetTableType());
            var sql = SqlBuilder.BuildUpdateQueryFromSetClauses(
                modelInstance.GetTableName(),
                setClauses,
                whereClause);

            transaction.AddStatement(sql);
            setClauses.Clear();
        }

        #region 조건 빌더 (체이닝 → 자신 반환)

        /// <summary>
        /// WHERE 조건 추가
        /// </summary>
        public TransactionQueryBuilder<T> Where(Expression<Func<T, bool>> predicate)
        {
            var condition = expressionAnalyzer.AnalyzeSingle(predicate, whereConditions);
            if (condition != null)
            {
                whereConditions.Add(condition);
            }
            return this;
        }

        /// <summary>
        /// 현재 사용자 데이터만 대상 (UserTable용)
        /// </summary>
        public TransactionQueryBuilder<T> OfCurrentUser()
        {
            if (modelInstance.GetTableType() != TableType.UserTable)
                throw new InvalidOperationException("OfCurrentUser() can only be used with UserTable");

            isOfCurrentUser = true;
            return this;
        }

        /// <summary>
        /// 필드 값 증가
        /// </summary>
        public TransactionQueryBuilder<T> Inc<TField>(Expression<Func<T, TField>> selector, TField value)
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

        /// <summary>
        /// 필드 값 감소
        /// </summary>
        public TransactionQueryBuilder<T> Dec<TField>(Expression<Func<T, TField>> selector, TField value)
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

        /// <summary>
        /// 필드 값 직접 설정
        /// </summary>
        public TransactionQueryBuilder<T> Set<TField>(Expression<Func<T, TField>> selector, TField value)
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

        #endregion

        #region 실행 메서드 (SQL 추가 → TransactionBuilder 반환)

        /// <summary>
        /// INSERT 작업 추가
        /// </summary>
        public TransactionBuilder Insert(T model)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));

            var sql = SqlBuilder.BuildInsertQuery(model, out _);
            transaction.AddStatement(sql);

            // 활성 QueryBuilder 해제
            transaction.activeQueryBuilder = null;
            return transaction;
        }

        /// <summary>
        /// UPDATE 작업 추가
        /// </summary>
        public TransactionBuilder Update(T model)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));

            // WHERE 절이 없으면 PrimaryKey로 자동 생성
            if (whereConditions.Count == 0)
            {
                var pkConditions = SqlBuilder.BuildPrimaryKeyConditions(model);
                whereConditions.AddRange(pkConditions);
            }

            var whereClause = SqlBuilder.BuildWhereClause(
                whereConditions,
                isOfCurrentUser,
                modelInstance.GetTableType());
            var sql = SqlBuilder.BuildUpdateQuery(model, whereClause, out _);
            transaction.AddStatement(sql);

            // 활성 QueryBuilder 해제
            transaction.activeQueryBuilder = null;
            return transaction;
        }

        /// <summary>
        /// Set/Inc/Dec로 지정한 필드만 UPDATE 작업 추가
        /// </summary>
        public TransactionBuilder Update()
        {
            if (setClauses.Count == 0)
                throw new InvalidOperationException("No modifications specified. Use Set(), Inc() or Dec() before Update()");

            if (whereConditions.Count == 0 && !isOfCurrentUser)
                throw new InvalidOperationException("Update requires a WHERE clause");

            var whereClause = SqlBuilder.BuildWhereClause(
                whereConditions,
                isOfCurrentUser,
                modelInstance.GetTableType());
            var sql = SqlBuilder.BuildUpdateQueryFromSetClauses(
                modelInstance.GetTableName(),
                setClauses,
                whereClause);

            transaction.AddStatement(sql);
            setClauses.Clear();

            // 활성 QueryBuilder 해제
            transaction.activeQueryBuilder = null;
            return transaction;
        }

        /// <summary>
        /// DELETE 작업 추가
        /// </summary>
        public TransactionBuilder Delete()
        {
            if (whereConditions.Count == 0 && !isOfCurrentUser)
                throw new InvalidOperationException("Delete requires a WHERE clause");

            var whereClause = SqlBuilder.BuildWhereClause(
                whereConditions,
                isOfCurrentUser,
                modelInstance.GetTableType());
            var sql = SqlBuilder.BuildDeleteQuery(modelInstance.GetTableName(), whereClause);
            transaction.AddStatement(sql);

            // 활성 QueryBuilder 해제
            transaction.activeQueryBuilder = null;
            return transaction;
        }

        #endregion

        #region 암묵적 실행을 위한 From<U>()

        /// <summary>
        /// 다른 테이블로 전환 (현재 Inc/Dec 작업이 있으면 자동 flush)
        /// </summary>
        public TransactionQueryBuilder<U> From<U>() where U : BaseModel, new()
        {
            // 현재 Inc/Dec 작업이 있으면 자동으로 UPDATE SQL 생성
            if (HasPendingSetClauses)
            {
                FlushPendingSetClauses();
            }

            // 새 QueryBuilder 생성
            return transaction.From<U>();
        }

        #endregion
    }
}
