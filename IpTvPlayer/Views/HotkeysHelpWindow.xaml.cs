using System.Windows;

namespace IpTvPlayer.Views;

public partial class HotkeysHelpWindow : Window
{
    public HotkeysHelpWindow()
    {
        InitializeComponent();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
