#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using IntPtr = UnityEngine.Networking.UnityWebRequest;

namespace Unity.Networking
{
    public class UnityWebRequestAwaiter : INotifyCompletion
    {
        private UnityWebRequestAsyncOperation asyncOp;
        private Action continuation;

        public UnityWebRequestAwaiter(UnityWebRequestAsyncOperation asyncOp)
        {
            this.asyncOp = asyncOp;
            asyncOp.completed += OnRequestCompleted;
        }

        public bool IsCompleted { get { return asyncOp.isDone; } }

        public void GetResult() { }

        public void OnCompleted(Action continuation)
        {
            this.continuation = continuation;
        }

        private void OnRequestCompleted(AsyncOperation obj)
        {
            continuation();
        }
    }

    public static class ExtensionMethods
    {
        public static UnityWebRequestAwaiter GetAwaiter(this UnityWebRequestAsyncOperation asyncOp)
        {
            return new UnityWebRequestAwaiter(asyncOp);
        }
    }
    class BackgroundDownloadEditor : BackgroundDownload
    {
        IntPtr _backend = null;
        static Dictionary<string, IntPtr> _dwnloads = new Dictionary<string, IntPtr>();

        private IntPtr UnityBackgroundDownloadCreateRequest(string url) {
            IntPtr uwr = new UnityWebRequest(url);
            return uwr; 
        }
        private IntPtr UnityBackgroundDownloadAddRequestHeader(IntPtr request, string Key, string val) {
            request.SetRequestHeader(Key,val);
            return request;
        }
       private async void UnityBackgroundDownloadStart(IntPtr www, string full_path) {
            BackgroundDownloadConfig config = new BackgroundDownloadConfig();
            config.url = www.uri;
            config.filePath = full_path;
            var dl = new BackgroundDownloadEditor(www, config);
            try {
                _dwnloads.Remove(full_path);
            }
            catch (Exception) {
            }
            _dwnloads.Add(full_path,www);
            if (!full_path.Contains(Application.persistentDataPath))
                full_path = System.IO.Path.Combine(Application.persistentDataPath, full_path);
            _status = BackgroundDownloadStatus.Downloading;
            await www.SendWebRequest();
//            _status = www.isDone ? BackgroundDownloadStatus.Done : www.isHttpError? BackgroundDownloadStatus.Failed : BackgroundDownloadStatus.Downloading;
            //            _dwnloads.Remove(full_path);
/*
            BinaryWriter writer = new BinaryWriter(File.OpenWrite(full_path));
            writer.Write(www.downloadHandler.data, 0, www.downloadHandler.data.Length);
            writer.Flush();
            writer.Close();
*/
        }
        internal BackgroundDownloadEditor(BackgroundDownloadConfig config)
            : base(config)
        {
            var destDir = Path.GetDirectoryName(Path.Combine(Application.persistentDataPath, config.filePath));
            try
            {
                if (!Directory.Exists(destDir))
                    Directory.CreateDirectory(destDir);
            }
            catch (Exception E) {
            }

            IntPtr request = UnityBackgroundDownloadCreateRequest(config.url.AbsoluteUri);
            request.downloadHandler = new DownloadHandlerFile(Path.Combine(Application.persistentDataPath, config.filePath));

            if (config.requestHeaders != null)
                foreach (var header in config.requestHeaders)
                    if (header.Value != null)
                        foreach (var val in header.Value)
                            UnityBackgroundDownloadAddRequestHeader(request, header.Key, val);
            _backend = request;
            UnityBackgroundDownloadStart(request, config.filePath);
        }

        
        BackgroundDownloadEditor(IntPtr backend, BackgroundDownloadConfig config)
            : base(config)
        {
            _backend = backend;
        }
        
        public override BackgroundDownloadStatus status { get { UpdateStatus(); return base.status; } }

        public override bool keepWaiting
        {
            get
            {
                UpdateStatus();
                return _status == BackgroundDownloadStatus.Downloading;
            }
        }

        internal static Dictionary<string, BackgroundDownload> LoadDownloads()
        {
            Dictionary<string, BackgroundDownload> downloads = new Dictionary<string, BackgroundDownload>();
            foreach (var item in _dwnloads)
            {
                IntPtr backend = item.Value;
                BackgroundDownloadConfig config = new BackgroundDownloadConfig();
                config.url = backend.uri;
                config.filePath = item.Key;
                var dl = new BackgroundDownloadEditor(backend, config);
                downloads[config.filePath] = dl;
            }
            return downloads;
        }

        internal static void SaveDownloads(Dictionary<string, BackgroundDownload> downloads)
        {
        }

        private float UnityBackgroundDownloadGetProgress() {
            float total = 0;
            float current = 0;
            foreach (var item in _dwnloads) {
                total++;
                current += item.Value.downloadProgress;
            }

            return current/total;
        }
        protected override float GetProgress()
        {
            if (_dwnloads.Count==0)
                return 1.0f;
            if (_status != BackgroundDownloadStatus.Downloading)
                return 1.0f;
            return UnityBackgroundDownloadGetProgress();
        }

        public override void Dispose()
        {
//            if (_backend.Count != 0)
//                UnityBackgroundDownloadDestroy(_backend);
            base.Dispose();
        }

        private BackgroundDownloadStatus UnityBackgroundDownloadGetStatus()
        {
            if (_backend.isHttpError || _backend.isNetworkError)
                return BackgroundDownloadStatus.Failed;
            if (_backend.downloadProgress<1.0)
                return BackgroundDownloadStatus.Downloading;
            return BackgroundDownloadStatus.Done;
        }
        private void UpdateStatus()
        {
            if (_status != BackgroundDownloadStatus.Downloading)
                return;
            _status = UnityBackgroundDownloadGetStatus();
            if (_status == BackgroundDownloadStatus.Failed)
                _error = GetError();
        }

        private string GetError()
        {

            return _backend.error;

        }

    }
}

#endif
