using System.Windows.Input;

namespace BookRead.Models;

/// <summary>
/// 描述一个快捷键及其修饰键组合。
/// </summary>
public sealed record ShortcutBinding(Key Key, ModifierKeys Modifiers);
