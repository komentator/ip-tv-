using System.Windows;
using IpTvPlayer.Models;

namespace IpTvPlayer.Views;

public partial class EpgScheduleWindow : Window
{
    public EpgScheduleWindow()
    {
        InitializeComponent();
    }

    public void Load(Channel channel, IEnumerable<EpgProgram> programs)
    {
        ChannelName.Text = channel.Name;
        var list = programs.ToList();
        if (list.Count == 0)
        {
            DateRange.Text = "Нет данных EPG для этого канала";
        }
        else
        {
            DateRange.Text = $"{list.First().StartTime:dd.MM HH:mm} — {list.Last().EndTime:dd.MM HH:mm}";
        }
        ProgramsList.ItemsSource = list;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
