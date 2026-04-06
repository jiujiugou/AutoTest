using System;
using System.Collections.Generic;
using System.Text;

namespace AutoTest.Application.Dto
{
    public class DbAssertionDto
    {
        public Guid Id { get; set; }
        public string Field { get; set; }           // RowValue / AffectedRows / Scalar / ...
        public string Operator { get; set; }        // Equal / NotEqual / GreaterThan ...
        public string ColumnName { get; set; } = ""; // RowValue 专用
        public int RowIndex { get; set; } = 0;       // RowValue 专用
        public string Expected { get; set; } = null!;
    }
}
