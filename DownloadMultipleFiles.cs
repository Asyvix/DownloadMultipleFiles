using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using asyvix.ExNet.Core.Security.Cryptography.Hash;
/**
* 만든이 : Asyvix
* 시간 = 2018-4-27
*
* 여러개의 클라이언트로 여러개의 파일을 동시에 받는 기능을 하는 것.
**/
namespace asyvix.ExNet.Core.Network.Downloader
{
    public class DownloadMultipleFiles // DownloadClient<DownloadItem> 여러개만들어야함
    {
        private const int CLIENT_COUNT_MIN = 1;
        private const int CLIENT_COUNT_MAX = 10;
        public int ClientCount { get; private set; }

        public bool InProgress { get; private set; }

        private ConcurrentQueue<DownloadItem> _filequeue;
        private BlockingCollection<DownloadClient<DownloadItem>> _clients;
       
        public DownloadMultipleFiles(ConcurrentQueue<DownloadItem> filequeue, int clientcount = 1, bool autoRedownload = false)
        {
            if (clientcount < CLIENT_COUNT_MIN)
            {
                clientcount = CLIENT_COUNT_MIN;
            }
            if (clientcount > CLIENT_COUNT_MAX)
            {
                clientcount = CLIENT_COUNT_MAX;
            }

            ClientCount = clientcount;
            _filequeue = filequeue;
            DownloadToComplete = filequeue.Count;
            _clients = new BlockingCollection<DownloadClient<DownloadItem>>();
            for (int i = 0; i < clientcount; i++)
            {
                var client = new DownloadClient<DownloadItem>(i, 200);
                client.DownloadFileCompleted += OnDownloadCompleted;
                client.DownloadProgressChanged += OnDownloadProgressChanged;
                _clients.Add(client);
            }
        }

        public int DownloadToComplete { get; private set; }

        public int Downloaded { get; private set; } = 0;

        private Thread _downloadThread;

        public void DownloadStart()
        {
            if (!InProgress)
            {
                
                InProgress = true;
                if (_downloadThread == null)
                {
                    _downloadThread = new Thread(DownloadNextItem);
                    _downloadThread.Start();
                }
                
            }
            else
            {
                throw new Exception("in DownloadProgress...");
            }
        }

        public void EnqueueItem(DownloadItem item)
        {
            _filequeue.Enqueue(item);
            DownloadToComplete++;
            //if (!NowDownloading)
            //{
            //    DownloadStart();
            //}
        }
        private void OnDownloadProgressChanged(DownloadClient<DownloadItem> sender, DownloadProgressChangedArgs e, DownloadItem userToken)
        {
            var item = new FileDownloadProgressChangedArgs();
            item.ReceivedBytes = e.ReceivedBytes;
            item.ClientId = sender.ClientId;

            item.TotalBytesToBeRecieved = e.TotalBytesToBeRecieved;
            item.ProgressPercentage = e.ProgressPercentage;
            item.DownloadItem = userToken;
            item.DownloadSpeed = e.DownloadSpeed;
            RaiseDownloadProgressChanged(item);
        }


