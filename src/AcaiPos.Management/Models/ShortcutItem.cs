using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AcaiPos.Management.Models;

public partial class ShortcutItem : ObservableObject
{
    public required string Title { get; init; }

    public IReadOnlyList<ShortcutItem> Children { get; init; } = [];

    public bool HasChildren => Children.Count > 0;

    [ObservableProperty]
    private bool _isExpanded;

    public string Chevron => HasChildren ? (IsExpanded ? "▾" : "▸") : string.Empty;

    partial void OnIsExpandedChanged(bool value) => OnPropertyChanged(nameof(Chevron));

    [RelayCommand]
    private void Toggle()
    {
        if (HasChildren)
            IsExpanded = !IsExpanded;
    }
}
