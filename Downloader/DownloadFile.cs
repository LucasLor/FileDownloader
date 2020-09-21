using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace Downloader
{
    public class DownloadFile
    {
        #region Property and Fields

        private int count = 0;
        private long lastNotificationDownloadedSize;
        private DateTime lastNotificationTime;
        private List<int> downloadRates = new List<int>();
        private int recentAverageRate;
        private bool speedChange = false;
        private int maxSpeed = 0;
        private long startByte = 0;
        private Stopwatch enlapsedTime = new Stopwatch();

        // File Url to Download
        public string Url { get; set; } = string.Empty;

        // Path to Save Download
        public string FilePath { get; set; } = string.Empty;

        // Attemp Download
        public int Retry { get; set; } = 5;
        public int TimeOut { get; set; } = 20000;
        public bool Replace { get; set; } = true;
        public bool IsPaused { get => _pause; }

        // Can pick up where you left off 
        public bool SupportsRange { get; private set; }
        public long FileSize { get; private set; }
        public bool HasError { get; private set; }
        public int DownloadedSize { get; private set; }
        public int DownloadSpeed { get; private set; }
        public int RefreshRate { get; set; } = 1000;
        public float Progress { get; private set; } = 0F;
        public int MaxSpeed
        {
            get => maxSpeed;
            set
            {
                speedChange = true;
                maxSpeed = value;
            }
        }

        public TimeSpan TimeLeft
        {
            get
            {
                if (recentAverageRate == 0)
                    return TimeSpan.Zero;

                var leftTime = (FileSize - DownloadedSize) / recentAverageRate;
                return TimeSpan.FromSeconds(leftTime);
            }
        }

        public TimeSpan DownloadedTime
        {
            get => enlapsedTime.Elapsed; // TimeSpan.FromMilliseconds(enlapsedTime.ElapsedMilliseconds);
        }

        #endregion

        public async Task Down()
        {
            try
            {
                CheckUrl();

                if (HasError)
                    return;

                if (File.Exists(FilePath))
                {
                    startByte = SupportsRange ? new FileInfo(FilePath).Length : 0;
                    DownloadedSize = (int)startByte;
                }

                // Refresh Downlaod Speed, Downloaded Size, Timeleft
                Timer t = new Timer(UpdatePropertys, null, 0, RefreshRate);

                //DownloadingTime = new Timer((x) => enlapsedTime += 100, null, 0, 100);

                // Download
                StartDownload?.Invoke(this, EventArgs.Empty);
                await HttpDownloadFile(Url, FilePath);

                // Stop Refresh
                t.Dispose();

                // Stop Downloaded Time
                //DownloadingTime.Dispose();

                // Reset propertys and actualize last Info
                ResetProperty();

                if (File.Exists(FilePath))
                {
                    //FileInfo fi = new FileInfo(savePath);
                    //fi.MoveTo(Path.GetDirectoryName(savePath) + "\\" + Path.GetFileNameWithoutExtension(savePath) + out);
                }
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("404"))
                {
                    HasError = true;
                    DownloadError?.Invoke(this, EventArgs.Empty);
                }
                else if (count++ < Retry)
                {
                    await Task.Delay(2000);
                    await StartAsync();
                }
            }

            if (!HasError)
                FinishDownload?.Invoke(this, EventArgs.Empty);
        }

        public async Task StartAsync()
        {
            if (File.Exists(FilePath) && Replace && !_pause)
            {
                File.Delete(FilePath);
            }

            _pause = false;
            HasError = false;
            await Down();
        }

        public void Pause()
        {
            _pause = true;
        }

        public void Stop()
        {
            _stop = true;
        }

        private void UpdatePropertys(object x)
        {
            Progress = (float)DownloadedSize / (float)FileSize * 100F;

            CalculateDownloadSpeed();
            CalculateAverageRate();

            var d = new DownloadData
            {
                DownloadedSize = DownloadedSize,
                LeftTime = TimeLeft,
                Speed = DownloadSpeed,
                Percent = Progress
            };

            DownloadChange?.Invoke(this, d);
        }

        private void ResetProperty()
        {
            lastNotificationDownloadedSize = 0;
            lastNotificationTime = DateTime.MinValue;
            downloadRates.Clear();
            recentAverageRate = 0;
            count = 0;
            DownloadSpeed = 0;

            CalculateAverageRate();
            CalculateDownloadSpeed();
            DownloadChange?.Invoke(this, new DownloadData { DownloadedSize = DownloadedSize, LeftTime = TimeLeft, Speed = DownloadSpeed });
        }

        private async Task HttpDownloadFile(string url, string path)
        {
            try
            {
                HttpWebRequest request = WebRequest.Create(url) as HttpWebRequest;
                request.Timeout = TimeOut;
                request.ReadWriteTimeout = TimeOut; //important
                request.AllowAutoRedirect = true;
                request.KeepAlive = false;
                request.Method = "GET";

                request.AddRange(startByte);

                long totalLen = 0;
                long downLen = 0;

                // Start Download Cont Time
                enlapsedTime.Start();

                using (var response = await request.GetResponseAsync())
                {
                    using (var responseStream = response.GetResponseStream())
                    {
                        // Limite Download Speed
                        ThrottledStream throttledStream = new ThrottledStream(responseStream, MaxSpeed <= 0 ? ThrottledStream.Infinite : MaxSpeed);

                        using (var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                        {
                            totalLen = response.ContentLength;
                            byte[] bArr = new byte[4096];
                            int size = await throttledStream.ReadAsync(bArr, 0, bArr.Length); //responseStream.Read(bArr, 0, (int)bArr.Length);

                            while (size > 0)
                            {
                                if (speedChange)
                                {
                                    throttledStream.MaximumBytesPerSecond = MaxSpeed <= 0 ? ThrottledStream.Infinite : MaxSpeed;
                                    speedChange = false;
                                }
                                if (_pause || _stop)
                                {
                                    break;
                                }
                                await stream.WriteAsync(bArr, 0, size);
                                downLen += size;
                                DownloadedSize += size;
                                DownloadSizeChange?.Invoke(this, size);
                                size = await throttledStream.ReadAsync(bArr, 0, bArr.Length); // responseStream.Read(bArr, 0, (int)bArr.Length);
                            }
                            await stream.FlushAsync();
                            
                            // Pause Downloading cont time
                            enlapsedTime.Stop();
                        }
                    }
                }

            }
            catch (Exception)
            {
                HasError = true;
                DownloadError?.Invoke(this, EventArgs.Empty);
            }
        }

        // Check URL to get file size, check if the server supports the Range header
        private void CheckUrl()
        {
            try
            {
                var webRequest = (HttpWebRequest)WebRequest.Create(this.Url);
                webRequest.Method = "HEAD";
                webRequest.Timeout = 5000;
                webRequest.Credentials = CredentialCache.DefaultCredentials;
                webRequest.Proxy = WebRequest.DefaultWebProxy;

                using (WebResponse response = webRequest.GetResponse())
                {
                    foreach (var header in response.Headers.AllKeys)
                    {
                        if (header.Equals("Accept-Ranges", StringComparison.OrdinalIgnoreCase))
                        {
                            this.SupportsRange = true;
                        }
                    }

                    this.FileSize = response.ContentLength;

                    if (this.FileSize <= 0)
                        throw new Exception();
                }
            }
            catch (Exception)
            {
                this.HasError = true;
                DownloadError?.Invoke(this, EventArgs.Empty);
            }
        }

        // Calculate download speed
        private void CalculateDownloadSpeed()
        {
            DateTime now = DateTime.UtcNow;
            TimeSpan interval = now - lastNotificationTime;
            double timeDiff = interval.TotalSeconds;
            double sizeDiff = (double)(DownloadedSize - lastNotificationDownloadedSize);

            DownloadSpeed = (int)Math.Floor(sizeDiff / timeDiff);

            downloadRates.Add(DownloadSpeed);

            lastNotificationDownloadedSize = DownloadedSize;
            lastNotificationTime = now;
        }

        // Calculate average download speed in the last 10 seconds
        private void CalculateAverageRate()
        {
            if (downloadRates.Count > 0)
            {
                if (downloadRates.Count > 10)
                    downloadRates.RemoveAt(0);

                int rateSum = 0;
                //recentAverageRate = 0;
                foreach (int rate in downloadRates)
                {
                    rateSum += rate;
                }

                recentAverageRate = rateSum / downloadRates.Count;
            }
        }


        public delegate void downloadChange(object sender, DownloadData size);
        public event downloadChange DownloadChange;

        public delegate void downloadSizeChange(object sender, int size);
        public event downloadSizeChange DownloadSizeChange;

        public EventHandler DownloadError;
        public EventHandler StartDownload;
        public EventHandler FinishDownload;
        private bool _pause;
        private bool _stop;
    }

    public struct DownloadData
    {
        public int Speed { get; internal set; }
        public int DownloadedSize { get; internal set; }
        public TimeSpan LeftTime { get; internal set; }
        public float Percent { get; internal set; }
    }
}
