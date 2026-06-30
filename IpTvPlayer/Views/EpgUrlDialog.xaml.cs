using System.Windows;

namespace IpTvPlayer.Views;

public partial class EpgUrlDialog : Window
{
    public string EpgUrl
    {
        get => UrlBox.Text;
        set => UrlBox.Text = value;
    }

    public EpgUrlDialog()
    {
        InitializeComponent();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
