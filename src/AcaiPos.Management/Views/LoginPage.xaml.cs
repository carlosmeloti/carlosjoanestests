using AcaiPos.Management.ViewModels;

namespace AcaiPos.Management.Views;

public partial class LoginPage : ContentPage
{
	public LoginPage(LoginViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}

	private void OnUserCompleted(object? sender, EventArgs e) => PasswordEntry.Focus();
}
