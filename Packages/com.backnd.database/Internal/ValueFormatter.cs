using System;
using System.Collections.Generic;

using Newtonsoft.Json;

namespace BACKND.Database.Internal
{
    /// <summary>
    /// SQL 쿼리용 값 포맷팅 유틸리티
    /// </summary>
    public static class ValueFormatter
    {
        /// <summary>
        /// SQL 함수 및 키워드 목록
        /// </summary>
        private static readonly HashSet<string> SqlFunctionsAndKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "NOW()", "CURRENT_TIMESTAMP", "CURRENT_TIMESTAMP()", "UUID()",
            "CURRENT_DATE", "CURRENT_DATE()", "CURRENT_TIME", "CURRENT_TIME()",
            "NULL"
        };

        /// <summary>
        /// 값이 SQL 함수 또는 키워드인지 확인
        /// </summary>
        public static bool IsSqlFunctionOrKeyword(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            return SqlFunctionsAndKeywords.Contains(value.Trim());
        }

        /// <summary>
        /// 값을 SQL 쿼리용 문자열로 포맷팅
        /// </summary>
        public static string FormatValueForQuery(object value)
        {
            if (value == null) return "NULL";

            // 원본이 string 타입이면 항상 따옴표로 감싸기 (SQL 키워드 오인식 방지)
            bool isOriginalString = value is string;

            // 값을 문자열로 변환
            var stringValue = ConvertValueToString(value);

            // SQL 함수 및 키워드는 따옴표 없이 그대로 반환 (원본이 string이 아닌 경우만)
            if (!isOriginalString && IsSqlFunctionOrKeyword(stringValue))
            {
                return stringValue.Trim();
            }

            // 길이 제한 검사
            const int maxLength = 1024 * 12; // 12KB
            if (stringValue.Length > maxLength)
            {
                throw new ArgumentException($"Value too long. Maximum length is {maxLength} characters, but got {stringValue.Length}");
            }

            // 함수와 NULL을 제외한 모든 값은 따옴표로 감싸기
            return $"'{stringValue.Replace("'", "''").Replace("\\", "\\\\")}'";
        }

        /// <summary>
        /// 값을 문자열로 변환
        /// </summary>
        public static string ConvertValueToString(object value)
        {
            if (value == null) return "NULL";

            var valueType = value.GetType();

            // DateTime → ISO 8601 포맷
            if (valueType == typeof(DateTime))
                return ((DateTime)value).ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

            // bool → 소문자
            if (valueType == typeof(bool))
                return ((bool)value) ? "true" : "false";

            // Enum → 숫자 (long/uint 등 다양한 underlying type 지원)
            if (valueType.IsEnum)
                return Convert.ChangeType(value, Enum.GetUnderlyingType(valueType)).ToString();

            // 복합 객체 → JSON
            if (!valueType.IsPrimitive && valueType != typeof(string) && valueType != typeof(Guid))
            {
                try
                {
                    return JsonConvert.SerializeObject(value, new JsonSerializerSettings
                    {
                        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                        NullValueHandling = NullValueHandling.Ignore,
                        Formatting = Formatting.None
                    });
                }
                catch (JsonException ex)
                {
                    throw new InvalidOperationException($"Failed to serialize object of type {valueType.Name} to JSON", ex);
                }
            }

            // 기본: ToString()
            return value.ToString();
        }
    }
}
