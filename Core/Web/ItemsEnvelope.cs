namespace Platform.Common.Web;

/// <summary>
/// 列表接口统一 shape：<c>{ items, total, page?, page_size? }</c>，
/// 通常作为 <see cref="ApiEnvelope{T}"/> 的 <c>data</c> 字段返回。
/// </summary>
public sealed class ItemsEnvelope<T>
{
    public T[] Items { get; init; } = [];
    public int Total { get; init; }
    public int? Page { get; init; }
    public int? PageSize { get; init; }
}
