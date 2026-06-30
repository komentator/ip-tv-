using System.Windows;
using System.Windows.Controls;
using IpTvPlayer.Models;
using IpTvPlayer.ViewModels;
using Microsoft.Win32;

namespace IpTvPlayer;

public partial class MainWindow : Window
{
    private MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;

        _viewModel.PlaybackStateChanged += ViewModel_PlaybackStateChanged;

        PlaylistsList.ItemsSource = _viewModel.Playlists;
        ChannelsList.ItemsSource = _viewModel.CurrentChannels;
        GroupFilter.ItemsSource = _viewModel.Groups;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel.MediaPlayer != null)
        {
            VideoView.MediaPlayer = _viewModel.MediaPlayer;
        }
    }

    private void ViewModel_PlaybackStateChanged(object? sender, IpTvPlayer.Services.Playback.PlaybackStateChangedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            if (e.IsPlaying && e.Channel != null)
            {
                VideoPlaceholder.Visibility = Visibility.Collapsed;
                TitleInfo.Text = e.Channel.Name;
            }
            else if (!string.IsNullOrEmpty(e.Error))
            {
                VideoPlaceholder.Text = $"Ошибка: {e.Error}";
                VideoPlaceholder.Visibility = Visibility.Visible;
            }
            else
            {
                VideoPlaceholder.Text = "Выберите канал для воспроизведения";
                VideoPlaceholder.Visibility = Visibility.Visible;
                TitleInfo.Text = "";
            }
        });
    }

    private void ImportPlaylist_Click(object sender, RoutedEventArgs e)
    {
        var importWindow = new IpTvPlayer.Views.ImportWindow { Owner = this };

        if (importWindow.ShowDialog() == true)
        {
            if (importWindow.IsUrlImport && !string.IsNullOrWhiteSpace(importWindow.PlaylistUrl))
            {
                ImportFromUrlAsync(importWindow.PlaylistName!, importWindow.PlaylistUrl!);
            }
        }
        else
        {
            var fileDialog = new OpenFileDialog
            {
                Filter = "M3U файлы (*.m3u;*.m3u8)|*.m3u;*.m3u8|Все файлы (*.*)|*.*",
                Title = "Импортировать плейлист"
            };

            if (fileDialog.ShowDialog() == true)
            {
                var fileName = System.IO.Path.GetFileNameWithoutExtension(fileDialog.FileName);
                ImportFromFileAsync(fileDialog.FileName, fileName);
            }
        }
    }

    private async void ImportFromUrlAsync(string name, string url)
    {
        await _viewModel.ImportPlaylistFromUrlAsync(url, name);
    }

    private async void ImportFromFileAsync(string path, string name)
    {
        await _viewModel.ImportPlaylistFromFileAsync(path, name);
    }

    private void Playlist_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PlaylistsList.SelectedItem is Playlist playlist)
        {
            _viewModel.SelectPlaylist(playlist);
            ChannelsList.ItemsSource = _viewModel.CurrentChannels;
            GroupFilter.ItemsSource = _viewModel.Groups;
            GroupFilter.SelectedIndex = 0;
        }
    }

    private void Channel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ChannelsList.SelectedItem is Channel channel)
        {
            _viewModel.SelectChannel(channel);
        }
    }

    private void Search_TextChanged(object sender, TextChangedEventArgs e)
    {
        _viewModel.SearchChannels(SearchBox.Text);
    }

    private void Group_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GroupFilter.SelectedItem is string group)
        {
            _viewModel.FilterByGroup(group);
        }
    }

    private void Play_Click(object sender, RoutedEventArgs e)
    {
        if (ChannelsList.SelectedItem is Channel channel)
        {
            _viewModel.SelectChannel(channel);
        }
    }

    private void Pause_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.PlayPause();
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.Stop();
    }

    private void Volume_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _viewModel.SetVolume((int)e.NewValue);
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel?.Dispose();
        base.OnClosed(e);
    }
}
