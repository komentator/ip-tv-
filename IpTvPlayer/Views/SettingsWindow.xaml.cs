using System.Windows;
using IpTvPlayer.Utilities;
using Microsoft.Win32;

namespace IpTvPlayer.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        var cfg = ConfigManager.Load();
        EpgUrlBox.Text = cfg.EpgUrl ?? "";
        VolumeSlider.Value = cfg.DefaultVolume;
        SnapshotDirBox.Text = cfg.SnapshotDir ?? "";
    }

    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (VolumeLabel != null) VolumeLabel.Text = ((int)e.NewValue).ToString();
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Выберите файл в папке для скриншотов",
            CheckFileExists = false,
            FileName = "Выбрать папку"
        };
        if (dlg.ShowDialog() == true)
        {
            var dir = System.IO.Path.GetDirectoryName(dlg.FileName);
            if (!string.IsNullOrEmpty(dir)) SnapshotDirBox.Text = dir;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var cfg = ConfigManager.Load();
        cfg.EpgUrl = EpgUrlBox.Text.Trim();
        cfg.DefaultVolume = (int)VolumeSlider.Value;
        cfg.SnapshotDir = SnapshotDirBox.Text.Trim();
        ConfigManager.Save(cfg);
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
