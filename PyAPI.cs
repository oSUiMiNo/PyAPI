using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using System.IO;
using System;
using System.Collections.Generic;
using UniRx;
using System.Text;
using MyUtil;


public class PyAPIHandler : SingletonCompo<PyAPIHandler>
{
    protected sealed override void Awake0() => PyAPI.InitLog();
    private void OnApplicationQuit() => PyAPI.Close();
    private void OnDestroy() => PyAPI.Close(); // パッケージインポート先で実行されてない
}


//****************************************************
// PyFnc を生成して待機させたりアイドルさせる
//****************************************************
public class PyAPI
{
    //==================================================
    // パブリック
    //==================================================
    public string PyInterpFile { get; }
    public string PyDir { get; }
    //==================================================
    // スタティック-ローカル
    //==================================================
    static string LogPath => $"{Application.dataPath}/PyLog.txt";
    // Python ログ表示用インスタンス
    static SharedLog Log = new SharedLog(LogPath);
    // シェアログ読み取りタイミングのハンドラ
    static IObservable<long> OnRead => logActive.UpdateWhileEqualTo(Log.isActive, 0.05f);
    static BoolReactiveProperty logActive = new BoolReactiveProperty(true);
    //==================================================
    // コンストラクタ
    //==================================================
    /// <param name="pyDir">ラップする .py ファイルのがある Dir</param>
    /// <param name="pyInterpFile">Python のインタプリタ</param>
    public PyAPI(string pyDir, string pyInterpFile = "")
    {
        PyDir = pyDir;
        if (string.IsNullOrEmpty(pyInterpFile)) PyInterpFile = $"{pyDir}/.venv/Scripts/python.exe";
        else PyInterpFile = pyInterpFile;
    }

    //==================================================
    // 高速実行したい関数を作成してアイドリングさせる
    //==================================================
    public async UniTask<PyFnc> Idle(string pyFileName, int processCount = 1, int threadCount = 1)
    {
        // Pythonファイルパス
        string pyFile = @$"{PyDir}\{pyFileName}";
        if (!File.Exists(PyInterpFile)) Debug.LogError($"次の実行ファイルは無い{PyInterpFile}");
        if (!File.Exists(pyFile)) Debug.LogError($"次のPyファイルは無い{pyFile}");
        PyFnc pyFnc;
        if (processCount <= 1) pyFnc = await PyFnc.Create(PyInterpFile, pyFile);
        else pyFnc = await PyFnc.Create(PyInterpFile, pyFile, processCount: processCount, threadCount: threadCount);
        pyFnc.Start();
        GC.Collect();
        return pyFnc;
    }

    //==================================================
    // １ショット実行する関数を作成して待機させる
    //==================================================
    public async UniTask<PyFnc> Wait(string pyFileName, float timeout = 0)
    {
        return await Wait(pyFileName, new JObject(), timeout);
    }
    public async UniTask<PyFnc> Wait(string pyFileName, JObject inJO, float timeout = 0, bool largeInput = false)
    {
        // Pythonファイルパス
        string pyFile = @$"{PyDir}\{pyFileName}";
        if (!File.Exists(PyInterpFile)) Debug.LogError($"次の実行ファイルは無い{PyInterpFile}");
        if (!File.Exists(pyFile)) Debug.LogError($"次のPyファイルは無い{pyFile}");
        //// ["] を [\""] にエスケープしたJson
        //string sendData = JsonConvert.SerializeObject(inJO).Replace("\"", "\\\"\"");
        PyFnc pyFnc = await PyFnc.Create(PyInterpFile, pyFile, inJO, largeInput: largeInput);
        GC.Collect();
        return pyFnc;
    }

    //==================================================
    // 
    //==================================================
    public static void InitLog()
    {
        OnRead.Subscribe(_ =>
        {
            if (!File.Exists(LogPath)) return; // なんかオペレータをすり抜けるのでブロックしとく
            //Debug.Log($"ログ {File.Exists(LogPath)}");
            Log.ReadLogFile();
        }).AddTo(PyAPIHandler.Compo);

        Log.OnLog.Subscribe(msg =>
        {
            Debug.Log(msg.HexColor("#90E3C4"));
        }).AddTo(PyAPIHandler.Compo);
    }

