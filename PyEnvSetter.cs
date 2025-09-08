using Cysharp.Threading.Tasks;
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;




public class PyEnvSetter
{
    ///==============================================<summary>
    /// 指定フォルダに pyenv local バージョンを設定するフロー
    ///</summary>=============================================
    public static async UniTask ExeFlow(string dir, string ver)
    {
        try
        {
            Debug.Log($"Pyenv セットアップ開始...\n{dir}");

            //-----------------------------------------
            // pyenv のインストールが未だならインストール
            //-----------------------------------------
            await IsInstalled_PyEnv();
            //try
            //{
            //    await IsInstalled_PyEnv();
            //}
            //catch(Exception e)
            //{
            //    throw;
            //    //await InstallPyEnv();
            //}

            //if (!await IsInstalled_PyEnv())
            //{
            //    //await InstallPyEnv();
            //}

            //-----------------------------------------
            // 指定フォルダが存在しなければ作成
            //-----------------------------------------
            if (!Directory.Exists(dir))
            {
                Debug.Log($"フォルダが存在しないため作成: {dir}");
                Directory.CreateDirectory(dir);
            }
            //-----------------------------------------
            // pyenv に指定バージョンがインストールされていなければインストール
            //-----------------------------------------
            if (!await IsInstalled_PyVer(ver))
            {
                Debug.Log($"Python {ver} がインストールされていない");
                await InstallPyVer(ver);
                //try
                //{
                //    await InstallPyVer(ver);
                //}
                //catch { throw; }
                
                //if (!installSuccess)
                //{
                //    throw new Exception($"Python {ver} インストール失敗");
                //    //Debug.LogError($"Python {ver} インストール失敗");
                //    //return;
                //}
            }
            else
            {
                Debug.Log($"Python {ver} は既にインストールされている");
            }
            //-----------------------------------------
            // pyenv localを指定バージョンに設定
            //-----------------------------------------
            bool setLocalSuccess = await SetLocalVer(dir, ver);
            if (setLocalSuccess)
            {
                Debug.Log($"pyenv local {ver}の設定完了: {dir}");
            }
            else
            {
                throw new Exception($"pyenv localの設定失敗");
                //Debug.LogError("pyenv localの設定失敗");
            }
        }
        catch { throw; }
        //{
        //    //throw new Exception($"エラー: {e.Message}");
        //    //Debug.LogError($"エラー: {e.Message}");
        //}
        Debug.Log($"PyEnv セットアップ完了\n{dir}");
    }


    ///==============================================<summary>
    /// Python 3.12.5がインストールされているか確認
    ///</summary>=============================================
    static async UniTask IsInstalled_PyEnv()
    {
        Debug.Log($"pyenv インストール状況確認開始...");
        string version = await PowerShellAPI.Command("pyenv --version");
        Debug.Log($"pyenv インストール状況確認完了　バージョン：{version}");
        Debug.Log($"pyenv 未インストール");
        
        //string version = await CommandUtil.ExeToolCommand("pyenv --version");
        //try
        //{
        //    Debug.Log($"pyenv インストール状況確認開始...");
        //    string version = await CommandUtil.ExeToolCommand("pyenv --version");
        //    Debug.Log($"pyenv インストール状況確認完了　バージョン：{version}");
        //    Debug.Log($"pyenv 未インストール");
        //}
        //catch (Exception e)
        //{
        //    throw;
        //}
    }


    ///==============================================<summary>
    /// Python 3.12.5をインストール
    ///</summary>=============================================
    static async UniTask<bool> InstallPyEnv()
    {
        Debug.Log($"pyenv インストール開始...");
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        string psCmd =
            "Invoke-WebRequest -UseBasicParsing -Uri \"https://raw.githubusercontent.com/pyenv-win/pyenv-win/master/pyenv-win/install-pyenv-win.ps1\" " +
            "-OutFile \"./install-pyenv-win.ps1\"; & \"./install-pyenv-win.ps1\"";

        // インストール直後に現在プロセスの PATH を更新
        RefreshPathForCurrentProcess();

        string result = await PowerShellAPI.Command(psCmd, workingDir: userProfile);
        Debug.Log($"pyenv インストール完了");
        return true;
    }

    static void RefreshPathForCurrentProcess()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var pyenvRoot = Path.Combine(userProfile, ".pyenv", "pyenv-win");
        var bin = Path.Combine(pyenvRoot, "bin");
        var shims = Path.Combine(pyenvRoot, "shims");

        var current = Environment.GetEnvironmentVariable("PATH") ?? "";

        // セミコロンで分割してリスト化（大文字小文字区別しない）
        var paths = current.Split(';', StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim())
                    .ToList();

        // 既に入っていなければ先頭に追加
        if (!paths.Any(p => string.Equals(p, bin, StringComparison.OrdinalIgnoreCase)))
            paths.Insert(0, bin);

        if (!paths.Any(p => string.Equals(p, shims, StringComparison.OrdinalIgnoreCase)))
            paths.Insert(0, shims);

        var updated = string.Join(";", paths);
        Environment.SetEnvironmentVariable("PATH", updated, EnvironmentVariableTarget.Process);
    }



    ///==============================================<summary>
    /// Python 3.12.5がインストールされているか確認
    ///</summary>=============================================
    static async UniTask<bool> IsInstalled_PyVer(string ver)
    {
        Debug.Log($"pyenv インストール済 Python バージョン確認開始...");
        string result = await PowerShellAPI.Command("pyenv versions");
        Debug.Log($"pyenv インストール済 Python バージョン確認完了\n一覧\n {result}");
        return result.Contains(ver);
    }


    ///==============================================<summary>
    /// Python 3.12.5をインストール
    ///</summary>=============================================
    static async UniTask InstallPyVer(string ver)
    {
        Debug.Log($"Python {ver} インストール開始...");
        string result = await PowerShellAPI.Command($"pyenv install {ver}");
        Debug.Log($"Python {ver} インストール完了");
    }


    ///==============================================<summary>
    /// 指定フォルダに pyenv localを設定
    ///</summary>=============================================
    static async UniTask<bool> SetLocalVer(string targDir, string ver)
    {
        string result = await PowerShellAPI.Command($"pyenv local {ver}", targDir);

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