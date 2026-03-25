using Cysharp.Threading.Tasks;
using Maku;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using UniRx;
using UnityEngine;



public class PyAPIHandler : SingletonCompo<PyAPIHandler>
{
    protected sealed override void Awake0() => PyAPI.InitLog();
    private void OnApplicationQuit() => PyAPI.Close();
    private void OnDestroy() => PyAPI.Close();
}



///*******************************************************<summary>
/// PyFnc を生成して待機させたりアイドルさせる
///</summary>******************************************************
public class PyAPI
{
    //--------------------------------------
    // パブリック
    //--------------------------------------
    public string PyInterpFile { get; }
    public string PyDir { get; }
    Dictionary<string, PyFnc> idleCache = new();


    /// <param name="pyDir">ラップする .py ファイルのがある Dir</param>
    /// <param name="pyInterpFile">Python のインタプリタ</param>
    public PyAPI(string pyDir, string pyInterpFile = "")
    {
        PyDir = pyDir;
        if (string.IsNullOrEmpty(pyInterpFile)) PyInterpFile = $"{pyDir}/.venv/Scripts/python.exe";
        else PyInterpFile = pyInterpFile;
    }


    ///==============================================<summary>
    /// 高速実行したい関数を作成してアイドリングさせる
    ///</summary>=============================================
    public async UniTask<PyFnc> Idle(string pyFileName, int processCount = 1, int threadCount = 1, int completionRate = 7)
    {
        try
        {
            if (idleCache.TryGetValue(pyFileName, out var cached) && !cached.IsClosed)
                return cached;
            string pyFile = @$"{PyDir}\{pyFileName}";
            if (!File.Exists(PyInterpFile)) throw new Exception($"次の実行ファイルは無い{PyInterpFile}");
            if (!File.Exists(pyFile)) throw new Exception($"次のPyファイルは無い{pyFile}");
            PyFnc pyFnc;
            if (processCount <= 1) pyFnc = await PyFnc.Create(PyInterpFile, pyFile);
            else pyFnc = await PyFnc.Create(PyInterpFile, pyFile, processCount: processCount, threadCount: threadCount);
            pyFnc.Start();
            await pyFnc.WaitLoad(completionRate);
            GC.Collect();
            idleCache[pyFileName] = pyFnc;
            return pyFnc;
        }
        catch (Exception e) { throw e; }
    }


    ///==============================================<summary>
    /// １ショット実行する関数を作成して待機させる
    ///</summary>=============================================
    public async UniTask<PyFnc> Wait(string pyFileName, float timeout = 0)
    {
        return await Wait(pyFileName, new JObject(), timeout);
    }
    public async UniTask<PyFnc> Wait(string pyFileName, JObject inJO, float timeout = 0, bool largeInput = false)
    {
        try
        {
            string pyFile = @$"{PyDir}\{pyFileName}";
            if (!File.Exists(PyInterpFile)) throw new Exception($"次の実行ファイルは無い{PyInterpFile}");
            if (!File.Exists(pyFile)) throw new Exception($"次のPyファイルは無い{pyFile}");
            PyFnc pyFnc = await PyFnc.Create(PyInterpFile, pyFile, inJO, timeout: timeout, largeInput: largeInput);
            GC.Collect();
            return pyFnc;
        }
        catch (Exception e) { throw e; }
    }


    ///==============================================<summary>
    /// 待機 -> ワンショット実行
    ///</summary>=============================================
    public async UniTask Exe(string pyFileName, float timeout = 0)
    {
        PyFnc fnc = await Wait(pyFileName, timeout);
        await fnc.Exe();
    }
    public async UniTask Exe(string pyFileName, JObject inJO, float timeout = 0, bool largeInput = false)
    {
        PyFnc fnc = await Wait(pyFileName, inJO, timeout, largeInput);
        await fnc.Exe();
    }


    ///==============================================<summary>
    /// 初期化（PyAPIHandler.Awake0 から呼ばれる）
    ///</summary>=============================================
    public static void InitLog()
    {
        // TCP 方式ではグローバルログファイル不要
        // ログは各 PyFnc の TcpBridge 経由で受信する
    }


    ///==============================================<summary>
    /// 全体終了
    ///</summary>=============================================
    public static async void Close()
    {
        PyFnc.CloseAll(0);
        await UniTask.Delay(1);
        Debug.Log("PyAPI クローズ完了");
    }
}




