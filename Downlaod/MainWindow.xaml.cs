using Downloader;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace Downlaod
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private DownloadFile d;

        public MainWindow()
        {
            InitializeComponent();

            var link = "https://sv1.animeshd.org/u/uzaki-chan-wa-asobitai!/1080p/1.mp4";
            link = @"https://ffmpeg.zeranoe.com/builds/win64/static/ffmpeg-20200814-a762fd2-win64-static.zip";

            d = new DownloadFile()
            {
                Url = link,
                FilePath = "ffmpeg.zip",
                MaxSpeed = 51200
            };
            d.DownloadChange += D_DownloadChange;
        }

        private async void DownloadStart()
        {
            

            // Task.Run(()=> d.Start()).ConfigureAwait(true);
            await d.StartAsync();
        }
        private void D_DownloadChange(object sender, DownloadData size)
        {
            Dispatcher.Invoke(() => tbDownloadedSize.Text = FormatFileSize(size.DownloadedSize));
            Dispatcher.Invoke(() => tbSize.Text = FormatFileSize((sender as DownloadFile).FileSize));
            Dispatcher.Invoke(() => tbSpeed.Text = FormatFileSize((sender as DownloadFile).DownloadSpeed));
            Dispatcher.Invoke(() => tbLeft.Text = (sender as DownloadFile).TimeLeft.ToString());
            Dispatcher.Invoke(() => tbpercent.Text = (sender as DownloadFile).Progress.ToString("0.00"));
            Dispatcher.Invoke(() => tbDownloadingTime.Text = (sender as DownloadFile).DownloadedTime.ToString());

        }
        public static String FormatFileSize(long fileSize)
        {

            string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB" }; //Longs run out around EB
            if (fileSize == 0)
                return "0" + suf[0];
            long bytes = Math.Abs(fileSize);
            int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            double num = Math.Round(bytes / Math.Pow(1024, place), 1);
            return (Math.Sign(fileSize) * num).ToString() + suf[place];
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

            //Task.Run(()=>  DownloadStart());
            DownloadStart();

        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            d.Pause();
        }
    }
}
