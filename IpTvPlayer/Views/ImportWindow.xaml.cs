using System.Windows;

namespace IpTvPlayer.Views;

public partial class ImportWindow : Window
{
    public string? PlaylistName => PlaylistNameBox.Text;
    public string? PlaylistUrl => UrlBox.Text;
    public bool IsUrlImport { get; set; }

    public ImportWindow()
    {
        InitializeComponent();
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(PlaylistNameBox.Text))
        {
            MessageBox.Show("Введите название плейлиста", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(UrlBox.Text))
        {
            MessageBox.Show("Введите URL плейлиста", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsUrlImport = true;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