    //==================================================
    // 
    //==================================================
    public static async void Close()
    {
        // 終了時はは待ち時間0じゃないとパッケージ利用先で実行されない
        PyFnc.CloseAll(0);
        logActive.Dispose();
        // 終了後に待ちたいのでここはDelay.Secondではだめ
        await UniTask.Delay(1);
        Log.Close();
        Debug.Log("PyAPI クローズ完了");
    }
}


//****************************************************
// Python プロセスのラッパ
// 1つの .py ファイルにつき (場合により) 複数のプロセスを作成し
// それらを1つの関数 (PyFnc インスタンス) としてラップ
//****************************************************
public class PyFnc
{
    //==================================================
    // スタティック-ローカル
    //==================================================
    // アイドル中の PyFnc インスタンス管理
    static List<PyFnc> IdolingFncs = new();
    // 引数を渡すファイルを1Fncにつき複数使う
    static int InPathNum = 0;
    //==================================================
    // パブリック
    //==================================================
    // .py ファイル名を Fnc 名とする
    public string FncName { get; private set; }
    // 戻り値が書き込まれるファイル
    public string OutPath { get; private set; }
    //==================================================
    // プライベート
    //==================================================
    // 子プロセス管理
    List<System.Diagnostics.Process> children = new();
    // 子プロセスのインデックス
    int currentChildIndex = 0;
    // タイムアウト時間
    float Timeout = 0;
    // 実行キャンセル用トークン
    CancellationTokenSource cts = new();
    // アウトプット監視用
    SharedLog Output;
    // アウトプット読み取りタイミングのハンドラ
    IObservable<long> OnRead => logActive.TimerWhileEqualTo(Output.isActive, 0.01f);
    BoolReactiveProperty logActive = new(true);

    //==================================================
    // アウトプットに戻り値が来たら流す
    //==================================================
    public IObservable<JObject> OnOut => Output.OnLog
    .Select(msg =>
    {
        try
        {
            return JObject.Parse(msg);
        }
        catch (Exception ex)
        {
            // エラー処理 (必要に応じて)
            Debug.LogError($"JSONパースエラー: {ex.Message}");
            return null;
        }
    })
    .Where(JO => JO != null)
    .Where(JO => JO["Loaded"] == null);

    //==================================================
    // アウトプットにプロセスのロード完了通知が来たら流す
    //==================================================
    public IObservable<JObject> OnLoad => Output.OnLog
    .Select(msg =>
    {
        try
        {
            return JObject.Parse(msg);
        }
        catch (Exception ex)
        {
            // エラー処理 (必要に応じて)
            Debug.LogError($"JSONパースエラー: {ex.Message}");
            return null;
        }
    })
    .Where(JO => JO != null)
    .Where(JO => JO["Loaded"] != null);