        public async void OnDownloadCompleted(DownloadClient<DownloadItem> sender, DownloadCompletedArgs e, DownloadItem userToken)
        {


            _clients.Add(sender); //클라이언트 큐에 추가


            Downloaded++; //다운로드된 아이템 추가(DownloadFileAsync는 비동기-동기화를 쓰기때문에 InterLocked를 쓸 필요가 없다.)

            DownloadItem di = userToken;
            FileDownloadCompletedArgs args = new FileDownloadCompletedArgs();
            args.DownloadedItem = di;
            args.DownloadQueue = _filequeue.Count;
            args.ClientId = sender.ClientId;
            args.Downloaded = Downloaded;
            args.DownloadToComplete = DownloadToComplete;
            args.ProgressPercentage = Math.Round((double)(100 * Downloaded) / DownloadToComplete);

            if (di.UseFileIntegrityCheck) // 파일 무결성 검사
            {
                args.DownloadedFileHash = await FileHashing.GetFileHashAsync(di.Path, di.HashType);
                if (di.FileHash != args.DownloadedFileHash)
                {
                    args.FileIntegrity = false;
                }
                else
                {
                    args.FileIntegrity = true;
                }
            }


            if (di.UseAutoRedownloadWhenFileIsCracked) //파일 깨졌다면 재다운로드
            {
                if (!args.FileIntegrity)
                {
                    if (di.UseTempPath)
                    {
                        File.Delete(di.TempPath);
                    }
                    else
                    {
                        File.Delete(di.Path);
                    }
                    EnqueueItem(userToken);
                }
            }

            if (di.UseTempPath)
            {
                string path = Path.GetDirectoryName(di.Path);
                if (!System.IO.Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
                string temppath = Path.GetDirectoryName(di.TempPath);
                if (!System.IO.Directory.Exists(temppath))
                {
                    Directory.CreateDirectory(temppath);
                }
                File.Delete(di.Path);
                File.Move(di.TempPath, di.Path);
            }
            RaiseDownloadCompleted(args);
            if (Downloaded == DownloadToComplete)
            {
                RaiseAllDownloadCompleted(new AllFileDownloadCompletedArgs());

            }
        }

        private void DownloadNextItem()
        {
            while (true)
            {
                DownloadClient<DownloadItem> client = _clients.Take();

                DownloadItem di;
                if (_filequeue.TryDequeue(out di))
                {
                    if (di.UseTempPath)
                    {
                        client.DownloadFileAsync(di.DownloadUri, di.TempPath, di);
                    }
                    else
                    {
                        client.DownloadFileAsync(di.DownloadUri, di.Path, di);
                    }
                    RaiseDownloadStarted(new FileDownloadStartedArgs { DownloadItem = di, ClientId = client.ClientId });
                }

                if (_filequeue.Count == 0)
                {
                    break;
                    
                }
            }
        }

        private void RaiseAllDownloadCompleted(AllFileDownloadCompletedArgs e)
        {
            AllFileDownloadCompleted?.Invoke(e);
        }

        private void RaiseDownloadCompleted(FileDownloadCompletedArgs e)
        {
            FileDownloadCompleted?.Invoke(e);
        }

        private void RaiseDownloadProgressChanged(FileDownloadProgressChangedArgs e)
        {
            FileDownloadProgressChanged?.Invoke(e);
        }

        private void RaiseDownloadStarted(FileDownloadStartedArgs e)
        {
            FileDownloadStarted?.Invoke(e);
        }


        public event AllFileDownloadCompletedHandler AllFileDownloadCompleted;
        public event FileDownloadCompletedHandler FileDownloadCompleted;
        public event FileDownloadStartedHandler FileDownloadStarted;
        public event FileDownloadProgressChangedHandler FileDownloadProgressChanged;

        public delegate void FileDownloadProgressChangedHandler(FileDownloadProgressChangedArgs dpe);
        public delegate void FileDownloadCompletedHandler(FileDownloadCompletedArgs dce);
        public delegate void AllFileDownloadCompletedHandler(AllFileDownloadCompletedArgs adce);
        public delegate void FileDownloadStartedHandler(FileDownloadStartedArgs dse);
    }

    public class FileDownloadProgressChangedArgs
    {

        public int ClientId;
        public long ReceivedBytes;
        public long TotalBytesToBeRecieved;
        public string DownloadSpeed;
        public int ProgressPercentage;
        public DownloadItem DownloadItem;

    }
    public class FileDownloadCompletedArgs
    {
        public int ClientId;
        public double ProgressPercentage;
        public int DownloadToComplete;
        public int Downloaded;
        public int DownloadQueue;

        public string TotalDownloadSpeed;
        public DownloadItem DownloadedItem;
        public bool FileIntegrity;
        public bool Redownload;
        public string DownloadedFileHash;
    }
    public class FileDownloadStartedArgs
    {
        public int ClientId;
        public DownloadItem DownloadItem;
    }
    public class AllFileDownloadCompletedArgs
    {
        public int TotalDownloadCount;
    }




}





