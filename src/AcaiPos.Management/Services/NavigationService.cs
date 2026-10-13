using AcaiPos.Management.ViewModels;
using AcaiPos.Management.Views;

namespace AcaiPos.Management.Services;

public sealed class NavigationService : INavigationService
{
    public void ShowMain(string userName)
    {
        var window = Application.Current?.Windows.FirstOrDefault()
            ?? throw new InvalidOperationException("A janela principal ainda não está disponível.");

        window.Page = new MainPage(new MainViewModel(userName));
    }
}
