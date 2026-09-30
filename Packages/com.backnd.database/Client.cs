using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

using BACKND.Database.Exceptions;
using BACKND.Database.Network;

namespace BACKND.Database
{
    public class Client : IDisposable
    {
        public string UserUUID => headers.ContainsKey("x-gamerid") ? headers["x-gamerid"] : null;

        private volatile bool initialized = false;

        private readonly Dictionary<string, string> headers = new Dictionary<string, string>();


        // HTTP 헤더 유효성 검사용 정규식 (RFC 7230 준수)
        // 헤더 키: 토큰 형식 (영문자, 숫자, !#$%&'*+-.^_`|~)
        private static readonly Regex ValidHeaderKeyRegex = new Regex(@"^[A-Za-z0-9!\#\$%&'\*\+\-\.\^_`\|~]+$", RegexOptions.Compiled);
        // 헤더 값: ASCII 인쇄 가능 문자만 허용 (32~126)
        private static readonly Regex ValidHeaderValueRegex = new Regex(@"^[\x20-\x7E]*$", RegexOptions.Compiled);

        // SQL 식별자 검증용 정규식 (테이블명/필드명/인덱스명: 영문자 시작, 영문숫자+언더스코어, 1~64자)
        private static readonly Regex ValidSqlIdentifierRegex = new Regex(@"^[A-Za-z][A-Za-z0-9_]{0,63}$", RegexOptions.Compiled);

