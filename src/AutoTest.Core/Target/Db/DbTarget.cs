using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace AutoTest.Core.Target.Db
{
    public class DbTarget : MonitorTarget
    {
        public string ConnectionString { get; set; }
        public string Sql { get; set;  }
        public string DbType {  get; set; }
        public int Rows { get; set; }
        public int EffectedRows { get; set; }
        public SqlCommandType CommandType { get; set; }
        public override string Type => "DB";

        public DbTarget(string sqlstring,string sql,string dbtype,int rows,int effectedrows,SqlCommandType commandType) 
        {
            ConnectionString = sqlstring;
            Sql = sql;
            DbType= dbtype;
            Rows = rows;
            EffectedRows= effectedrows;
            CommandType = commandType;
        }

        public override string ToJson()
        {
            return JsonSerializer.Serialize(this);
        }
    }
    public enum SqlCommandType
    {
        Query,      // SELECT
        NonQuery,   // INSERT / UPDATE / DELETE
        Scalar      // COUNT / SUM / 单值
    }
}
