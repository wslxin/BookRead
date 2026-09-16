namespace BookRead.Models;

/// <summary>
/// 表示书籍内容无法读取或解析，并携带可直接展示给用户的错误信息。
/// </summary>
internal sealed class BookContentLoadException : Exception
{
    /// <summary>
    /// 使用指定错误信息初始化书籍加载异常。
    /// </summary>
    /// <param name="message">可直接展示给用户的错误信息。</param>
    public BookContentLoadException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// 使用指定错误信息和内部异常初始化书籍加载异常。
    /// </summary>
    /// <param name="message">可直接展示给用户的错误信息。</param>
    /// <param name="innerException">导致书籍加载失败的基础异常。</param>
    public BookContentLoadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