        // ALTER TABLE 허용 액션 패턴 (ADD COLUMN, DROP COLUMN, MODIFY COLUMN만 허용)
        private static readonly Regex ValidAlterActionRegex = new Regex(
            @"^(ADD|DROP|MODIFY)\s+COLUMN\s+",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static void ValidateSqlIdentifier(string value, string parameterName)
        {
            if (string.IsNullOrEmpty(value))
                throw new ArgumentException($"{parameterName} cannot be null or empty.", parameterName);
            if (!ValidSqlIdentifierRegex.IsMatch(value))
                throw new ArgumentException(
                    $"Invalid {parameterName}: '{value}'. Must start with a letter, contain only letters/digits/underscores, and be 1-64 characters.",
                    parameterName);
        }

        private readonly Queue<QueuedRequest> requestQueue = new Queue<QueuedRequest>();
        private readonly Queue<QueuedRequest> highPriorityQueue = new Queue<QueuedRequest>();
        private readonly object queueLock = new object();
        private bool isProcessingQueue = false;
        private readonly CancellationTokenSource queueCancellationSource;

        public Client(string uuid)
        {
            SetHeader("database_uuid", uuid);

            this.queueCancellationSource = new CancellationTokenSource();
        }

        private static bool IsValidHeaderKey(string key)
        {
            return !string.IsNullOrEmpty(key) && ValidHeaderKeyRegex.IsMatch(key);
        }

        private static bool IsValidHeaderValue(string value)
        {
            return string.IsNullOrEmpty(value) || ValidHeaderValueRegex.IsMatch(value);
        }

        private bool SetHeader(string key, string value)
        {
            if (!IsValidHeaderKey(key))
            {
                return false;
            }

            if (!IsValidHeaderValue(value))
            {
                return false;
            }

            headers[key] = value;
            return true;
        }

        public async BTask Initialize()
        {
            if (initialized)
            {
                return;
            }

            var userInfoResult = await GetUserInfoAsync();
            if (!userInfoResult.IsSuccess())
            {
                UnityEngine.Debug.LogError("Failed to Backnd Service Initialization or Login Process - " + userInfoResult.ToString());
                return;
            }

            var json = Newtonsoft.Json.Linq.JObject.Parse(userInfoResult.ReturnValue);
            var gamerId = json["row"]?["gamerId"]?.ToString();
            if (!SetHeader("x-gamerid", gamerId))
            {
                UnityEngine.Debug.LogError("please check backnd login state - invalid gamerId");
                return;
            }

            var settings = BackEnd.Backend.GetBackndChatSettings();
            foreach (var header in settings)
            {
                if (headers.ContainsKey(header.Key) && !string.IsNullOrEmpty(headers[header.Key]))
                    continue;

                if (string.IsNullOrEmpty(header.Value))
                    continue;

                SetHeader(header.Key, header.Value);
            }

            StartQueueProcessing();

            initialized = true;

            await BTask.CompletedTask;
        }

        private BTask<BackEnd.BackendReturnObject> GetUserInfoAsync()
        {
            var tcs = new BTaskCompletionSource<BackEnd.BackendReturnObject>();

            BackEnd.Backend.BMember.GetUserInfoV2(result =>
            {
                tcs.SetResult(result);
            });

            return tcs.Task;
        }

        #region Queue Management

        private void StartQueueProcessing()
        {
            if (isProcessingQueue) return;

            isProcessingQueue = true;
            ProcessQueue();
        }

        private async void ProcessQueue()
        {
            // Capture before Dispose; reading CTS.Token after disposal throws.
            var queueToken = queueCancellationSource.Token;
            try
            {
                while (!queueToken.IsCancellationRequested)
                {
                    QueuedRequest request = null;

                    lock (queueLock)
                    {
                        if (highPriorityQueue.Count > 0)
                        {
                            request = highPriorityQueue.Dequeue();
                        }
                        else if (requestQueue.Count > 0)
                        {
                            request = requestQueue.Dequeue();
                        }
                    }

                    if (request != null)
                    {
                        await ProcessRequest(request);
                    }
                    else
                    {
                        await BTask.Yield();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 정상 취소 - 재시작하지 않음
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[Database.Client] Queue processing error: {ex.Message}");

                // 취소가 아닌 예외 발생 시 자동 재시작
                if (!queueToken.IsCancellationRequested)
                {
                    isProcessingQueue = false;
                    StartQueueProcessing();
                    return;
                }
            }
            finally
            {
                isProcessingQueue = false;
            }
        }

        private async BTask ProcessRequest(QueuedRequest queuedRequest)
        {
            try
            {
                var response = await DatabaseExecutor.Execute(queuedRequest.Request, headers, queuedRequest.CancellationToken);
                queuedRequest.TaskCompletionSource.SetResult(response);
            }
            catch (Exception ex)
            {
                queuedRequest.TaskCompletionSource.SetException(ex);
            }
        }

        private BTask<Response> EnqueueRequest(DatabaseRequest request, bool highPriority = false, CancellationToken cancellationToken = default)
        {
            var queuedRequest = new QueuedRequest
            {
                Request = request,
                TaskCompletionSource = new BTaskCompletionSource<Response>(),
                CancellationToken = cancellationToken,
                Timestamp = DateTime.Now
            };

            lock (queueLock)
            {
                if (!initialized)
                {
                    throw new InvalidOperationException("[Database.Client] Client not initialized. Call Initialize() first.");
                }

                if (highPriority)
                {
                    highPriorityQueue.Enqueue(queuedRequest);
                }
                else
                {
                    requestQueue.Enqueue(queuedRequest);
                }
            }

            return queuedRequest.TaskCompletionSource.Task;
        }

        #endregion

        #region Table Operations

        public async BTask CreateTable<T>() where T : BaseModel, new()
        {
            var query = BuildCreateTableQuery<T>();

            var request = new DatabaseRequest { Query = query };

            var response = await EnqueueRequest(request, highPriority: true);

            if (!response.Success)
            {
                throw new DatabaseException($"Failed to create table: {response.Error}");
            }
        }

        public async BTask DropTable<T>() where T : BaseModel, new()
        {
            var instance = new T();
            var query = $"DROP TABLE {instance.GetTableName()}";

            var request = new DatabaseRequest { Query = query };

            var response = await EnqueueRequest(request, highPriority: true);

            if (!response.Success)
            {
                throw new DatabaseException($"Failed to drop table: {response.Error}");
            }
        }

        public async BTask AlterTable<T>(string alterStatement) where T : BaseModel, new()
        {
            if (string.IsNullOrWhiteSpace(alterStatement))
                throw new ArgumentException("alterStatement cannot be null or empty.", nameof(alterStatement));

            if (!ValidAlterActionRegex.IsMatch(alterStatement.TrimStart()))
                throw new ArgumentException(
                    "alterStatement must start with 'ADD COLUMN', 'DROP COLUMN', or 'MODIFY COLUMN'.",
                    nameof(alterStatement));

            var instance = new T();
            var query = $"ALTER TABLE {instance.GetTableName()} {alterStatement}";

            var request = new DatabaseRequest { Query = query };

            var response = await EnqueueRequest(request, highPriority: true);

            if (!response.Success)
            {
                throw new DatabaseException($"Failed to alter table: {response.Error}");
            }
        }

        #endregion

        #region Index Operations

        public async BTask CreateIndex<T>(string indexName, params string[] columns) where T : BaseModel, new()
        {
            ValidateSqlIdentifier(indexName, nameof(indexName));

            if (columns == null || columns.Length == 0)
                throw new ArgumentException("At least one column must be specified.", nameof(columns));

            foreach (var column in columns)
                ValidateSqlIdentifier(column, nameof(columns));

            var instance = new T();
            var query = $"CREATE INDEX {indexName} ON {instance.GetTableName()} ({string.Join(", ", columns)})";

            var request = new DatabaseRequest { Query = query };

            var response = await EnqueueRequest(request, highPriority: true);

            if (!response.Success)
            {
                throw new DatabaseException($"Failed to create index: {response.Error}");
            }
        }

        public async BTask DropIndex<T>(string indexName) where T : BaseModel, new()
        {
            ValidateSqlIdentifier(indexName, nameof(indexName));

            var instance = new T();
            var query = $"DROP INDEX {indexName} ON {instance.GetTableName()}";

            var request = new DatabaseRequest { Query = query };

            var response = await EnqueueRequest(request, highPriority: true);

            if (!response.Success)
            {
                throw new DatabaseException($"Failed to drop index: {response.Error}");
            }
        }

        #endregion

        #region Query Operations

        public QueryBuilder<T> From<T>() where T : BaseModel, new()
        {
            if (!initialized)
            {
                throw new InvalidOperationException("[Database.Client] Client not initialized. Call Initialize() first.");
            }

            return new QueryBuilder<T>(this);
        }

        internal async BTask<Response> ExecuteRawQuery(string query, CancellationToken cancellationToken = default)
        {
            var request = new DatabaseRequest { Query = query };
            return await EnqueueRequest(request, highPriority: false, cancellationToken);
        }

        internal async BTask<Response> ExecuteQuery(DatabaseRequest request, CancellationToken cancellationToken = default)
        {
            return await EnqueueRequest(request, highPriority: false, cancellationToken);
        }

        internal async BTask<Response> ExecuteMutation(DatabaseRequest request, CancellationToken cancellationToken = default)
        {
            return await EnqueueRequest(request, highPriority: true, cancellationToken);
        }

        /// <summary>
        /// 트랜잭션 빌더 생성
        /// </summary>
        public TransactionBuilder Transaction()
        {
            if (!initialized)
            {
                throw new InvalidOperationException("[Database.Client] Client not initialized. Call Initialize() first.");
            }

            return new TransactionBuilder(this);
        }

        /// <summary>
        /// 트랜잭션 실행 (내부용)
        /// </summary>
        internal async BTask<Response> ExecuteTransaction(DatabaseRequest request, CancellationToken cancellationToken = default)
        {
            return await EnqueueRequest(request, highPriority: true, cancellationToken);
        }

        #endregion

        #region Cleanup

        private bool disposed;
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            queueCancellationSource?.Cancel();
            queueCancellationSource?.Dispose();

            lock (queueLock)
            {
                initialized = false;

                while (highPriorityQueue.Count > 0)
                {
                    var request = highPriorityQueue.Dequeue();
                    request.TaskCompletionSource.SetCanceled();
                }
                while (requestQueue.Count > 0)
                {
                    var request = requestQueue.Dequeue();
                    request.TaskCompletionSource.SetCanceled();
                }
            }
        }

        #endregion

        #region Query Generation Helper Methods

        private string BuildCreateTableQuery<T>() where T : BaseModel, new()
        {
            var instance = new T();
            var tableName = instance.GetTableName();
            var columnList = instance.GetColumnList();

            if (string.IsNullOrEmpty(tableName) || string.IsNullOrEmpty(columnList))
            {
                throw new InvalidOperationException($"Model class {typeof(T).Name} has not been processed by DatabaseWeaver. Table name or column information is missing.");
            }

            var sb = new StringBuilder();
            sb.Append($"CREATE TABLE {tableName} (");

            var columns = columnList.Split(',').Select(c => c.Trim()).ToArray();
            var columnDefs = new List<string>();

            foreach (var column in columns)
            {
                var dataType = instance.GetColumnDataType(column);
                var nullable = instance.IsColumnNullable(column);
                var defaultValue = instance.GetColumnDefaultValue(column);
                var isPrimary = instance.GetPrimaryKeyColumnNames().Any(pk => pk.Equals(column, StringComparison.OrdinalIgnoreCase));

                var columnDef = $"{column} {dataType}";

                if (isPrimary)
                {
                    columnDef += " PRIMARY KEY";
                    if (column.Equals(instance.GetAutoIncrementColumnName(), StringComparison.OrdinalIgnoreCase))
                    {
                        columnDef += " AUTO_INCREMENT";
                    }
                }
                else if (!nullable)
                {
                    columnDef += " NOT NULL";
                }

                if (!string.IsNullOrEmpty(defaultValue) && !isPrimary)
                {
                    columnDef += $" DEFAULT {FormatDefaultValue(defaultValue, dataType)}";
                }

                columnDefs.Add(columnDef);
            }

            sb.Append(string.Join(", ", columnDefs));

            var tableType = instance.GetTableType();
            var tableTypeClause = tableType == TableType.UserTable ? "USERTABLE" : "FLEXIBLETABLE";
            var readPermissions = string.Join(", ", instance.GetReadPermissions());
            var writePermissions = string.Join(", ", instance.GetWritePermissions());

            sb.Append($", {tableTypeClause} (");
            sb.Append($"CLIENT_ACCESS = {instance.GetClientAccess().ToString().ToLower()}, ");
            sb.Append($"READ = ({readPermissions}), ");
            sb.Append($"WRITE = ({writePermissions})");
            sb.Append("))");

            return sb.ToString();
        }

        private static readonly HashSet<string> SqlFunctionsAndKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "NOW()", "CURRENT_TIMESTAMP", "CURRENT_TIMESTAMP()", "UUID()",
            "CURRENT_DATE", "CURRENT_DATE()", "CURRENT_TIME", "CURRENT_TIME()",
            "NULL"
        };

        private static string FormatDefaultValue(string defaultValue, string dataType)
        {
            if (string.IsNullOrWhiteSpace(defaultValue))
                return defaultValue;

            var trimmed = defaultValue.Trim();

            // SQL 함수 및 키워드(NULL)는 그대로 반환
            if (SqlFunctionsAndKeywords.Contains(trimmed))
                return trimmed;

            // 이미 따옴표로 감싸져 있으면 그대로 반환
            if ((trimmed.StartsWith("'") && trimmed.EndsWith("'")) ||
                (trimmed.StartsWith("\"") && trimmed.EndsWith("\"")))
            {
                return trimmed;
            }

            // 함수와 NULL을 제외한 모든 값은 따옴표로 감싸기
            return $"'{trimmed.Replace("'", "''")}'";
        }

        #endregion
    }

    internal class QueuedRequest
    {
        public DatabaseRequest Request { get; set; }
        public BTaskCompletionSource<Response> TaskCompletionSource { get; set; }
        public CancellationToken CancellationToken { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class BTaskCompletionSource<T>
    {
        private BTask<T> task;
        private bool completed = false;
        private readonly object lockObj = new object();

        public BTaskCompletionSource()
        {
            task = new BTask<T>();
        }

        public BTask<T> Task => task;

        public void SetResult(T result)
        {
            lock (lockObj)
            {
                if (completed) return;
                completed = true;
                task.SetResult(result);
            }
        }

        public void SetException(Exception exception)
        {
            lock (lockObj)
            {
                if (completed) return;
                completed = true;
                task.SetException(exception);
            }
        }

        public void SetCanceled()
        {
            SetException(new OperationCanceledException());
        }
    }
}