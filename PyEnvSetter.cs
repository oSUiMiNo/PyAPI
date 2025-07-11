using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;


public class PyEnvSetter
{
    //================================================
    // 指定フォルダに pyenv local バージョンを設定するフロー
    //================================================
    public static async UniTask ExeFlow(string dir, string ver)
    {
        try
        {
            Debug.Log($"Pyenv セットアップ開始...\n{dir}");
            //-------------------------------
            // 指定フォルダが存在しなければ作成
            //-------------------------------
            if (!Directory.Exists(dir))
            {
                Debug.Log($"フォルダが存在しないため作成します: {dir}");
                Directory.CreateDirectory(dir);
            }
            //-------------------------------
            // pyenv に指定バージョンがインストールされていなければインストール
            //-------------------------------
            bool isInstalled = await InstalledPyVer(ver);
            if (!isInstalled)
            {
                Debug.Log($"Python {ver} がインストールされていない");
                bool installSuccess = await InstallPyVer(ver);
                if (!installSuccess)
                {
                    Debug.LogError($"Python {ver} インストール失敗");
                    return;
                }
            }
            else
            {
                Debug.Log($"Python {ver} は既にインストールされている");
            }
            //-------------------------------
            // pyenv localを指定バージョンに設定
            //-------------------------------
            bool setLocalSuccess = await SetLocalVer(dir, ver);
            if (setLocalSuccess)
            {
                Debug.Log($"pyenv local {ver}の設定が完了しました: {dir}");
            }
            else
            {
                Debug.LogError("pyenv localの設定に失敗しました");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"エラーが発生しました: {e.Message}");
        }
        Debug.Log($"PyEnv セットアップ完了\n{dir}");
    }

    //================================================
    // Python 3.12.5がインストールされているか確認
    //================================================
    static async UniTask<bool> InstalledPyVer(string ver)
    {
        string result = await CommandUtil.ExeToolCommand("pyenv versions");
        Debug.Log($"pyenv バージョン一覧\n {result}");
        return result.Contains(ver);
    }

    //================================================
    // Python 3.12.5をインストール
    //================================================
    static async UniTask<bool> InstallPyVer(string ver)
    {
        Debug.Log($"Python {ver} インストールが開始...");
        string result = await CommandUtil.ExeToolCommand($"pyenv install {ver}");
        Debug.Log($"Python {ver} インストールが完了");
        return true;
    }

    //================================================
    // 指定フォルダに pyenv localを設定
    //================================================
    static async UniTask<bool> SetLocalVer(string targDir, string ver)
    {
        string result = await CommandUtil.ExeToolCommand($"pyenv local {ver}", targDir);

        // .python-versionファイルが作成されたか確認
        string versionFile = $"{targDir}/.python-version";
        if (File.Exists(versionFile))
        {
            string content = await File.ReadAllTextAsync(versionFile);
            Debug.Log($".python-versionファイルが作成された: {content.Trim()}");
            return true;
        }
        return false;
    }
}