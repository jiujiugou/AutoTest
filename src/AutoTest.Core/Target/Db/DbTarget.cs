using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace AutoTest.Core.Target.Db
{
    /// <summary>
    /// 数据库监控目标：描述一次数据库命令执行的配置（连接串、SQL、数据库类型等）。
    /// </summary>
    public class DbTarget : MonitorTarget
    {
        /// <summary>
        /// 数据库连接字符串。
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// 要执行的 SQL。
        /// </summary>
        public string Sql { get; set;  }

        /// <summary>
        /// 数据库类型标识（如 sqlserver/mysql/postgresql）。
        /// </summary>
        public string DbType {  get; set; }

        /// <summary>
        /// 结果行数（可选，按业务场景使用）。
        /// </summary>
        public int Rows { get; set; }

        /// <summary>
        /// 影响行数（可选，按业务场景使用）。
        /// </summary>
        public int EffectedRows { get; set; }

        /// <summary>
        /// SQL 命令类型（查询/非查询/标量）。
        /// </summary>
        public SqlCommandType CommandType { get; set; }
        public override string Type => "DB";

        /// <summary>
        /// 创建数据库监控目标。
        /// </summary>
        public DbTarget(string sqlstring,string sql,string dbtype,int rows,int effectedrows,SqlCommandType commandType) 
        {
            ConnectionString = sqlstring;
            Sql = sql;
            DbType= dbtype;
            Rows = rows;
            EffectedRows= effectedrows;
            CommandType = commandType;
        }

        /// <summary>
        /// 将目标配置序列化为 JSON，用于数据库存储。
        /// </summary>
        public override string ToJson()
        {
            return JsonSerializer.Serialize(this);
        }
    }

    /// <summary>
    /// SQL 命令类型。
    /// </summary>
    public enum SqlCommandType
    {
        Query,      // SELECT
        NonQuery,   // INSERT / UPDATE / DELETE
        Scalar      // COUNT / SUM / 单值
    }
}
