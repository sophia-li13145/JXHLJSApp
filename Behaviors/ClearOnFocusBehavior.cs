using Microsoft.Maui.Controls;

namespace IndustrialControlMAUI.Behaviors
{
    public class ClearOnFocusBehavior : Behavior<Entry>
    {
        private string? _original;

        protected override void OnAttachedTo(Entry bindable)
        {
            base.OnAttachedTo(bindable);
            bindable.Focused += OnFocused;
            bindable.Unfocused += OnUnfocused;
        }

        protected override void OnDetachingFrom(Entry bindable)
        {
            base.OnDetachingFrom(bindable);
            bindable.Focused -= OnFocused;
            bindable.Unfocused -= OnUnfocused;
        }

        private void OnFocused(object? sender, FocusEventArgs e)
        {
            if (sender is not Entry entry) return;

            // 记录当前显示的值，然后清空，达到“点击就隐藏”的效果
            _original = entry.Text;
            entry.Text = string.Empty;
        }

        private void OnUnfocused(object? sender, FocusEventArgs e)
        {
            if (sender is not Entry entry) return;

            // 如果用户什么都没输就离开，恢复原来的值
            if (string.IsNullOrWhiteSpace(entry.Text))
            {
                entry.Text = _original;
            }
        }
    }
}
