namespace BookRead.Dialogs;

/// <summary>
/// 定义通用确认弹窗的显示内容和操作语义。
/// </summary>
/// <param name="Title">弹窗标题。</param>
/// <param name="Message">需要用户确认的主要信息。</param>
/// <param name="Detail">辅助说明；不需要时可为空。</param>
/// <param name="ConfirmText">确认按钮文案。</param>
/// <param name="CancelText">取消按钮文案。</param>
/// <param name="IsDestructive">确认操作是否具有删除等危险语义。</param>
/// <param name="IsAlert">是否为仅展示信息的提示弹窗；为 true 时隐藏取消按钮。</param>
/// <param name="AlternativeText">可选的第三个按钮文案；为空时隐藏。</param>
internal sealed record ConfirmationDialogOptions(
    string Title,
    string Message,
    string? Detail = null,
    string ConfirmText = "确认",
    string CancelText = "取消",
    bool IsDestructive = false,
    bool IsAlert = false,
    string? AlternativeText = null);

/// <summary>
/// 表示用户在通用确认弹窗中选择的操作结果。
/// </summary>
internal enum ConfirmationDialogResult
{
    /// <summary>用户选择主确认按钮或关闭弹窗。</summary>
    Confirm,

    /// <summary>用户选择备选按钮。</summary>
    Alternative,

    /// <summary>用户选择取消按钮。</summary>
    Cancel
}
