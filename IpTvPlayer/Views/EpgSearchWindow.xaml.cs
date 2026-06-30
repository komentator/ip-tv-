using System.Windows;
using System.Windows.Input;
using IpTvPlayer.Models;
using IpTvPlayer.Services.Storage;

namespace IpTvPlayer.Views;

public partial class EpgSearchWindow : Window
{
    private readonly StorageService _storage;
    public Action<string>? PlayByTvgId { get; set; }

    public EpgSearchWindow(StorageService storage)
    {
        _storage = storage;
        InitializeComponent();
        Loaded += (_, _) => QueryBox.Focus();
    }

    private async void Search_Click(object sender, RoutedEventArgs e) => await DoSearch();

    private async void QueryBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) await DoSearch();
    }

    private async Task DoSearch()
    {
        var q = QueryBox.Text.Trim();
        if (q.Length < 2)
        {
            StatusText.Text = "Минимум 2 символа";
            return;
        }
        StatusText.Text = "Поиск...";
        var results = await _storage.SearchEpgAsync(q, 200);
        ResultsList.ItemsSource = results;
        StatusText.Text = $"Найдено: {results.Count}";
    }

    private void ResultsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ResultsList.SelectedItem is EpgProgram p && PlayByTvgId != null)
        {
            PlayByTvgId(p.ChannelTvgId);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
