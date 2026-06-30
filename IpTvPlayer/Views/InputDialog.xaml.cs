using System.Windows;

namespace IpTvPlayer.Views;

public partial class InputDialog : Window
{
    public string Value
    {
        get => InputBox.Text;
        set => InputBox.Text = value;
    }

    public string Prompt
    {
        get => PromptText.Text;
        set => PromptText.Text = value;
    }

    public InputDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => { InputBox.Focus(); InputBox.SelectAll(); };
    }

    public static string? Ask(Window? owner, string title, string prompt, string initial = "")
    {
        var dlg = new InputDialog { Owner = owner, Title = title, Prompt = prompt, Value = initial };
        return dlg.ShowDialog() == true ? dlg.Value : null;
    }

    private void Ok_Click(object sender, RoutedEventArgs e) { DialogResult = true; Close(); }
    private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
}
