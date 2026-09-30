namespace BACKND.Database.Internal
{
    /// <summary>
    /// SQL 비교 연산자
    /// </summary>
    public enum CompareOperator
    {
        Equal,
        NotEqual,
        GreaterThan,
        GreaterThanOrEqual,
        LessThan,
        LessThanOrEqual,
        Between,
        In,
        IsNull,
        IsNotNull
    }

    /// <summary>
    /// SQL 논리 연산자
    /// </summary>
    public enum LogicalOperator
    {
        And,
        Or
    }

    /// <summary>
    /// WHERE 절 조건 정보
    /// </summary>
    public class WhereCondition
    {
        public string ColumnName { get; set; }
        public CompareOperator Operator { get; set; }
        public object Value { get; set; }
        public object SecondValue { get; set; }  // BETWEEN 연산자용
        public LogicalOperator LogicalOperator { get; set; }
        public bool IsGroupStart { get; set; }   // 그룹 시작: (
        public bool IsGroupEnd { get; set; }     // 그룹 종료: )
    }

    /// <summary>
    /// ORDER BY 절 정보
    /// </summary>
    public class OrderByInfo
    {
        public string Column { get; set; }
        public bool Descending { get; set; }
    }

    /// <summary>
    /// SET 절 정보 (Inc/Dec 연산용)
    /// </summary>
    public class SetClause
    {
        public string ColumnName { get; set; }
        public string Operator { get; set; }  // "+" or "-"
        public object Value { get; set; }
    }
}
