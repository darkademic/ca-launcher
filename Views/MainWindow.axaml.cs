using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Styling;
using System.Threading.Tasks;

namespace CALauncher.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    public async Task<bool> ShowConfirmationDialogAsync(string title, string message)
    {
        var dialog = new Window
		{
			Title = title,
			Width = 380,
			Height = 150,
			WindowStartupLocation = WindowStartupLocation.CenterOwner,
			CanResize = false,
			ShowInTaskbar = false,
			Topmost = true,
			Background = Avalonia.Media.SolidColorBrush.Parse("#111")
        };

        var stackPanel = new StackPanel
        {
            Spacing = 15,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };

        var messageText = new TextBlock
        {
			Foreground = Avalonia.Media.SolidColorBrush.Parse("#DDD"),
            Text = message,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };

        var buttonPanel = new StackPanel
		{
			Orientation = Avalonia.Layout.Orientation.Horizontal,
			HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
			Spacing = 15,
			Margin = new Avalonia.Thickness(0, 0, 0, 10)
        };

        var yesButton = new Border
        {
            Width = 80,
            Height = 32,
            Background = Avalonia.Media.SolidColorBrush.Parse("#222"),
            CornerRadius = new Avalonia.CornerRadius(3),
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
            Child = new TextBlock
            {
                Text = "Yes",
                Foreground = Avalonia.Media.SolidColorBrush.Parse("#DDD"),
                FontSize = 16,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            }
        };

        var noButton = new Border
        {
            Width = 80,
            Height = 32,
            Background = Avalonia.Media.SolidColorBrush.Parse("#222"),
            CornerRadius = new Avalonia.CornerRadius(3),
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
            Child = new TextBlock
            {
                Text = "No",
                Foreground = Avalonia.Media.SolidColorBrush.Parse("#DDD"),
                FontSize = 16,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            }
        };

        // Add hover effects using Classes for proper styling
        yesButton.PointerEntered += (s, e) =>
        {
            if (s is Border btn)
            {
                btn.Background = Avalonia.Media.SolidColorBrush.Parse("#888");
            }
        };
        yesButton.PointerExited += (s, e) =>
        {
            if (s is Border btn)
            {
                btn.Background = Avalonia.Media.SolidColorBrush.Parse("#222");
            }
        };
        noButton.PointerEntered += (s, e) =>
        {
            if (s is Border btn)
            {
                btn.Background = Avalonia.Media.SolidColorBrush.Parse("#888");
            }
        };
        noButton.PointerExited += (s, e) =>
        {
            if (s is Border btn)
            {
                btn.Background = Avalonia.Media.SolidColorBrush.Parse("#222");
            }
        };

        bool result = false;

        yesButton.PointerPressed += (s, e) =>
        {
            result = true;
            dialog.Close();
        };

        noButton.PointerPressed += (s, e) =>
        {
            result = false;
            dialog.Close();
        };

        buttonPanel.Children.Add(yesButton);
        buttonPanel.Children.Add(noButton);

        stackPanel.Children.Add(messageText);
        stackPanel.Children.Add(buttonPanel);

        dialog.Content = stackPanel;

        await dialog.ShowDialog(this);
        return result;
    }

    public async Task ShowInfoDialogAsync(string title, string message)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 800,
            Height = 600,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true,
            ShowInTaskbar = false,
            Topmost = true,
            Background = Avalonia.Media.SolidColorBrush.Parse("#111")
        };

        var mainPanel = new StackPanel
        {
            Margin = new Avalonia.Thickness(20),
            Spacing = 20
        };

        var scrollViewer = new ScrollViewer
        {
            Height = 500,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
        };

        var messageText = new TextBlock
        {
            Text = message,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Foreground = Avalonia.Media.SolidColorBrush.Parse("#DDD"),
            FontFamily = "Consolas, Courier New, monospace",
            FontSize = 12
        };

        scrollViewer.Content = messageText;

        var okButton = new Border
        {
            Width = 80,
            Height = 32,
            Background = Avalonia.Media.SolidColorBrush.Parse("#222"),
            BorderBrush = Avalonia.Media.SolidColorBrush.Parse("#444"),
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new Avalonia.CornerRadius(3),
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Child = new TextBlock
            {
                Text = "OK",
                Foreground = Avalonia.Media.SolidColorBrush.Parse("#DDD"),
                FontSize = 16,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            }
        };

        okButton.PointerEntered += (s, e) =>
        {
            if (s is Border btn)
            {
                btn.Background = Avalonia.Media.SolidColorBrush.Parse("#888");
                btn.BorderBrush = Avalonia.Media.SolidColorBrush.Parse("#AAA");
            }
        };
        okButton.PointerExited += (s, e) =>
        {
            if (s is Border btn)
            {
                btn.Background = Avalonia.Media.SolidColorBrush.Parse("#222");
                btn.BorderBrush = Avalonia.Media.SolidColorBrush.Parse("#444");
            }
        };

        okButton.PointerPressed += (s, e) => dialog.Close();

        mainPanel.Children.Add(scrollViewer);
        mainPanel.Children.Add(okButton);

        dialog.Content = mainPanel;

        await dialog.ShowDialog(this);
    }
}
