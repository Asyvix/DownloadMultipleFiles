using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading.Tasks;
/**
* 만든이 : Asyvix
* 시간 = 2018-4-27
*
* WebClient의 DownloadFileAsync가 UserToken이 Object형식으로 받아오기때문에 캐스팅에 문제가 있는것을 해결하고
 * 이벤트들을 더 사용하기 편하도록 만듬. 덕분에 성능은 더 쓰지만.
**/
namespace asyvix.ExNet.Core.Network.Downloader
{

    public class DownloadClient<T> : WebClient
    {
        public delegate void DownloadCompletedHandler(DownloadClient<T> sender, DownloadCompletedArgs args, T value);

        public new event DownloadCompletedHandler DownloadFileCompleted;

        public delegate void DownloadProgressChangedHandler(DownloadClient<T> sender, DownloadProgressChangedArgs args, T value);

        public new event DownloadProgressChangedHandler DownloadProgressChanged;

        public DownloadClient(int clientId, long raiseEventLimit)
        {
            ClientId = clientId;
            LimitRaiseEvent = raiseEventLimit;
            
            base.DownloadFileCompleted += RaiseDownloadCompleted;
            base.DownloadProgressChanged += LimitRaiseDownloadProgressChanged;
        }
        public DownloadClient(int clientId)
        {
            ClientId = clientId;
            base.DownloadFileCompleted += RaiseDownloadCompleted;
            base.DownloadProgressChanged += RaiseDownloadProgressChanged;
        }
        public DownloadClient()
        {
            ClientId = -1;
            base.DownloadFileCompleted += RaiseDownloadCompleted;
            base.DownloadProgressChanged += RaiseDownloadProgressChanged;
        }
        public async void DownloadFileAsync(Uri address, string filePath, T userToken)
        {
            if (NowDownloading)
            {
                throw new Exception("다른 파일을 다운로드 중입니다.");               
            }
            TotalSize = 0;
            ReceivedBytes = 0;
            UserToken = userToken;
            FilePath = filePath;
            while (true)
            {
                try
                {
                    string folderpath = Path.GetDirectoryName(filePath);
                    if (!System.IO.Directory.Exists(folderpath))
                    {
                        DirectoryInfo di = Directory.CreateDirectory(folderpath);
                    }
                    base.DownloadFileAsync(address, filePath);
                    _culcDownloadSpeedStopwatch.Start();
                    _limitRaiseEventSw.Start();
                    NowDownloading = true;
                    break;
                }
                catch (NotSupportedException)
                {
                    Console.WriteLine("캐치됨");
                    await Task.Delay(30);
                }
                catch (Exception ex)
                {
                    throw;
                }
            }
        }


        public void DownloadFile(Uri address, string filePath, T userToken)
        {
            TotalSize = 0;
            ReceivedBytes = 0;
            UserToken = userToken;
            FilePath = filePath;
            base.DownloadFile(address, filePath);
        }

        public bool NowDownloading { get; private set; } = false;
        public T UserToken { get; private set; }
        public string FilePath { get; private set; }
        public long TotalSize { get; private set; }
        public long ReceivedBytes { get; private set; }

        public int ClientId { get; private set; }

        public long LimitRaiseEvent { get; private set; }

        Stopwatch _culcDownloadSpeedStopwatch = new Stopwatch();
        Stopwatch _limitRaiseEventSw = new Stopwatch();

        private void RaiseDownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            ReceivedBytes = e.BytesReceived;
            TotalSize = e.TotalBytesToReceive;

            DownloadProgressChangedArgs args = new DownloadProgressChangedArgs();
            args.ReceivedBytes = ReceivedBytes;
            args.ProgressPercentage = e.ProgressPercentage;
            args.TotalBytesToBeRecieved = TotalSize;
            args.DownloadSpeed = string.Format("{0} kB/s", (e.BytesReceived / 1024d / _culcDownloadSpeedStopwatch.Elapsed.TotalSeconds).ToString("0.00"));
            DownloadProgressChanged?.Invoke(this, args, UserToken);
        }

        private void LimitRaiseDownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            if (_limitRaiseEventSw.ElapsedMilliseconds < LimitRaiseEvent)
            {
                if (e.ProgressPercentage != 100)
                {
                    return;
                }
            }
            ReceivedBytes = e.BytesReceived;
            TotalSize = e.TotalBytesToReceive;

            DownloadProgressChangedArgs args = new DownloadProgressChangedArgs();
            args.ReceivedBytes = ReceivedBytes;
            args.ProgressPercentage = e.ProgressPercentage;
            args.TotalBytesToBeRecieved = TotalSize;
            args.DownloadSpeed = string.Format("{0} kB/s", (e.BytesReceived / 1024d / _culcDownloadSpeedStopwatch.Elapsed.TotalSeconds).ToString("0.00"));
            DownloadProgressChanged?.Invoke(this, args, UserToken);

            _limitRaiseEventSw.Restart();
        }

        private void RaiseDownloadCompleted(object sender, AsyncCompletedEventArgs e)
        {
            _culcDownloadSpeedStopwatch.Reset();
            _limitRaiseEventSw.Reset();
            DownloadCompletedArgs args = new DownloadCompletedArgs();
            args.ReceivedBytes = TotalSize;
            NowDownloading = false;
            DownloadFileCompleted?.Invoke(this, args, UserToken);
        }

    }

    public class DownloadProgressChangedArgs
    {
        public long ReceivedBytes;
        public long TotalBytesToBeRecieved;
        public int ProgressPercentage;
        public string DownloadSpeed;
    }

    public class DownloadCompletedArgs
    {
        public long ReceivedBytes;
    }
}