///*******************************************************<summary>
/// Python プロセスのラッパ
/// 1つの .py ファイルにつき (場合により) 複数のプロセスを作成し
/// それらを1つの関数 (PyFnc インスタンス) としてラップ
///</summary>******************************************************
public class PyFnc
{
    //--------------------------------------
    // スタティック-ローカル
    //--------------------------------------
    static List<PyFnc> IdolingFncs = new();
    static int InPathNum = 0;
    //--------------------------------------
    // パブリック
    //--------------------------------------
    public string FncName { get; private set; }
    public bool IsClosed { get; private set; }
    //--------------------------------------
    // プライベート
    //--------------------------------------
    List<System.Diagnostics.Process> children = new();
    int currentChildIndex = 0;
    float Timeout = 0;
    CancellationTokenSource cts = new();
    //--------------------------------------
    // TCP 通信
    //--------------------------------------
    List<TcpBridge> bridges = new();
    Subject<string> mergedMessages = new();


    ///==============================================<summary>
    /// アウトプットに戻り値が来たら流す
    ///</summary>=============================================
    public IObservable<JObject> OnOut => mergedMessages
    .Select(msg =>
    {
        try { return JObject.Parse(msg); }
        catch (Exception e) { throw new Exception($"JSONパースエラー: {e.Message}"); }
    })
    .Where(JO => JO != null)
    .Where(JO => (string)JO["_type"] == "out")
    .Select(JO => { JO.Remove("_type"); return JO; });


    ///==============================================<summary>
    /// アウトプットにプロセスのロード完了通知が来たら流す
    ///</summary>=============================================
    public IObservable<JObject> OnLoad => mergedMessages
    .Select(msg =>
    {
        try { return JObject.Parse(msg); }
        catch (Exception e) { throw new Exception($"JSONパースエラー: {e.Message}"); }
    })
    .Where(JO => JO != null)
    .Where(JO => (string)JO["_type"] == "loaded");


