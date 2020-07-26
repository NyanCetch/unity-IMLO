using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

public class ImageLoader : MonoBehaviour
{
    [SerializeField] private string dirRoot;
    [SerializeField] private string imageDir;
    [SerializeField] private Transform container;
    [SerializeField] private GameObject imagePrefab;
    [SerializeField] private float loadDelay = 0.5f;

    private int _imageCount;
    void Start()
    {
        var imageDirPath = Path.Combine(dirRoot, imageDir);
        var imageUrls = Directory.GetFiles(imageDirPath, "*.jpg");
        _imageCount = imageUrls.Length;
        
        CreateImageItems();
        
        TextureLoaderManager.IsDebug = true;

        var operations = new List<Operation<Texture2D>>();
        var loader = TextureLoaderManager.Instance;
        for (var i = 0; i < _imageCount; ++i)
        {
            var op = loader.LoadTexture(imageUrls[i], _imageItems[i].SetTexture);
            operations.Add(op);
        }

        var cancelledCount = Random.Range(1, operations.Count / 2);
        var cancelled = 0;
        while (cancelled != cancelledCount)
        {
            var index = Random.Range(0, operations.Count);
            var op = operations[index];
            
            if (op.IsCanceled) 
                continue;
            
            op.Cancel();
            cancelled += 1;
        }
    }

    private ImageItem[] _imageItems;

    public void CreateImageItems()
    {
        for (var i = 0; i < container.childCount; ++i)
            Destroy(container.GetChild(i).gameObject);

        _imageItems = new ImageItem[_imageCount];
        for (var i = 0; i < _imageCount; i++)
        {
            var imageItem = Instantiate(imagePrefab, container);
            _imageItems[i] = imageItem.GetComponent<ImageItem>();
        }
    }

    private async UniTask<byte[]> RequestTextureDataAsync(string url)
    {
        using (var request = new UnityWebRequest{ url = url, method = UnityWebRequest.kHttpVerbGET, downloadHandler = new DownloadHandlerBuffer()})
        {
            await request.SendWebRequest();
            return request.downloadHandler.data;
        }
    }

    private async UniTask<Texture2D> RequestTexture1Async(string url)
    {
        using (var request = UnityWebRequestTexture.GetTexture(url))
        {
            await request.SendWebRequest();
            return DownloadHandlerTexture.GetContent(request);
        }
    }
    
    private async UniTask<Texture2D> RequestTexture2Async(string url)
    {
        using (var request = UnityWebRequest.Get(url))
        {
            request.downloadHandler = new DownloadHandlerBuffer();
            await request.SendWebRequest();
            var t = new Texture2D(2, 2);
            t.LoadImage(request.downloadHandler.data);
            return t;
        }
    }

    private async UniTaskVoid LoadImageMethodAsync(IReadOnlyList<string> paths)
    {
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < paths.Count; i++)
        {
            sw.Restart();
            var data = File.ReadAllBytes(paths[i]);
            var t = new Texture2D(2, 2);
            t.LoadImage(data);
            sw.Stop();
            _imageItems[i].SetTexture(t);
            Debug.Log($"Load: {sw.Elapsed.TotalSeconds}, Image: {paths[i]}");
            
            await UniTask.Delay(TimeSpan.FromSeconds(loadDelay));
        }
    }
    
    private async UniTaskVoid UnityWebRequestMethodAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < paths.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            var load = sw.Elapsed;
            var t = await RequestTexture1Async(paths[i]);
            
            _imageItems[i].SetTexture(t);
            Debug.Log($"Load: {(sw.Elapsed - load).TotalSeconds}, Image: {paths[i]}");
            
            if (loadDelay > 0 && i < paths.Count - 1)
                await UniTask.Delay(TimeSpan.FromSeconds(loadDelay), cancellationToken: cancellationToken);
        }
        Debug.Log($"Total: {sw.Elapsed.TotalSeconds - loadDelay * (paths.Count - 1)}");
    }
}
