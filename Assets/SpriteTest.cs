using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using YooAsset;

public class SpriteTest : MonoBehaviour
{
    public EPlayMode playMode = EPlayMode.EditorSimulateMode;
    public Image Image;
    public Image Image2;
    private string PackageVersion;
    private ResourceDownloaderOperation Downloader;

    private const string s_PackageName = "DefaultPackage";

    private void Awake()
    {
        Do();
    }

    async UniTaskVoid Do()
    {
        YooAssets.Initialize();
        YooAssets.SetOperationSystemMaxTimeSlice(30);

        var packageName = s_PackageName;
        var package = YooAssets.TryGetPackage(packageName);
        if (package == null)
        {
            package = YooAssets.CreatePackage(packageName);
            YooAssets.SetDefaultPackage(package);
        }

        InitializationOperation initializationOperation = null;
        if (playMode == EPlayMode.EditorSimulateMode)
        {
            var createParameters = new EditorSimulateModeParameters();
            createParameters.SimulateManifestFilePath = EditorSimulateModeHelper.SimulateBuild(packageName);
            initializationOperation = package.InitializeAsync(createParameters);
        }

        // 单机运行模式
        if (playMode == EPlayMode.OfflinePlayMode)
        {
            var createParameters = new OfflinePlayModeParameters();
            initializationOperation = package.InitializeAsync(createParameters);
        }

        // 联机运行模式
        if (playMode == EPlayMode.HostPlayMode)
        {
            var createParameters = new HostPlayModeParameters();
            createParameters.QueryServices = new GameQueryServices();
            createParameters.DefaultHostServer = GetHostServerURL();
            createParameters.FallbackHostServer = GetHostServerURL();
            initializationOperation = package.InitializeAsync(createParameters);
        }

        await initializationOperation;
        if (package.InitializeStatus == EOperationStatus.Succeed)
        {
            OnInitSuccess();
        }
        else
        {
            Debug.LogWarning($"{initializationOperation.Error}");
            PatchEventDefine.InitializeFailed.SendEventMessage();
        }
    }

    string GetHostServerURL()
    {
        return "http://127.0.0.1:8088";
    }

    void OnInitSuccess()
    {
        UpdateResVersion();
    }

    async UniTaskVoid UpdateResVersion()
    {
        Debug.Log("UpdateResVersion-Begin");
        var package = YooAssets.GetPackage(s_PackageName);
        var handle = package.UpdatePackageVersionAsync();
        await handle;
        if (handle.Status == EOperationStatus.Succeed)
        {
            Debug.Log("UpdateResVersion-Finish");
            PackageVersion = handle.PackageVersion;
            UpdateManifest();
        }
        else
        {
            Debug.LogError($"{handle.Error}");
        }
    }

    async UniTaskVoid UpdateManifest()
    {
        Debug.Log("UpdateManifest-Begin");
        var package = YooAssets.GetPackage(s_PackageName);
        var handle = package.UpdatePackageManifestAsync(PackageVersion);
        await handle;
        if (handle.Status == EOperationStatus.Succeed)
        {
            Debug.Log("UpdateManifest-Finish");
            handle.SavePackageVersion();
            CreateDownloader();
        }
        else
        {
            Debug.LogError($"{handle.Error}");
        }
    }

    async UniTaskVoid CreateDownloader()
    {
        Debug.Log("CreateDownloader-Begin");
        var downloadingMaxNum = 10;
        var failedTryAgain = 3;
        var downloader = YooAssets.CreateResourceDownloader(downloadingMaxNum, failedTryAgain);
        Downloader = downloader;
        Debug.Log("CreateDownloader-Finish");
        if (downloader.TotalDownloadCount == 0)
        {
            Debug.Log("nothing to download");
            DownloadOver();
        }
        else
        {
            Debug.Log($"{downloader.TotalDownloadCount} to download");
            DownloadFile();
        }
    }

    async UniTaskVoid DownloadFile()
    {
        Debug.Log("DownloadFile-Begin");
        Downloader.OnDownloadErrorCallback = OnDownloadError;
        Downloader.OnDownloadProgressCallback = OnDownloadProgress;
        Downloader.BeginDownload();
        await Downloader;
        Debug.Log($"DownloadFile-Finish,State:{Downloader.Status}");
        UpdateFinish();
    }

    void OnDownloadError(string fileName,string error)
    {
        Debug.LogError($"{fileName},{error}");
    }

    void OnDownloadProgress(int totalDownloadCount, int currentDownloadCount, long totalDownloadBytes, long currentDownloadBytes)
    {
        Debug.Log($"totalCount:{totalDownloadCount},currentCount:{currentDownloadCount},totalBytes:{totalDownloadBytes},currentBytes:{currentDownloadBytes}");
    }

    async UniTaskVoid DownloadOver()
    {
        Debug.Log("DownloadOver-Begin");
        Debug.Log("DownloadOver-Finish");
        ClearCache();
    }

    async UniTaskVoid ClearCache()
    {
        Debug.Log("ClearCache-Begin");
        var package = YooAssets.GetPackage(s_PackageName);
        var handle = package.ClearUnusedCacheFilesAsync();
        await handle;
        Debug.Log("ClearCache-Finish");
        UpdateFinish();
    }

    async UniTaskVoid UpdateFinish()
    {
        Debug.Log("UpdateFinish-Begin");
        Debug.Log("UpdateFinish-Finish");
        StartGame();
    }

    private AssetOperationHandle Handle;
    void StartGame()
    {
        var handle = YooAssets.LoadAssetSync<Sprite>("arrow1_left");
        Handle = handle;
        if (handle.Status == EOperationStatus.Succeed)
        {
            Image.sprite = handle.AssetObject as Sprite;
        }
        else
        {
            Debug.LogError($"{handle.LastError}");
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F))
        {
            Handle.Release();
            var package = YooAssets.GetPackage(s_PackageName);
            package.UnloadUnusedAssets();
        }
    }
}

public class GameQueryServices : IQueryServices
{
    public bool QueryStreamingAssets(string fileName)
    {
        string buildinFolderName = YooAssets.GetStreamingAssetBuildinFolderName();
        return StreamingAssetsHelper.FileExists($"{buildinFolderName}/{fileName}");
    }
}