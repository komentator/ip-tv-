using System.Windows;
using System.Windows.Controls;
using LibVLCSharp.Shared;

namespace IpTvPlayer.Views;

public partial class TracksDialog : Window
{
    private MediaPlayer? _mediaPlayer;
    private bool _loading;

    public TracksDialog()
    {
        InitializeComponent();
    }

    public void Load(MediaPlayer mp)
    {
        _mediaPlayer = mp;
        _loading = true;

        AudioBox.Items.Clear();
        foreach (var t in mp.AudioTrackDescription)
        {
            AudioBox.Items.Add(new TrackItem { Id = t.Id, Name = t.Name ?? $"Track {t.Id}" });
        }
        SelectById(AudioBox, mp.AudioTrack);

        SubBox.Items.Clear();
        SubBox.Items.Add(new TrackItem { Id = -1, Name = "Отключены" });
        foreach (var t in mp.SpuDescription)
        {
            SubBox.Items.Add(new TrackItem { Id = t.Id, Name = t.Name ?? $"Track {t.Id}" });
        }
        SelectById(SubBox, mp.Spu);

        _loading = false;
    }

    private static void SelectById(ComboBox box, int id)
    {
        for (int i = 0; i < box.Items.Count; i++)
        {
            if (box.Items[i] is TrackItem t && t.Id == id) { box.SelectedIndex = i; return; }
        }
        if (box.Items.Count > 0) box.SelectedIndex = 0;
    }

    private void AudioBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || _mediaPlayer == null) return;
        if (AudioBox.SelectedItem is TrackItem t) _mediaPlayer.SetAudioTrack(t.Id);
    }

    private void SubBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || _mediaPlayer == null) return;
        if (SubBox.SelectedItem is TrackItem t) _mediaPlayer.SetSpu(t.Id);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private class TrackItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public override string ToString() => Name;
    }
}
