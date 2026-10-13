using AcaiPos.Management.Models;
using AcaiPos.Management.ViewModels;

namespace AcaiPos.Management.Views;

public partial class MainPage : ContentPage
{
	private static readonly Color MenuHover = Color.FromArgb("#2A3642");
	private static readonly Color MenuText = Color.FromArgb("#F4F1EC");
	private static readonly Color MenuOpen = Color.FromArgb("#222C36");

	private Button? _openCaption;
	private TopMenu? _openMenu;
	private double _lastWidth;
	private double _lastHeight;

	public MainPage(MainViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
		SizeChanged += OnPageSizeChanged;
	}

	private void OnPageSizeChanged(object? sender, EventArgs e)
	{
		if (MenuLayer.IsVisible
			&& (Math.Abs(Width - _lastWidth) > 0.5 || Math.Abs(Height - _lastHeight) > 0.5))
		{
			HideMenu();
		}

		_lastWidth = Width;
		_lastHeight = Height;
	}

	private void OnMenuCaptionClicked(object? sender, EventArgs e)
	{
		if (sender is not Button button || button.BindingContext is not TopMenu menu)
			return;

		if (MenuLayer.IsVisible && ReferenceEquals(_openMenu, menu))
		{
			HideMenu();
			return;
		}

		ShowMenu(button, menu);
	}

	private void OnDismissMenuTapped(object? sender, TappedEventArgs e) => HideMenu();

	private void ShowMenu(Button caption, TopMenu menu)
	{
		Highlight(caption);
		_openMenu = menu;
		BuildSubmenu(menu);

		var captionOffset = GetOffset(caption, Root);
		var menuHostOffset = GetOffset(MenuBarHost, Root);
		var menuBottom = menuHostOffset.Y + MenuBarHost.Height;
		var width = Math.Max(180, menu.Items.Max(item => item.Title.Length) * 8.0 + 48);
		var height = (menu.Items.Count * 34) + 8;
		var x = captionOffset.X;
		if (x + width > Root.Width)
			x = Math.Max(8, Root.Width - width - 8);

		var y = captionOffset.Y + caption.Height;
		AbsoluteLayout.SetLayoutFlags(MenuDismiss, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.None);
		AbsoluteLayout.SetLayoutBounds(MenuDismiss, new Rect(0, menuBottom, Root.Width, Math.Max(0, Root.Height - menuBottom)));
		AbsoluteLayout.SetLayoutFlags(SubmenuPanel, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.None);
		AbsoluteLayout.SetLayoutBounds(SubmenuPanel, new Rect(x, y, width, height));
		MenuLayer.IsVisible = true;
	}

	private void BuildSubmenu(TopMenu menu)
	{
		SubmenuItems.Children.Clear();
		foreach (var item in menu.Items)
		{
			var row = new Border
			{
				BackgroundColor = Colors.Transparent,
				StrokeThickness = 0,
				Padding = new Thickness(12, 7),
				HorizontalOptions = LayoutOptions.Fill,
				Content = new Label
				{
					Text = item.Title,
					TextColor = MenuText,
					FontSize = 13,
					HorizontalTextAlignment = TextAlignment.Start,
					VerticalTextAlignment = TextAlignment.Center
				}
			};

			var pointer = new PointerGestureRecognizer();
			pointer.PointerEntered += (_, _) => row.BackgroundColor = MenuHover;
			pointer.PointerExited += (_, _) => row.BackgroundColor = Colors.Transparent;
			row.GestureRecognizers.Add(pointer);

			var tap = new TapGestureRecognizer();
			tap.Tapped += (_, _) => HideMenu();
			row.GestureRecognizers.Add(tap);
			SubmenuItems.Children.Add(row);
		}
	}

	private void Highlight(Button caption)
	{
		if (_openCaption is not null)
			_openCaption.BackgroundColor = Colors.Transparent;

		_openCaption = caption;
		caption.BackgroundColor = MenuOpen;
	}

	private void HideMenu()
	{
		MenuLayer.IsVisible = false;
		SubmenuItems.Children.Clear();
		_openMenu = null;
		if (_openCaption is not null)
			_openCaption.BackgroundColor = Colors.Transparent;

		_openCaption = null;
	}

	private static Point GetOffset(VisualElement element, VisualElement relativeTo)
	{
		if (element.Handler?.PlatformView is not Microsoft.UI.Xaml.UIElement from
			|| relativeTo.Handler?.PlatformView is not Microsoft.UI.Xaml.UIElement to)
		{
			return new Point(0, 0);
		}

		var point = from.TransformToVisual(to).TransformPoint(new global::Windows.Foundation.Point(0, 0));
		return new Point(point.X, point.Y);
	}
}