    //==================================================
    // PyFnc インスタンス作成
    //==================================================
    public static async UniTask<PyFnc> Create(string pyInterpFile, string pyFile, JObject inJO = null, int processCount = 1, int threadCount = 1, float timeout = 0, bool largeInput = false)
    {
        await UniTask.SwitchToThreadPool();
        var newFnc = new PyFnc();
        IdolingFncs.Add(newFnc);

        newFnc.FncName = Path.GetFileName(pyFile);
        newFnc.Timeout = timeout;

        Debug.Log($"ラージインプット {largeInput}");
        string inPath = "";
        if (largeInput == true)
        {
            inPath = $"{Path.GetDirectoryName(pyFile)}/LargeInput{InPathNum}.txt";
            InPathNum++;
            if (InPathNum > 50000) InPathNum = 0;
            // ファイルが存在する場合は上書き
            StreamWriter writer = new StreamWriter(inPath, false);
            writer.WriteLine(JsonConvert.SerializeObject(inJO));//.Replace("\"", "\\\"\"")); // 書き込むテキスト
            writer.Close();
            inJO = null;
            Debug.Log($"書き込み完了");
        }
        if (inJO == null) inJO = new JObject();
        inJO["ThreadCount"] = threadCount;
        inJO["LargeInput"] = largeInput;
        inJO["InPath"] = inPath;

        // ["] を [\""] にエスケープしたJson
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
                    UseShellExecute = false, // シェルを使用しない
                    RedirectStandardOutput = true, // 標準出力をリダイレクト
                    RedirectStandardInput = true, // 標準入力をリダイレクト
                    RedirectStandardError = true, // 標準エラーをリダイレクト
                    CreateNoWindow = true, // PowerShellウィンドウを表示しない
                }
            });
            log += $", {i.ToString()}";
        }
        log += $"\n各スレッド数: {threadCount}";
        Debug.Log(log);
        await UniTask.Delay(1);
        newFnc.InitLog(pyFile);
        // Create 内でawait すると何故か onOut が発火しない
        //await newFnc.WaitLoad(count);
        await UniTask.SwitchToMainThread();
        return newFnc;
    }

    //==================================================
    // 全プロセスの7割以上がロード完了するまで待つ
    // Create 内でawait すると何故か onOut が発火しない
    //==================================================
    public async UniTask WaitLoad(int completionRate)
    {
        if (completionRate < 1 || 10 < completionRate)
        {
            Debug.LogError("completionRate は 1-10 の間で");
            return;
        }
        // AddTo の中身はGOかCompoなのでメインスレッドじゃないとだめ
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

    //==================================================
    // 存在している全 PyFnc 終了
    //==================================================
    public static void CloseAll(int waittMilliSecond)
    {
        foreach (var fnc in IdolingFncs)
        {
            fnc.Close(waittMilliSecond);
        }
        IdolingFncs.Clear();
    }

    //==================================================
    // 本 PyFunc 終了
    //==================================================
    public async void Close(int waittMilliSecond)
    {
        await UniTask.SwitchToThreadPool();
        // 実行後即クローズされた場合アウトプットが受取れなかったりするので待つ
        // ただしアプリ終了時は待つとパッケージ利用先では呼ばれないので最後はは待たない
        await UniTask.Delay(waittMilliSecond);
        cts.Cancel();
        Output.Close();
        logActive.Dispose();
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

    //==================================================
    // 出力監視の設定
    //==================================================
    void InitLog(string pyFile)
    {
        // アウトプット用ファイル作成;
        OutPath = $"{pyFile.Replace("\\", "/").Replace(".py", ".txt")}";
        Output = new SharedLog(OutPath);
        OnRead.Subscribe(_ =>
        {
            if (!File.Exists(OutPath)) return; // なんかオペレータをすり抜けるのでブロックしとく
            //Debug.Log($"ログ{File.Exists(OutPath)} {Path.GetFileName(OutPath)}");
            Output.ReadLogFile();
        }).AddTo(PyAPIHandler.Compo);
    }

    //==================================================
    // 1PyFncインスタンスで管理する全プロセスを起動
    //==================================================
    public async void Start()
    {
        foreach (var child in children)
        {
            await UniTask.SwitchToThreadPool();
            child.Start();
            await UniTask.SwitchToMainThread();
        }
    }

    //==================================================
    // Idle 中の関数を実行
    //==================================================
    public async UniTask<JObject> Exe(JObject inJO)
    {
        JObject outJO = null;

        // AddTo の中身は GO か Compo なのでメインスレッドじゃないとだめ
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
    public void ExeBG(JObject inJO)
    {
        children[currentChildIndex].Exe(inJO);
        if (currentChildIndex == children.Count - 1) currentChildIndex = 0;
        else currentChildIndex++;
        //GC.Collect();
    }

    //==================================================
    // Wait 中の関数を実行
    //==================================================
    public async UniTask<JObject> Exe()
    {
        JObject outJO = null;

        // AddTo の中身はGOかCompoなのでメインスレッドじゃないとだめ
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
    public async void ExeBG()
    {
        try
        {
            // Close するために待機
            await children[currentChildIndex].ExeAsync(Timeout, () => Output.Close(), cts.Token);
        }
        catch (OperationCanceledException) { }
        if (currentChildIndex == children.Count - 1) currentChildIndex = 0;
        else currentChildIndex++;
        
        Close(100);
        GC.Collect();
    }
}