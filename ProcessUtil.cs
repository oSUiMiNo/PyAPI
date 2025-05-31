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


public static class ProcessUtil
{
    //==================================================
    // 引数的な感じで JObject を渡しプロセスを実行
    //==================================================
    public static async void Exe(this System.Diagnostics.Process process, JObject inJO)
    {
        if (process.StartInfo.RedirectStandardInput == false)
        {
            Debug.LogError("StartInfo.RedirectStandardInput を True にして");
            return;
        }
        try
        {
            await UniTask.SwitchToThreadPool();
            string sendData = JsonConvert.SerializeObject(inJO);
            StreamWriter inputWriter = process.StandardInput;
            inputWriter.WriteLine(sendData);
            inputWriter.Flush();
            await UniTask.SwitchToMainThread();
        }
        catch { }
    }

   
    //==================================================
    // プロセスの非同期実行 (外部からキャンセル可)
    //==================================================
    public static async UniTask ExeAsync(this System.Diagnostics.Process process, float timeout = 0, Action fncOnDispose = null, CancellationToken externalCT = default)
    {
        await UniTask.SwitchToThreadPool();
        var timeoutCTS = new CancellationTokenSource();
        var exited = new UniTaskCompletionSource();

        // タイムアウト時間が設定されている場合はその時間でタイムアウト
        if (timeout != 0)
            UniTask.RunOnThreadPool(() => process.Timeout(timeout, timeoutCTS.Token)).Forget();

        //--------------------------------------
        // プロセス終了時処理
        //--------------------------------------
        // Exited (プロセス終了) イベントを有効化
        process.EnableRaisingEvents = true;
        process.Exited += (sender, args) =>
        {
            // エラー読取り -> ログ出力
            string error = process.StandardError.ReadToEnd();
            if (!string.IsNullOrEmpty(error)) Debug.LogError($"PowerShell Error: {error}");
            // 実行結果の出力をセット
            exited.TrySetResult();
            // プロセス抹消
            process.PerfectKill();
        };

        //--------------------------------------
        // プロセス抹消時処理
        //--------------------------------------
        process.Disposed += (sender, args) =>
        {
            timeoutCTS.Cancel();
            fncOnDispose?.Invoke();
        };

        process.Start();

        //--------------------------------------
        // 外部からのキャンセルを設定
        //--------------------------------------
        try
        {
            // 外部キャンセルを反映
            await exited.Task.AttachExternalCancellation(externalCT);
            Debug.Log($"プロセス完了");
        }
        //--------------------------------------
        // 外部からキャンセルされた場合に走る処理
        //--------------------------------------
        catch (OperationCanceledException)
        {
            Debug.Log("外部からキャンセルされた");
            // 
            process.PerfectKill();
            // 必要に応じて例外を再スロー
            throw;
        }
        await UniTask.SwitchToMainThread();
    }

    //==================================================
    // タイムアウト
    //==================================================
    public static async void Timeout(this System.Diagnostics.Process process, float timeout, CancellationToken CT)
    {
        try
        {
            // 指定の時間経過したらプロセスを抹消
            await UniTask.WaitForSeconds(timeout, false, PlayerLoopTiming.Update, CT);
            Debug.LogAssertion("タイムアウト");
            process.PerfectKill();
        }
        catch (OperationCanceledException)
        {
            // タイムアウト前に実行が完了した場合はタイムアウトの予約自体キャンセルされる
            Debug.Log("タイムアウトがキャンセルされた");
        }
    }

    //==================================================
    // プロセス抹消 {
    //     -> Kill() でプロセスの動作即死
    //     -> Dipose() でプロセスと周辺資源を管理から解放
    // }
    //==================================================
    public static void PerfectKill(this System.Diagnostics.Process process)
    {
        try
        {
            // 例外を打ちすぎると無駄コストにらしいので事前にプロセスが稼働中な場合のみキル
            if (!process.HasExited)
            {
                process.Kill();           // 強制終了
                process.WaitForExit();    // 終了確認（タイムアウト付けてもいい）
            }
        }
        catch (InvalidOperationException)
        {
            // すでに終了していたら例外無視でOK
        }
        finally
        {
            // ハンドルだけは確実に解放
            process.Dispose();           
        }
    }


    // レガシー

    ////==================================================
    //// コマンド実行
    ////==================================================
    //public static async void Command(this System.Diagnostics.Process process, string command)
    //{
    //    if (process.StartInfo.RedirectStandardInput == false)
    //    {
    //        Debug.LogError("StartInfo.RedirectStandardInput を True にして");
    //        return;
    //    }
    //    try
    //    {
    //        await UniTask.SwitchToThreadPool();
    //        StreamWriter inputWriter = process.StandardInput;
    //        inputWriter.WriteLine(command);
    //        inputWriter.Flush();
    //        await UniTask.SwitchToMainThread();
    //    }
    //    catch { }
    //}

    ////==================================================
    //// プロセスの非同期実行
    ////==================================================
    //public static UniTask<string> RunAsync(this System.Diagnostics.Process process, float timeout = 0, Action fncOnDispose = null)
    //{
    //    var cts = new CancellationTokenSource();
    //    var exited = new UniTaskCompletionSource<string>();
    //    string output = "";

    //    // タイムアウト時間が設定されている場合はその時間でタイムアウト
    //    if (timeout != 0)
    //        UniTask.RunOnThreadPool(() => process.Timeout(timeout, cts.Token)).Forget();

    //    // Exited イベントを有効化
    //    process.EnableRaisingEvents = true;
    //    process.Exited += (sender, args) =>
    //    {
    //        // エラー読取り
    //        string error = process.StandardError.ReadToEnd();
    //        if (!string.IsNullOrEmpty(error)) Debug.LogError($"PowerShell Error: {error}");
    //        output = process.StandardOutput.ReadToEnd();
    //        process.PerfectKill();
    //    };

    //    process.Disposed += (sender, args) =>
    //    {
    //        exited.TrySetResult(output);
    //        cts.Cancel();
    //        fncOnDispose?.Invoke();
    //    };

    //    process.Start();

    //    return exited.Task;
    //}
}