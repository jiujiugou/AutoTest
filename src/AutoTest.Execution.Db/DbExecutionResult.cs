using AutoTest.Core;
using AutoTest.Core.Execution;
using System.Data;

namespace AutoTest.Execution.Db
{
    public class DbExecutionResult : ExecutionResult, IDbExecutionResult
    {
        public List<Dictionary<string, object>>? Rows { get; set; }
        public int AffectedRows { get; set; }
        // 标量
        public object? Scalar { get; set; }
        public string? Sql { get; set; }

        public long ElapsedMilliseconds { get; set; }


        // 构造成功结果
        public DbExecutionResult(bool success, string message,
            int affectedRows, List<Dictionary<string, object>> rows, string sql,
            long elapsedMilliseconds) : base(success, message)
        {
            AffectedRows = affectedRows;
            Rows = rows;
            Sql = sql;
            ElapsedMilliseconds=elapsedMilliseconds;
        }
        // 构造失败结果
        public DbExecutionResult(bool success, string message) : base(success, message)
        {
            
        }
    }
}
