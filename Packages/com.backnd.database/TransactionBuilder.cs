using System;
using System.Collections.Generic;
using System.Text;

using BACKND.Database.Network;

using Newtonsoft.Json.Linq;

namespace BACKND.Database
{
    /// <summary>
    /// 트랜잭션 빌더 - 여러 데이터베이스 작업을 원자적으로 실행
    /// </summary>
    public class TransactionBuilder
    {
        private readonly Client client;
        private readonly List<string> statements = new();

        /// <summary>
        /// 현재 활성화된 TransactionQueryBuilder (암묵적 실행용)
        /// </summary>
        internal TransactionQueryBuilderBase activeQueryBuilder;

        internal TransactionBuilder(Client client)
        {
            this.client = client;
        }

        /// <summary>
        /// 특정 모델 타입에 대한 쿼리 빌더 시작
        /// </summary>
        public TransactionQueryBuilder<T> From<T>() where T : BaseModel, new()
        {
            // 이전에 활성화된 QueryBuilder가 있고 미완료 작업이 있으면 flush
            FlushActiveQueryBuilder();

            var queryBuilder = new TransactionQueryBuilder<T>(this, client);
            activeQueryBuilder = queryBuilder;
            return queryBuilder;
        }

        /// <summary>
        /// 활성화된 QueryBuilder의 미완료 작업을 flush
        /// </summary>
        internal void FlushActiveQueryBuilder()
        {
            if (activeQueryBuilder != null && activeQueryBuilder.HasPendingSetClauses)
            {
                activeQueryBuilder.FlushPendingSetClauses();
            }
            activeQueryBuilder = null;
        }

        /// <summary>
        /// SQL 문장 추가 (내부용)
        /// </summary>
        internal void AddStatement(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql))
                throw new ArgumentException("SQL statement cannot be empty");

            statements.Add(sql);
        }

        /// <summary>
        /// 현재 트랜잭션에 포함된 작업 수
        /// </summary>
        public int Count => statements.Count;

        /// <summary>
        /// 트랜잭션 실행
        /// </summary>
        public async BTask<TransactionResult> Commit()
        {
            // 미완료 작업이 있으면 flush
            FlushActiveQueryBuilder();

            if (statements.Count == 0)
                throw new InvalidOperationException("Transaction has no operations. Add at least one operation before committing.");

            // DynamoDB TransactWriteItems 제한: 100개
            const int maxOperations = 100;
            if (statements.Count > maxOperations)
                throw new InvalidOperationException($"Transaction exceeds maximum operations limit. Maximum: {maxOperations}, Current: {statements.Count}");

            // 트랜잭션 SQL 생성
            var sb = new StringBuilder();
            sb.AppendLine("BEGIN;");
            foreach (var statement in statements)
            {
                sb.Append(statement);
                if (!statement.EndsWith(";"))
                    sb.Append(";");
                sb.AppendLine();
            }
            sb.Append("COMMIT;");

            var request = new DatabaseRequest
            {
                Query = sb.ToString(),
                Parameters = new Dictionary<string, object>()
            };

            // user_uuid 파라미터 추가
            if (client.UserUUID != null)
            {
                request.Parameters["@current_user_uuid"] = client.UserUUID;
            }

            var response = await client.ExecuteTransaction(request);

            if (!response.Success)
            {
                return new TransactionResult
                {
                    Success = false,
                    OperationCount = statements.Count,
                    Error = response.Error,
                    Message = "Transaction failed"
                };
            }

            // 결과 파싱 시도
            TransactionResult result = new TransactionResult();
            try
            {
                var jObj = JObject.Parse(response.Result);

                result.TotalAffectedRows = jObj["affected_rows"]?.Value<int>() ?? 0;
                result.OperationCount = jObj["statement_count"]?.Value<int>() ?? statements.Count;
                result.Message = jObj["message"]?.Value<string>();
            }
            catch (Exception ex)
            {
                result.TotalAffectedRows = 0;
                result.OperationCount = statements.Count;
                result.Message = $"Transaction succeeded but result parsing failed: {ex.Message}";
            }

            result.Success = true;
            result.Message = result.Message ?? "Transaction committed successfully";

            // Commit 후 상태 초기화 (재사용 시 이전 statements 중복 전송 방지)
            statements.Clear();

            return result;
        }
    }

    /// <summary>
    /// TransactionQueryBuilder 베이스 클래스 (암묵적 실행 지원용)
    /// </summary>
    public abstract class TransactionQueryBuilderBase
    {
        internal abstract bool HasPendingSetClauses { get; }
        internal abstract void FlushPendingSetClauses();
    }
}
