using System;
using asyvix.ExNet.Core.Security.Cryptography.Hash;
/**
* 만든이 : Asyvix
* 시간 = 2018-4-26
*
* DownloadMultipleFiles에서 쓸 용도와 ExNet.PackageManager에서 쓸 용도인 DownloadItem.
**/
namespace asyvix.ExNet.Core.Network.Downloader
{
    public class DownloadItem
    {
        public DownloadItem(string uri, string path, string fileName = "AutoDetect", bool useTempPath = false, string tempPath = "null")
        {
            Path = path;
            if (fileName == "AutoDetect")
            {
                FileName = System.IO.Path.GetFileName(path);
            }
            else
            {
                FileName = fileName;
            }
            DownloadUri = new Uri(uri);
            TempPath = tempPath;
            UseTempPath = useTempPath;
        }
        public DownloadItem(Uri uri, string path, string fileName = "AutoDetect", bool useTempPath = false, string tempPath = "null")
        {
            Path = path;
            if (fileName == "AutoDetect")
            {
                FileName = System.IO.Path.GetFileName(path);
            }
            else
            {
                FileName = fileName;
            }
            DownloadUri = uri;
            TempPath = tempPath;
            UseTempPath = useTempPath;
        }

        public Uri DownloadUri { get; set; }

        public string FileName { get; set; }
        public bool UseTempPath { get; set; } = false;
        public string TempPath { get; set; }
        public string Path { get; set; }

        public bool UseFileIntegrityCheck { get; set; } = false;
        public FileHashingType HashType { get; set; } = FileHashingType.MD5;
        public string FileHash { get; set; }

        public bool UseAutoRedownloadWhenFileIsCracked { get; set; }
    }

}
