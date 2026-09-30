using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using UnityEngine;
using UnityEngine.Networking;

using BACKND.Database.Internal;

namespace BACKND.Database.Network
{
    public static class DatabaseExecutor
    {
        //private static readonly string SERVER_URL = "http://localhost:10002";
        //private static readonly string SERVER_URL = "https://api.alpha.thebackend.io";
        //private static readonly string SERVER_URL = "https://api.beta.thebackend.io";

        private static readonly string SERVER_URL = "https://api.thebackend.io";
        private static readonly string ENDPOINT = "/v1/database/store";
        private static readonly int MAX_RETRIES = 3;
        private static readonly float RETRY_DELAY = 1.0f;
        private static readonly int REQUEST_TIMEOUT = 60;

        public static async BTask<Response> Execute(DatabaseRequest request, Dictionary<string, string> headers, CancellationToken cancellationToken = default)
        {
            var query = ReplacePlaceholders(request.Query, request.Parameters);

            var requestBody = new StoreRequest
            {
                query = query
            };

            var json = JsonConvert.SerializeObject(requestBody);
            var bytes = Encoding.UTF8.GetBytes(json);

            int retryCount = 0;
            Exception lastException = null;

            while (retryCount <= MAX_RETRIES)
            {
                using var webRequest = new UnityWebRequest($"{SERVER_URL}{ENDPOINT}", "POST");
                webRequest.uploadHandler = new UploadHandlerRaw(bytes);
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.timeout = REQUEST_TIMEOUT;

                webRequest.SetRequestHeader("Content-Type", "application/json");
                foreach (var header in headers)
                {
                    webRequest.SetRequestHeader(header.Key, header.Value);
                }

                try
                {
                    using var timeoutCts = new CancellationTokenSource();
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                    timeoutCts.CancelAfter(REQUEST_TIMEOUT * 1000);

                    var operation = webRequest.SendWebRequestAsBTask(linkedCts.Token);

                    try
                    {
                        var result = await operation;

                        if (result.result == UnityWebRequest.Result.Success)
                        {
                            var responseText = result.downloadHandler?.text;
                            if (string.IsNullOrEmpty(responseText))
                            {
                                return new Response
                                {
                                    Success = false,
                                    Error = "Server returned empty response"
                                };
                            }

                            var response = ParseResponse(responseText);
                            return response;
                        }
                        else
                        {
                            lastException = new Exception($"Network error: {result.error}");
                            retryCount++;

                            if (retryCount <= MAX_RETRIES)
                            {
                                Debug.LogWarning($"[DatabaseExecutor] Retry {retryCount}/{MAX_RETRIES}: {lastException.Message}");
                                await BTask.Delay(RETRY_DELAY * retryCount);
                                continue;
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        webRequest.Abort();

                        if (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                        {
                            lastException = new Exception($"Request timeout after {REQUEST_TIMEOUT} seconds");
                            retryCount++;

                            if (retryCount <= MAX_RETRIES)
                            {
                                Debug.LogWarning($"[DatabaseExecutor] Retry {retryCount}/{MAX_RETRIES}: {lastException.Message}");
                                await BTask.Delay(RETRY_DELAY * retryCount);
                                continue;
                            }
                        }
                        else
                        {
                            throw;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    retryCount++;

                    if (retryCount <= MAX_RETRIES)
                    {
                        Debug.LogWarning($"[DatabaseExecutor] Retry {retryCount}/{MAX_RETRIES}: {ex.Message}");
                        await BTask.Delay(RETRY_DELAY * retryCount);
                        continue;
                    }
                }
            }

            return new Response
            {
                Success = false,
                Error = $"Request failed after {MAX_RETRIES} retries: {lastException?.Message ?? "Unknown error"}"
            };
        }

        private static string ReplacePlaceholders(string query, Dictionary<string, object> parameters)
        {
            if (parameters == null || parameters.Count == 0)
                return query;

            var result = query;
            // 키 길이 역순 정렬: @param10이 @param1보다 먼저 치환되어 접두사 충돌 방지
            foreach (var param in parameters.OrderByDescending(p => p.Key.Length))
            {
                var value = FormatValue(param.Value);
                result = result.Replace(param.Key, value);
            }

            return result;
        }

        private static string FormatValue(object value)
        {
            return ValueFormatter.FormatValueForQuery(value);
        }

        private static Response ParseResponse(string responseText)
        {
            try
            {
                var jObj = JObject.Parse(responseText);
                var response = new Response
                {
                    Success = jObj["success"]?.Value<bool>() ?? false,
                    Error = jObj["error"]?.Value<string>()
                };

                var resultToken = jObj["result"];
                if (resultToken != null)
                {
                    response.Result = resultToken.Type == JTokenType.String
                        ? resultToken.Value<string>()
                        : resultToken.ToString(Formatting.None);
                }

                return response;
            }
            catch (Exception ex)
            {
                return new Response
                {
                    Success = false,
                    Error = $"Failed to parse server response: {ex.Message}"
                };
            }
        }

        private class StoreRequest
        {
            [JsonProperty("query")]
            public string query;
        }
    }
}