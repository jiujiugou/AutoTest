namespace AutoTest.Application.Dto
{
    public class DbAssertionDto
    {
        public Guid Id { get; set; }
        public string Field { get; set; } = null!;           // RowValue / AffectedRows / Scalar / ...
        public string Operator { get; set; } = null!;        // Equal / NotEqual / GreaterThan ...
        public string ColumnName { get; set; } = ""; // RowValue 专用
        public int RowIndex { get; set; } = 0;       // RowValue 专用
        public string Expected { get; set; } = null!;
    }
}
