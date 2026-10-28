namespace AcaiPos.Desktop.Behaviors;

/// <summary>
/// Liga ou desliga os teclados flutuantes (numérico e alfabético).
/// O padrão é ligado (Touchscreen). A inicialização e a tela de configurações chamam <see cref="SetEnabled"/>.
/// </summary>
public static class FloatingKeyboards
{
    public static bool IsEnabled { get; private set; } = true;

    public static event EventHandler? IsEnabledChanged;

    public static void SetEnabled(bool enabled)
    {
        if (IsEnabled == enabled)
            return;

        IsEnabled = enabled;
        IsEnabledChanged?.Invoke(null, EventArgs.Empty);
    }
}
