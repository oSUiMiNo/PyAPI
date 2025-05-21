using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System;
using Newtonsoft.Json.Linq;
using Cysharp.Threading.Tasks;
using UniRx;


public class SharedLog
{
    string LogPath; // 監視するファイルのパス
    DateTime lastWriteTime;
    public Subject<string> OnLog = new Subject<string>();
    public bool isActive = false;

    public SharedLog(string logPath)
    {
        LogPath = logPath;
        CreateLogFileAsync().Forget();
        isActive = true;
    }



    async UniTask CreateLogFileAsync()
    {
        await UniTask.SwitchToThreadPool();

        // 初期化時にログファイルを削除
        if (File.Exists(LogPath))
            try
            {
                File.Delete(LogPath);
                //Debug.Log($"ログファイル削除（初期化時）: {LogPath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"初期化時のログファイル削除に失敗: {e.Message}");
            }

        try
        {
            string directory = Path.GetDirectoryName(LogPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            File.Create(LogPath).Close();
            //Debug.Log($"ログファイル作成: {LogPath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"ログファイル作成失敗: {e.Message}");
            return;
        }

        try
        {
            lastWriteTime = File.GetLastWriteTime(LogPath);
        }
        catch (Exception e)
        {
            Debug.LogError($"最終更新日時取得失敗: {e.Message}");
            return;
        }
        await UniTask.SwitchToMainThread();
    }



    public async void ReadLogFile()
    {
        await UniTask.SwitchToThreadPool();
        //Debug.Log("ログ読み取り");
        //if (!File.Exists(LogPath))
        //    try
        //    {
        //        File.Create(LogPath).Close();
        //        Debug.LogWarning($"ログファイルが削除されたため再作成: {LogPath}");
        //        lastWriteTime = File.GetLastWriteTime(LogPath);
        //    }
        //    catch (Exception e)
        //    {
        //        Debug.LogError($"ログファイル再作成失敗: {e.Message}");
        //        return;
        //    }

        DateTime currentWriteTime = File.GetLastWriteTime(LogPath);

        if (currentWriteTime != lastWriteTime)
        {
            lastWriteTime = currentWriteTime;
            try
            {
                // 未処理部分を保持する変数
                string unprocessedLogs = "";

                // Python と同じtxtファイルを操作する際の競合を防止
                using (FileStream fs = new FileStream(LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (StreamReader sr = new StreamReader(fs))
                {
                    // 区切りごとに分割して処理
                    string[] logs = sr.ReadToEnd().Split(new[] { "___" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string log in logs)
                    {
                        string trimmedLog = log.Trim();
                        if (!string.IsNullOrEmpty(trimmedLog))
                        {
                            OnLog.OnNext(trimmedLog);
                        }
                    }
                }

                // 未処理部分をファイルに安全に書き戻す
                if (!string.IsNullOrEmpty(unprocessedLogs))
                {
                    using (FileStream fs = new FileStream(LogPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite))
                    {
                        fs.SetLength(0); // ファイル内容をクリア
                        using (StreamWriter sw = new StreamWriter(fs))
                        {
                            sw.Write(unprocessedLogs.Trim());
                        }
                    }
                }
                else
                {
                    // 全て処理済みならファイルを完全にクリア
                    using (FileStream fs = new FileStream(LogPath, FileMode.Truncate, FileAccess.Write, FileShare.ReadWrite))
                    {
                        // FileMode.Truncate を使用すると、ファイルを開いた瞬間にその内容が自動的に削除され、ファイルサイズが0にリセットされる。この箇所に具体的な処理を書く必要は無い
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"ログ読み取りエラー: {e.Message}");
            }
        }
        await UniTask.SwitchToMainThread();
    }


    // ログファイル削除
    public async void Close()
    {
        await UniTask.SwitchToThreadPool();
        isActive = false;
        OnLog.OnCompleted();
        //OnLog.Dispose();
        await UniTask.Delay(1);
        if (File.Exists(LogPath))
        try
        {
            File.Delete(LogPath);
            Debug.Log($"ログファイル削除（終了時）{LogPath} {File.Exists(LogPath)}");
        }
        catch (Exception e)
        {
            Debug.LogError($"終了時のログファイル削除に失敗: {e.Message}");
        }
        GC.Collect();
        await UniTask.SwitchToMainThread();
    }
}
