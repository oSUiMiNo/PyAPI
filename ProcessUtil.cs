using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using System.IO;
using System;
using System.Text;


public static class ProcessUtil
{
    ///==============================================<summary>
    /// 実行ファイルが環境PATH上に存在するか確認
    ///</summary>=============================================
    public static bool Exe_Is_In_PATH(this string exeName)
    {
        string[] paths = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator);
        if (paths == null) return false;

        foreach (var path in paths)
        {
            string fullPath = $"{path}/{exeName}";
            if (File.Exists(fullPath)) return true;
        }
        return false;
    }


    ///==============================================<summary>
    /// 引数的な感じで JObject を渡しプロセスを実行
    ///</summary>=============================================
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


    ///==============================================<summary>
    /// プロセスの非同期実行 (外部からキャンセル可)
    ///</summary>=============================================
    public static async UniTask ExeAsync(this System.Diagnostics.Process process, float timeout = 0, Action fncOnDispose = null, CancellationToken externalCT = default)
    {
        await UniTask.SwitchToThreadPool();
        var timeoutCTS = new CancellationTokenSource();
        var exited = new UniTaskCompletionSource();

        //-----------------------------------------
        // タイムアウト時間が設定されている場合は登録
        //-----------------------------------------
        if (timeout > 0)
        {
            UniTask.RunOnThreadPool(() => process.Timeout(timeout, timeoutCTS.Token)).Forget();
        }

        //-----------------------------------------
        // プロセス終了時処理
        //-----------------------------------------
        // Exited (プロセス終了) イベントを有効化
        process.EnableRaisingEvents = true;
        process.Exited += async (sender, args) =>
        {
            // イベント発火タイミングのズレによるエラー防止で一旦確実に終了を待つ
            process.WaitForExit();
            // エラー読取り -> ログ出力
            string e = await process.StandardError.ReadToEndAsync();
            if (!string.IsNullOrEmpty(e)) throw new Exception($"プロセスエラー：{e}");
            // 実行結果の出力をセット
            exited.TrySetResult();
            // プロセス抹消
            process.PerfectKill();
        };

        //-----------------------------------------
        // プロセス抹消時処理
        //-----------------------------------------
        process.Disposed += (sender, args) =>
        {
            timeoutCTS.Cancel();
            fncOnDispose?.Invoke();
        };

        //-----------------------------------------
        // 実行 -> 失敗した場合未完了タスクを残さない
        //-----------------------------------------
        try
        {
            // Start 失敗を確実に表面化
            if (!process.Start())
            {
                timeoutCTS.Cancel();
                try 
                {
                    process.Dispose();
                }
                catch { }
                throw new Exception("プロセス実行失敗");
            }
        }
        catch (Exception)
        {
            timeoutCTS.Cancel();
            try
            {
                process.Dispose();
            }
            catch { }
            // 終了し await に進まない
            throw;
        }

        //-----------------------------------------
        // 外部からのキャンセルを設定
        //-----------------------------------------
        try
        {
            // 外部キャンセルを反映
            await exited.Task.AttachExternalCancellation(externalCT);
            Debug.Log($"プロセス完了");
        }

        //-----------------------------------------
        // 外部からキャンセルされた場合に走る処理
        //-----------------------------------------
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


    ///==============================================<summary>
    /// プロセスの非同期実行 (簡易版)
    /// ReadToEnd() のバッファが小さいので
    /// [ 大出力を受け取ったり高速継続実行する処理 ] では NG
    ///</summary>=============================================
    public static async UniTask<string> ExeAsync_Light(this System.Diagnostics.Process process, float timeout = 0, Action fncOnDispose = null)
    {
        var timeoutCTS = new CancellationTokenSource();
        var exited = new UniTaskCompletionSource<string>();

        //-----------------------------------------
        // タイムアウト時間が設定されている場合は登録
        //-----------------------------------------
        if (timeout > 0)
        {
            UniTask.RunOnThreadPool(() => process.Timeout(timeout, timeoutCTS.Token)).Forget();
        }

        StringBuilder sbOut = new ();
        StringBuilder sbErr = new ();

        //-----------------------------------------
        // 行単位で詰まり回避
        //-----------------------------------------
        process.OutputDataReceived += (_, e) => { if (e.Data != null) sbOut.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) sbErr.AppendLine(e.Data); };

        if (!process.Start())
        {
            throw new Exception("プロセス起動に失敗");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        //-----------------------------------------
        // 終了待ち（イベントを使わない）
        //-----------------------------------------
        UniTask waitExit = UniTask.RunOnThreadPool(() =>
        {
            process.WaitForExit();           // 本体終了待ち
            process.WaitForExit(200);        // I/O ドレインの猶予
        });
        await waitExit;

        int code = process.ExitCode;
        string stdout = sbOut.ToString();
        string stderr = sbErr.ToString();

        try
        {
            process.PerfectKill();
        }
        catch { }

        if (code != 0)
        {
            throw new Exception($"ExitCode={code}\n{stderr}");
        }
        // 警告は返り値に付ける
        else
        if (!string.IsNullOrEmpty(stderr))
        {
            stdout += $"ExitCode={code} [警告]\n{stderr}";
        }

        return stdout;
    }



    //public static async UniTask<string> ExeAsync_Light(this System.Diagnostics.Process process, float timeout = 0, Action fncOnDispose = null)
    //{
    //    string output = "";
    //    var timeoutCTS = new CancellationTokenSource();
    //    var exited = new UniTaskCompletionSource<string>();

    //    //-----------------------------------------
    //    // タイムアウト時間が設定されている場合は登録
    //    //-----------------------------------------
    //    if (timeout > 0)
    //    {
    //        UniTask.RunOnThreadPool(() => process.Timeout(timeout, timeoutCTS.Token)).Forget();
    //    }

    //    //-----------------------------------------
    //    // プロセス終了時処理登録
    //    //-----------------------------------------
    //    // Exited イベントを有効化
    //    process.EnableRaisingEvents = true;
    //    //process.Exited += async (sender, args) =>
    //    //{
    //    //    try
    //    //    {
    //    //        // イベント発火タイミングのズレによるエラー防止で一旦確実に終了を待つ
    //    //        process.WaitForExit();
    //    //        Debug.Log($"プロセス0");
    //    //        // エラー読み取り
    //    //        string stdErr = await process.StandardError.ReadToEndAsync();
    //    //        // 結果読み取り
    //    //        string stdOut = await process.StandardOutput.ReadToEndAsync();
    //    //        Debug.Log($"プロセス1");

    //    //        int code = process.ExitCode;

    //    //        // 成功/失敗をここで完了させる
    //    //        if (code != 0 || !string.IsNullOrEmpty(stdErr))
    //    //            exited.TrySetException(new Exception($"ExitCode={code}\n{stdErr}"));
    //    //        else
    //    //            exited.TrySetResult(stdOut);
    //    //    }
    //    //    catch (Exception e)
    //    //    {
    //    //        exited.TrySetException(e);
    //    //    }
    //    //    finally
    //    //    {
    //    //        timeoutCTS.Cancel();
    //    //        try
    //    //        {
    //    //            process.PerfectKill();
    //    //        }
    //    //        catch { }
    //    //    }
    //    //};

    //    //-----------------------------------------
    //    // プロセス抹消時処理登録
    //    //-----------------------------------------
    //    process.Disposed += (sender, args) =>
    //    {
    //        exited.TrySetResult(output);
    //        timeoutCTS.Cancel();
    //        fncOnDispose?.Invoke();
    //    };

    //    //-----------------------------------------
    //    // 実行 -> 失敗した場合未完了タスクを残さない
    //    //-----------------------------------------
    //    try
    //    {
    //        // Start 失敗を確実に表面化
    //        if (!process.Start())
    //        {
    //            exited.TrySetException(new Exception("プロセス実行失敗"));
    //            timeoutCTS.Cancel();
    //            try
    //            {
    //                process.PerfectKill();
    //            }
    //            catch { }
    //            return await exited.Task;
    //        }
    //    }
    //    catch (Exception ex)
    //    {
    //        exited.TrySetException(ex);
    //        timeoutCTS.Cancel();
    //        try
    //        {
    //            process.PerfectKill();
    //        }
    //        catch { }
    //        return await exited.Task;
    //    }

    //    //-----------------------------------------
    //    // 終了待ち（イベントを使わない）
    //    //-----------------------------------------
    //    UniTask waitExit = UniTask.RunOnThreadPool(() =>
    //    {
    //        process.WaitForExit();           // 本体終了待ち
    //        process.WaitForExit(200);        // I/O ドレインの猶予
    //    });
    //    await waitExit;

    //    try
    //    {
    //        // イベント発火タイミングのズレによるエラー防止で一旦確実に終了を待つ
    //        Debug.Log($"プロセス0");
    //        // エラー読み取り
    //        string stdErr = await process.StandardError.ReadToEndAsync();
    //        // 結果読み取り
    //        string stdOut = await process.StandardOutput.ReadToEndAsync();
    //        Debug.Log($"プロセス1");

    //        int code = process.ExitCode;

    //        // 成功/失敗をここで完了させる
    //        if (code != 0 || !string.IsNullOrEmpty(stdErr))
    //            exited.TrySetException(new Exception($"ExitCode={code}\n{stdErr}"));
    //        else
    //            exited.TrySetResult(stdOut);
    //    }
    //    catch (Exception e)
    //    {
    //        exited.TrySetException(e);
    //    }
    //    finally
    //    {
    //        timeoutCTS.Cancel();
    //        try
    //        {
    //            process.PerfectKill();
    //        }
    //        catch { }
    //    }

    //    return await exited.Task;
    //}


    ///==============================================<summary>
    /// タイムアウト
    ///</summary>=============================================
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


    ///==============================================<summary>
    /// プロセス抹消 {
    ///     -> Kill() でプロセスの動作即死
    ///     -> Dipose() でプロセスと周辺資源を管理から解放
    /// }
    /// 
    ///</summary>=============================================
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
        catch (Exception)
        {
            // すでに終了していたら例外無視でOK
        }
        finally
        {
            // ハンドルだけは確実に解放
            process.Dispose();           
        }
    }
}