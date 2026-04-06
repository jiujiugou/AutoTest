using System;
using System.Collections.Generic;
using System.Text;

namespace AutoTest.Assertion.Db
{
    public enum DbAssertionField
    {
        RowValue,          // 单行单列值，需要 RowIndex + ColumnName
        AffectedRows,      // DML 影响行数
        Scalar,            // 单值查询结果
        ElapsedMilliseconds, // 执行耗时
        Sql                // 执行的 SQL 字符串
    }
}
