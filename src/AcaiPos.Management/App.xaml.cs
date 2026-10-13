using AcaiPos.Management.Views;
using Microsoft.Extensions.DependencyInjection;

namespace AcaiPos.Management;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
		UserAppTheme = AppTheme.Dark;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var loginPage = IPlatformApplication.Current?.Services.GetRequiredService<LoginPage>()
			?? throw new InvalidOperationException("Não foi possível abrir a tela de login.");

		return new Window(loginPage)
		{
			Title = "AcaiPos Management",
			Width = 1280,
			Height = 800,
			MinimumWidth = 1100,
			MinimumHeight = 700
		};
	}
}
