using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using IpTvPlayer.Models;
using IpTvPlayer.Utilities;
using IpTvPlayer.ViewModels;
using Microsoft.Win32;

namespace IpTvPlayer;

public partial class MainWindow : Window
{
    private MainViewModel _viewModel;
    private bool _isFullscreen;
    private WindowState _prevState;
    private WindowStyle _prevStyle;
    private ResizeMode _prevResize;
    private int _lastVolume = 100;
    private bool _isMuted;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;

        _viewModel.PlaybackStateChanged += ViewModel_PlaybackStateChanged;
        _viewModel.CurrentProgramChanged += ViewModel_CurrentProgramChanged;
        _viewModel.Buffering += ViewModel_Buffering;
        _viewModel.StreamInfoChanged += ViewModel_StreamInfoChanged;
        _viewModel.ChannelsListUpdated += (s, e) => UpdateChannelCountText();

        PlaylistsList.ItemsSource = _viewModel.Playlists;
        ChannelsList.ItemsSource = _viewModel.CurrentChannels;
        GroupFilter.ItemsSource = _viewModel.Groups;

        RestoreWindowState();
    }

    private void RestoreWindowState()
    {
        var cfg = ConfigManager.Load();
        if (cfg.WindowWidth > 200) Width = cfg.WindowWidth;
        if (cfg.WindowHeight > 200) Height = cfg.WindowHeight;
        if (cfg.WindowLeft >= 0 && cfg.WindowTop >= 0)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = cfg.WindowLeft;
            Top = cfg.WindowTop;
        }
        if (cfg.WindowMaximized) WindowState = WindowState.Maximized;
        VolumeSlider.Value = cfg.DefaultVolume;
    }

    private void SaveWindowState()
    {
        var cfg = ConfigManager.Load();
        if (WindowState != WindowState.Maximized)
        {
            cfg.WindowWidth = (int)Width;
            cfg.WindowHeight = (int)Height;
            cfg.WindowLeft = (int)Left;
            cfg.WindowTop = (int)Top;
        }
        cfg.WindowMaximized = WindowState == WindowState.Maximized;
        ConfigManager.Save(cfg);
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel.MediaPlayer != null)
        {
            VideoView.MediaPlayer = _viewModel.MediaPlayer;
        }
        Focus();

        await Task.Delay(800);
        var cfg = ConfigManager.Load();
        if (!string.IsNullOrWhiteSpace(cfg.LastChannelId))
        {
            var ch = _viewModel.Playlists.SelectMany(p => p.Channels).FirstOrDefault(c => c.Id == cfg.LastChannelId);
            if (ch != null)
            {
                var pl = _viewModel.Playlists.FirstOrDefault(p => p.Channels.Contains(ch));
                if (pl != null) PlaylistsList.SelectedItem = pl;
                await Task.Delay(100);
                ChannelsList.SelectedItem = ch;
                _viewModel.SelectChannel(ch);
            }
        }
    }

    private void UpdateChannelCountText()
    {
        var visible = _viewModel.CurrentChannels.Count;
        var total = _viewModel.TotalChannelCount;
        ChannelCountText.Text = visible == total ? $"Каналов: {total}" : $"Найдено: {visible} из {total}";
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

    private void ViewModel_StreamInfoChanged(object? sender, string info)
    {
        Dispatcher.Invoke(() => StreamInfo.Text = info);
    }

    private void ViewModel_Buffering(object? sender, float cache)
    {
        Dispatcher.Invoke(() =>
        {
            if (cache >= 99.9f)
            {
                BufferOverlay.Visibility = Visibility.Collapsed;
            }
            else
            {
                BufferOverlay.Visibility = Visibility.Visible;
                BufferText.Text = $"Загрузка {cache:0}%";
                BufferProgress.Value = cache;
            }
        });
    }

    private void ViewModel_CurrentProgramChanged(object? sender, Models.EpgProgram? prog)
    {
        Dispatcher.Invoke(() =>
        {
            EpgInfo.Text = prog == null
                ? ""
                : $"📺 {prog.Title}  ({prog.TimeRange})";
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

    private async void EpgRefresh_Click(object sender, RoutedEventArgs e)
    {
        var cfg = ConfigManager.Load();
        if (string.IsNullOrWhiteSpace(cfg.EpgUrl))
        {
            var dlg = new Views.EpgUrlDialog { Owner = this, EpgUrl = cfg.EpgUrl ?? "" };
            if (dlg.ShowDialog() != true || string.IsNullOrWhiteSpace(dlg.EpgUrl)) return;
            cfg.EpgUrl = dlg.EpgUrl.Trim();
            ConfigManager.Save(cfg);
        }

        EpgInfo.Text = "📡 Обновляю EPG...";
        try
        {
            await _viewModel.RefreshEpgAsync(cfg.EpgUrl, force: true);
            EpgInfo.Text = "EPG обновлён";
        }
        catch (Exception ex)
        {
            EpgInfo.Text = $"Ошибка EPG: {ex.Message}";
        }
    }

    private void Playlist_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PlaylistsList.SelectedItem is Playlist playlist)
        {
            _viewModel.SelectPlaylist(playlist);
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

    private void ViewAll_Checked(object sender, RoutedEventArgs e) => _viewModel?.SetView(ChannelView.All);
    private void ViewFavorites_Checked(object sender, RoutedEventArgs e) => _viewModel?.SetView(ChannelView.Favorites);
    private void ViewRecent_Checked(object sender, RoutedEventArgs e) => _viewModel?.SetView(ChannelView.Recent);

    private void Sort_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_viewModel == null) return;
        if (SortBox.SelectedItem is ComboBoxItem item && item.Tag is string tag &&
            Enum.TryParse<ChannelSort>(tag, out var sort))
        {
            _viewModel.SetSort(sort);
        }
    }

    private async void Favorite_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Channel channel)
        {
            await _viewModel.ToggleFavoriteAsync(channel);
        }
        e.Handled = true;
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
        _viewModel?.SetVolume((int)e.NewValue);
        if ((int)e.NewValue > 0)
        {
            _isMuted = false;
            if (MuteButton != null) MuteButton.Content = "🔊";
            _lastVolume = (int)e.NewValue;
        }
    }

    private void Mute_Click(object sender, RoutedEventArgs e)
    {
        ToggleMute();
    }

    private void ToggleMute()
    {
        if (_isMuted)
        {
            VolumeSlider.Value = _lastVolume;
            MuteButton.Content = "🔊";
            _isMuted = false;
        }
        else
        {
            _lastVolume = (int)VolumeSlider.Value;
            VolumeSlider.Value = 0;
            MuteButton.Content = "🔇";
            _isMuted = true;
        }
    }

    private void Fullscreen_Click(object sender, RoutedEventArgs e) => ToggleFullscreen();

    private void Record_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.IsRecording)
        {
            _viewModel.StopRecording();
            RecordButton.Content = "⏺";
            RecordButton.Foreground = System.Windows.Media.Brushes.White;
            EpgInfo.Text = "⏹ Запись остановлена";
        }
        else
        {
            if (_viewModel.SelectedChannel == null)
            {
                EpgInfo.Text = "Сначала выбери канал";
                return;
            }
            var cfg = ConfigManager.Load();
            var dir = string.IsNullOrWhiteSpace(cfg.RecordingDir)
                ? System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Recordings")
                : cfg.RecordingDir;
            var path = _viewModel.StartRecording(dir);
            if (path != null)
            {
                RecordButton.Content = "⏹";
                RecordButton.Foreground = System.Windows.Media.Brushes.Red;
                EpgInfo.Text = $"⏺ Запись: {System.IO.Path.GetFileName(path)}";
            }
            else
            {
                EpgInfo.Text = "Не удалось начать запись";
            }
        }
    }

    private void Snapshot_Click(object sender, RoutedEventArgs e)
    {
        var mp = _viewModel.MediaPlayer;
        if (mp == null || !mp.IsPlaying)
        {
            EpgInfo.Text = "Нечего снимать — нет потока";
            return;
        }

        var cfg = ConfigManager.Load();
        var dir = string.IsNullOrWhiteSpace(cfg.SnapshotDir)
            ? System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Snapshots")
            : cfg.SnapshotDir;
        System.IO.Directory.CreateDirectory(dir);

        var name = (_viewModel.SelectedChannel?.Name ?? "snapshot").Replace(' ', '_');
        foreach (var c in System.IO.Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        var file = System.IO.Path.Combine(dir, $"{name}_{DateTime.Now:yyyyMMdd_HHmmss}.png");

        try
        {
            mp.TakeSnapshot(0, file, 0, 0);
            EpgInfo.Text = $"📸 Сохранено: {System.IO.Path.GetFileName(file)}";
        }
        catch (Exception ex)
        {
            EpgInfo.Text = $"Ошибка снимка: {ex.Message}";
        }
    }

    private void Tracks_Click(object sender, RoutedEventArgs e)
    {
        var mp = _viewModel.MediaPlayer;
        if (mp == null)
        {
            EpgInfo.Text = "Плеер не инициализирован";
            return;
        }
        var dlg = new Views.TracksDialog { Owner = this };
        dlg.Load(mp);
        dlg.ShowDialog();
    }

    private async void Schedule_Click(object sender, RoutedEventArgs e)
    {
        var channel = _viewModel.SelectedChannel;
        if (channel == null)
        {
            EpgInfo.Text = "Сначала выбери канал";
            return;
        }
        var from = DateTime.Now.AddHours(-2);
        var to = DateTime.Now.AddHours(24);
        var programs = await _viewModel.GetScheduleAsync(channel, from, to);
        var win = new Views.EpgScheduleWindow { Owner = this };
        win.Load(channel, programs);
        win.ShowDialog();
    }

    private void EpgSearch_Click(object sender, RoutedEventArgs e)
    {
        var win = new Views.EpgSearchWindow(_viewModel.Storage) { Owner = this };
        win.PlayByTvgId = tvgId =>
        {
            var ch = _viewModel.FindChannelByTvgId(tvgId);
            if (ch != null)
            {
                _viewModel.SelectChannel(ch);
                ChannelsList.SelectedItem = ch;
                win.Close();
            }
        };
        win.ShowDialog();
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var win = new Views.SettingsWindow { Owner = this };
        if (win.ShowDialog() == true)
        {
            var cfg = ConfigManager.Load();
            VolumeSlider.Value = cfg.DefaultVolume;
        }
    }

    private void VideoArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleFullscreen();
            e.Handled = true;
        }
    }

    private void ToggleFullscreen()
    {
        if (!_isFullscreen)
        {
            _prevState = WindowState;
            _prevStyle = WindowStyle;
            _prevResize = ResizeMode;

            TopBar.Visibility = Visibility.Collapsed;
            Sidebar.Visibility = Visibility.Collapsed;
            FilterBar.Visibility = Visibility.Collapsed;
            BottomBar.Visibility = Visibility.Collapsed;
            ChannelsList.Visibility = Visibility.Collapsed;
            ChannelsCol.Width = new GridLength(0);
            SidebarCol.Width = new GridLength(0);
            TopBarRow.Height = new GridLength(0);
            BottomBarRow.Height = new GridLength(0);

            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Maximized;

            _isFullscreen = true;
        }
        else
        {
            TopBar.Visibility = Visibility.Visible;
            Sidebar.Visibility = Visibility.Visible;
            FilterBar.Visibility = Visibility.Visible;
            BottomBar.Visibility = Visibility.Visible;
            ChannelsList.Visibility = Visibility.Visible;
            ChannelsCol.Width = new GridLength(350);
            SidebarCol.Width = new GridLength(250);
            TopBarRow.Height = new GridLength(50);
            BottomBarRow.Height = new GridLength(60);

            WindowState = _prevState;
            WindowStyle = _prevStyle;
            ResizeMode = _prevResize;

            _isFullscreen = false;
        }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.OriginalSource is TextBox) return;

        switch (e.Key)
        {
            case Key.Space:
                _viewModel.PlayPause();
                e.Handled = true;
                break;
            case Key.S:
                _viewModel.Stop();
                e.Handled = true;
                break;
            case Key.F:
            case Key.F11:
                ToggleFullscreen();
                e.Handled = true;
                break;
            case Key.Escape:
                if (_isFullscreen) ToggleFullscreen();
                e.Handled = true;
                break;
            case Key.F1:
                new Views.HotkeysHelpWindow { Owner = this }.ShowDialog();
                e.Handled = true;
                break;
            case Key.M:
                ToggleMute();
                e.Handled = true;
                break;
            case Key.Up:
                VolumeSlider.Value = Math.Min(100, VolumeSlider.Value + 5);
                e.Handled = true;
                break;
            case Key.Down:
                VolumeSlider.Value = Math.Max(0, VolumeSlider.Value - 5);
                e.Handled = true;
                break;
            case Key.Right:
                MoveChannel(1);
                e.Handled = true;
                break;
            case Key.Left:
                MoveChannel(-1);
                e.Handled = true;
                break;
        }
    }

    private void MoveChannel(int delta)
    {
        if (_viewModel.CurrentChannels.Count == 0) return;
        var idx = ChannelsList.SelectedIndex + delta;
        if (idx < 0) idx = _viewModel.CurrentChannels.Count - 1;
        if (idx >= _viewModel.CurrentChannels.Count) idx = 0;
        ChannelsList.SelectedIndex = idx;
        ChannelsList.ScrollIntoView(ChannelsList.SelectedItem);
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files != null && files.Any(IsM3UFile))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
                return;
            }
        }
        e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null) return;

            foreach (var file in files.Where(IsM3UFile))
            {
                var name = System.IO.Path.GetFileNameWithoutExtension(file);
                EpgInfo.Text = $"📥 Импорт {name}...";
                await _viewModel.ImportPlaylistFromFileAsync(file, name);
                EpgInfo.Text = $"✓ Импортирован {name}";
            }
        }
    }

    private static bool IsM3UFile(string path)
    {
        var ext = System.IO.Path.GetExtension(path);
        return string.Equals(ext, ".m3u", StringComparison.OrdinalIgnoreCase)
            || string.Equals(ext, ".m3u8", StringComparison.OrdinalIgnoreCase);
    }

    private async void PlaylistRefresh_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.Tag is Playlist pl)
        {
            EpgInfo.Text = $"🔄 Обновляю {pl.Name}...";
            await _viewModel.RefreshPlaylistAsync(pl);
            EpgInfo.Text = $"✓ {pl.Name} обновлён";
        }
    }

    private async void PlaylistRename_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.Tag is Playlist pl)
        {
            var newName = Views.InputDialog.Ask(this, "Переименовать", "Новое название плейлиста:", pl.Name);
            if (!string.IsNullOrWhiteSpace(newName))
                await _viewModel.RenamePlaylistAsync(pl, newName.Trim());
        }
    }

    private async void PlaylistDelete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.Tag is Playlist pl)
        {
            var result = MessageBox.Show($"Удалить плейлист \"{pl.Name}\"?", "Подтверждение",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
                await _viewModel.DeletePlaylistAsync(pl.Id);
        }
    }

    private void ChannelPlay_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.Tag is Channel ch)
            _viewModel.SelectChannel(ch);
    }

    private async void ChannelFavorite_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.Tag is Channel ch)
            await _viewModel.ToggleFavoriteAsync(ch);
    }

    private void ChannelCopyUrl_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.Tag is Channel ch)
        {
            try { Clipboard.SetText(ch.Url); EpgInfo.Text = "📋 URL скопирован"; }
            catch (Exception ex) { EpgInfo.Text = $"Ошибка копирования: {ex.Message}"; }
        }
    }

    private void ChannelCopyName_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.Tag is Channel ch)
        {
            try { Clipboard.SetText(ch.Name); EpgInfo.Text = "📋 Название скопировано"; }
            catch (Exception ex) { EpgInfo.Text = $"Ошибка копирования: {ex.Message}"; }
        }
    }

    private async void ChannelSchedule_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.Tag is Channel ch)
        {
            var from = DateTime.Now.AddHours(-2);
            var to = DateTime.Now.AddHours(24);
            var programs = await _viewModel.GetScheduleAsync(ch, from, to);
            var win = new Views.EpgScheduleWindow { Owner = this };
            win.Load(ch, programs);
            win.ShowDialog();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        try { SaveWindowState(); } catch { }
        _viewModel?.Dispose();
        base.OnClosed(e);
    }
}
