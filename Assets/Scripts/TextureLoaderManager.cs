using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

public class TextureLoaderManager : MonoBehaviour
{
    public static bool IsDebug { get; set; }
    [SerializeField] private float loadDelay = 1f;
    public static TextureLoaderManager Instance { get; private set; }

    public Operation<Texture2D> LoadTexture(string url, Action<Texture2D> callback)
    {
        var task = new TextureLoadTask(url);
        var op = task.Operation;
        op.OnComplete(callback);
        AddTask(task);

        return op;
    }

    private Queue<TextureLoadTask> _loadQueue;

    private void AddTask(TextureLoadTask task)
    {
        _loadQueue.Enqueue(task);
        
        if (!_isExecuting)
            Loader(this.GetCancellationTokenOnDestroy()).Forget();
    }

    private bool _isExecuting;

    private async UniTaskVoid Loader(CancellationToken cancellationToken)
    {
        _isExecuting = true;
        
        while (_loadQueue.Count > 0)
        {
            if (cancellationToken.IsCancellationRequested)
                break;
            
            var task = _loadQueue.Dequeue();
            await task.Load(cancellationToken);
            if (_loadQueue.Count > 1 && task.Operation.IsSuccess) // Для предотвращения задержек на последнем задании или пропущенных
                await UniTask.Delay(TimeSpan.FromSeconds(loadDelay), cancellationToken: cancellationToken);
        }

        _isExecuting = false;
    }
    
    private void Awake()
    {
        if (Instance != null)
            Destroy(Instance.gameObject);

        Instance = this;
        
        _loadQueue = new Queue<TextureLoadTask>();
    }

    private class TextureLoadTask
    {
        private readonly string _url;
        private readonly Operation<Texture2D> _operation;
        private readonly CancellationToken _opCancellation;

        public Operation<Texture2D> Operation => _operation;

        public TextureLoadTask(string url)
        {
            _url = url;
            
            _operation = new Operation<Texture2D>();
            _opCancellation = _operation.GetToken();
        }

        public async UniTask Load(CancellationToken taskCancellation)
        {
            if (_opCancellation.IsCancellationRequested)
            {
                if (IsDebug)
                    Debug.Log("Загрузка отменена пользователем!");
                
                return;
            }

            if (taskCancellation.IsCancellationRequested)
                return;
            
            _operation.InProcess();
            
            var sw = Stopwatch.StartNew();

            var request = UnityWebRequestTexture.GetTexture(_url);
            await request.SendWebRequest();

            var t = DownloadHandlerTexture.GetContent(request);
            
            sw.Stop();
            if (IsDebug)
                Debug.Log($"Texture loaded: {sw.Elapsed.TotalSeconds}\nUrl: {_url}");
            
            _operation.Complete(t);
        }
    }
}

public class Operation<T>
{
    public T Data { get; private set; }
    public bool IsSuccess { get; private set; }
    public bool IsProcessing { get; private set; }
    public bool IsCanceled { get; private set; }

    private readonly List<Action<T>> _onCompleteCallbacks;
    private readonly List<Action> _onFailureCallbacks;

    private readonly CancellationTokenSource _cts;
    
    public Operation()
    {
        _cts = new CancellationTokenSource();
        _onCompleteCallbacks = new List<Action<T>>();
        _onFailureCallbacks = new List<Action>();
    }

    public CancellationToken GetToken() => _cts.Token;

    public void OnComplete(Action<T> callback)
    {
        _onCompleteCallbacks.Add(callback);
    }

    public void OnFailure(Action callback)
    {
        _onFailureCallbacks.Add(callback);
    }

    private void RaiseCompleteCallbacks()
    {
        foreach (var callback in _onCompleteCallbacks)
            callback?.Invoke(Data);
    }

    private void RaiseFailureCallbacks()
    {
        foreach (var callback in _onFailureCallbacks)
            callback?.Invoke();
    }

    public void InProcess()
    {
        IsProcessing = true;
    }
    
    public void Complete(T result)
    {
        Data = result;
        IsSuccess = true;
        IsProcessing = false;
        
        RaiseCompleteCallbacks();
    }

    public void Failure()
    {
        IsSuccess = false;
        IsProcessing = false;
        
        RaiseFailureCallbacks();
    }

    public void Cancel()
    {
        IsCanceled = true;
        IsProcessing = false;
        
        _cts.Cancel();
    }
}
