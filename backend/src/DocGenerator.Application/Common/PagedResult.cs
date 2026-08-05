namespace DocGenerator.Application.Common;

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; } = 1;
    public int PerPage { get; set; } = 20;
    public int TotalCount { get; set; }
    public int TotalPages => PerPage <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PerPage);
}