    ///==============================================<summary>
    /// PyFnc インスタンス作成
    ///</summary>=============================================
    public static async UniTask<PyFnc> Create(string pyInterpFile, string pyFile, JObject inJO = null, int processCount = 1, int threadCount = 1, float timeout = 0, bool largeInput = false)
    {
        if(largeInput) Debug.Log($"サイズの大きい入力");
        try
        {
            await UniTask.SwitchToThreadPool();
            var newFnc = new PyFnc();
            IdolingFncs.Add(newFnc);

            newFnc.FncName = Path.GetFileName(pyFile);
            newFnc.Timeout = timeout;

            string inPath = "";
            if (largeInput == true)
            {
                inPath = $"{Path.GetDirectoryName(pyFile)}/LargeInput{InPathNum}.txt";
                InPathNum++;
                if (InPathNum > 50000) InPathNum = 0;
                StreamWriter writer = new StreamWriter(inPath, false);
                writer.WriteLine(JsonConvert.SerializeObject(inJO));
                writer.Close();
                inJO = null;
                Debug.Log($"書き込み完了");
            }
            if (inJO == null) inJO = new JObject();
            inJO["ThreadCount"] = threadCount;
            inJO["LargeInput"] = largeInput;
            inJO["InPath"] = inPath;

            string sendData = JsonConvert.SerializeObject(inJO).Replace("\"", "\\\"\"");

            string log = $"PyFnc起動:{newFnc.FncName} - プロセス: ";
            if (processCount <= 0) processCount = 1;
            for (int i = 0; i < processCount; i++)
            {
                newFnc.children.Add(new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo(pyInterpFile)
                    {
                        Arguments = $"{pyFile} {sendData}",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardInput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                    }
                });
                log += $", {i.ToString()}";
            }
            log += $"\n各スレッド数: {threadCount}";
            Debug.Log(log);
            await UniTask.Delay(1);

            //--------------------------------------
            // ログ購読（_type=="log" → Unity コンソール出力）
            //--------------------------------------
            await UniTask.SwitchToMainThread();
            newFnc.mergedMessages
            .Select(msg => { try { return JObject.Parse(msg); } catch { return null; } })
            .Where(JO => JO != null && (string)JO["_type"] == "log")
            .Subscribe(JO =>
            {
                string logMsg = (string)JO["_msg"] ?? "";
                string src = (string)JO["_src"] ?? "";
                if (!string.IsNullOrEmpty(src)) logMsg += $"\n(at {src})";
                Debug.Log(logMsg.HexColor("#90E3C4"));
            }).AddTo(PyAPIHandler.Compo);

            return newFnc;
        }
        catch (Exception e) { throw e; }
    }


    ///==============================================<summary>
    /// 全プロセスの7割以上がロード完了するまで待つ
    ///</summary>=============================================
    public async UniTask WaitLoad(int completionRate)
    {
        try
        {
            if (completionRate < 1 || 10 < completionRate)
            {
                Debug.LogError("completionRate は 1-10 の間で");
                return;
            }
            bool ThreadIsMain = false;
            if (Thread.CurrentThread.ManagedThreadId == 1) ThreadIsMain = true;
            if (!ThreadIsMain) await UniTask.SwitchToMainThread();
            int loadedCount = 0;
            IDisposable onOut = OnLoad
            .Subscribe(JO =>
            {
                loadedCount++;
            }).AddTo(PyAPIHandler.Compo);
            if (!ThreadIsMain) await UniTask.SwitchToThreadPool();
            await UniTask.WaitUntil(() => loadedCount >= (int)(children.Count * 0.7));
            Debug.Log($"{FncName}: {completionRate}0% のプロセスがロード完了".Magenta());
            onOut.Dispose();
        }
        catch (Exception e) { throw e; }
    }


    ///==============================================<summary>
    /// 存在している全 PyFnc 終了
    ///</summary>=============================================
    public static void CloseAll(int waittMilliSecond)
    {
        foreach (var fnc in IdolingFncs)
        {
            fnc.Close(waittMilliSecond);
        }
        IdolingFncs.Clear();
    }


    ///==============================================<summary>
    /// 本 PyFunc 終了
    ///</summary>=============================================
    public async void Close(int waittMilliSecond)
    {
        if (IsClosed) return;
        IsClosed = true;
        await UniTask.SwitchToThreadPool();
        await UniTask.Delay(waittMilliSecond);
        cts.Cancel();
        //--------------------------------------
        // 全 bridge に終了通知 → クローズ
        //--------------------------------------
        foreach (var bridge in bridges)
        {
            try { await bridge.Send(new JObject { ["_type"] = "close" }); } catch { }
            bridge.Close();
        }
        try { mergedMessages.OnCompleted(); } catch { }
        string log = $"PyFncクローズ:{FncName} - プロセス ";
        for (int i = 0; i < children.Count; i++)
        {
            children[i].PerfectKill();
            log += $", {i.ToString()}";
        }
        Debug.Log(log);
        IdolingFncs.Remove(this);
        GC.Collect();
        await UniTask.SwitchToMainThread();
    }


    ///==============================================<summary>
    /// プロセスを起動し TCP 接続を確立する
    ///</summary>=============================================
    async UniTask StartChildAndConnect(System.Diagnostics.Process child)
    {
        await UniTask.SwitchToThreadPool();
        child.Start();
        //--------------------------------------
        // stdout から PORT:XXXXX を読み取り（タイムアウト付き）
        //--------------------------------------
        float timeoutSec = Timeout > 0 ? Timeout : 30f;
        var readTask = System.Threading.Tasks.Task.Run(() => child.StandardOutput.ReadLine());
        var completed = await System.Threading.Tasks.Task.WhenAny(
            readTask,
            System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(timeoutSec))
        );

        if (completed != readTask)
        {
            //--------------------------------------
            // タイムアウト：stderr からエラー情報を取得してから Kill
            //--------------------------------------
            string stderr = "";
            try
            {
                var stderrTask = System.Threading.Tasks.Task.Run(
                    () => child.StandardError.ReadToEnd());
                if (await System.Threading.Tasks.Task.WhenAny(
                    stderrTask,
                    System.Threading.Tasks.Task.Delay(2000)) == stderrTask)
                    stderr = stderrTask.Result;
            }
            catch { }
            child.PerfectKill();
            throw new TimeoutException(
                $"Python起動タイムアウト({timeoutSec}秒): {FncName}" +
                (string.IsNullOrEmpty(stderr) ? "" : $"\nstderr: {stderr}"));
        }

        string portLine = readTask.Result;
        if (portLine == null || !portLine.StartsWith("PORT:"))
        {
            //--------------------------------------
            // ポート読み取り失敗：stderr からエラー情報を取得してから Kill
            //--------------------------------------
            string stderr = "";
            try
            {
                var stderrTask = System.Threading.Tasks.Task.Run(
                    () => child.StandardError.ReadToEnd());
                if (await System.Threading.Tasks.Task.WhenAny(
                    stderrTask,
                    System.Threading.Tasks.Task.Delay(2000)) == stderrTask)
                    stderr = stderrTask.Result;
            }
            catch { }
            child.PerfectKill();
            throw new Exception(
                $"ポート読み取り失敗: {portLine}" +
                (string.IsNullOrEmpty(stderr) ? "" : $"\nstderr: {stderr}"));
        }
        int port = int.Parse(portLine.Replace("PORT:", ""));
        Debug.Log($"TCP接続: 127.0.0.1:{port}");
        //--------------------------------------
        // TcpBridge 接続 → 受信ループ開始
        //--------------------------------------
        var bridge = new TcpBridge("127.0.0.1", port);
        await bridge.Connect();
        bridge.OnMessage.Subscribe(msg => mergedMessages.OnNext(msg));
        bridge.StartReceiveLoop();
        bridges.Add(bridge);
        await UniTask.SwitchToMainThread();
    }


    ///==============================================<summary>
    /// 1PyFncインスタンスで管理する全プロセスを起動（Idle モード用）
    ///</summary>=============================================
    public async void Start()
    {
        foreach (var child in children)
        {
            await StartChildAndConnect(child);
        }
    }


    ///==============================================<summary>
    /// Idle 中の関数を実行
    ///</summary>=============================================
    public async UniTask<JObject> Exe(JObject inJO)
    {
        try
        {
            JObject outJO = null;

            bool ThreadIsMain = false;
            if (Thread.CurrentThread.ManagedThreadId == 1) ThreadIsMain = true;
            if (!ThreadIsMain) await UniTask.SwitchToMainThread();
            IDisposable onOut = OnOut.Subscribe(JO =>
            {
                outJO = JO;
            }).AddTo(PyAPIHandler.Compo);
            if (!ThreadIsMain) await UniTask.SwitchToThreadPool();

            ExeBG(inJO);
            await UniTask.WaitUntil(() => outJO != null);
            onOut.Dispose();

            return outJO;
        }
        catch (Exception e) { throw e; }
    }
    public void ExeBG(JObject inJO)
    {
        //--------------------------------------
        // TCP 経由でデータ送信（_type を付与してコピー送信）
        //--------------------------------------
        var msg = new JObject(inJO);
        msg["_type"] = "in";
        bridges[currentChildIndex].Send(msg).Forget();
        if (currentChildIndex == children.Count - 1) currentChildIndex = 0;
        else currentChildIndex++;
    }
    ///==============================================<summary>
    /// Idle 中の関数をバックグラウンドで実行（スレッド切替込み）
    ///</summary>=============================================
    public async UniTask ExeBGAsync(JObject inJO)
    {
        await UniTask.SwitchToThreadPool();
        ExeBG(inJO);
        await UniTask.SwitchToMainThread();
    }


    ///==============================================<summary>
    /// Wait 中の関数を実行（ワンショットモード）
    ///</summary>=============================================
    public async UniTask<JObject> Exe()
    {
        try
        {
            JObject outJO = null;

            bool ThreadIsMain = false;
            if (Thread.CurrentThread.ManagedThreadId == 1) ThreadIsMain = true;
            if (!ThreadIsMain) await UniTask.SwitchToMainThread();
            IDisposable onOut = OnOut.Subscribe(JO =>
            {
                outJO = JO;
            }).AddTo(PyAPIHandler.Compo);
            if (!ThreadIsMain) await UniTask.SwitchToThreadPool();

            ExeBG();
            await UniTask.WaitUntil(() => outJO != null);
            onOut.Dispose();

            return outJO;
        }
        catch (Exception e) { throw e; }
    }

    public async void ExeBG()
    {
        try
        {
            var child = children[currentChildIndex];
            //--------------------------------------
            // プロセス起動 → TCP 接続確立
            //--------------------------------------
            await StartChildAndConnect(child);
            //--------------------------------------
            // プロセス終了を待機
            //--------------------------------------
            await UniTask.SwitchToThreadPool();
            child.WaitForExit();
            await UniTask.SwitchToMainThread();
        }
        catch (OperationCanceledException) { }
        catch (Exception e) { Debug.LogError($"ExeBG エラー: {e.Message}"); }
        if (currentChildIndex == children.Count - 1) currentChildIndex = 0;
        else currentChildIndex++;

        Close(100);
        GC.Collect();
    }
}
