namespace Application.Dto.Auctions
{
    public class PagedResultDto<T>
    {
        public List<T> Items { get; set; } = new();
        public string? NextCursor { get; set; }
    }
}
