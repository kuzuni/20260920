namespace BACKND.Database
{
    /// <summary>
    /// 트랜잭션 실행 결과
    /// </summary>
    public class TransactionResult
    {
        /// <summary>
        /// 트랜잭션 성공 여부
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 트랜잭션에 포함된 작업 수
        /// </summary>
        public int OperationCount { get; set; }

        /// <summary>
        /// 결과 메시지
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 에러 메시지 (실패 시)
        /// </summary>
        public string Error { get; set; }

        /// <summary>
        /// 영향받은 총 행 수
        /// </summary>
        public int TotalAffectedRows { get; set; }
    }
}
