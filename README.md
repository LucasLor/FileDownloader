# FileDownloader
Download files with HttpWebRequest 

How to use
```
 private async void DownloadStart()
{
	var downloader = new DownloadFile()
	{
		Url = "url",
		FilePath = "C:/.../file.zip",
		MaxSpeed = 51512 // Speed Limit: 0 == No Limit
	};
	await downloader.StartAsync();
}

```
