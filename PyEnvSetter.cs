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
            try
            {
                await IsInstalled_PyEnv();
            }
            catch (Exception e)
            {
                Debug.Log($"pyenv がインストールされていない\n {e}");
                await InstallPyEnv();
            }
            //-----------------------------------------
            // 指定フォルダが存在しなければ作成
            //-----------------------------------------
            if (!Directory.Exists(dir))
            {
                Debug.Log($"フォルダが存在しないため作成：{dir}");
                Directory.CreateDirectory(dir);
            }
            //-----------------------------------------
            // pyenv に指定バージョンがインストールされていなければインストール
            //-----------------------------------------
            try
            {
                await IsInstalled_PyVer(ver);
            }
            catch
            {
                Debug.Log($"Python {ver} がインストールされていない");
                await InstallPyVer(ver);
            }
            //if (!await IsInstalled_PyVer(ver))
            //{
            //    Debug.Log($"Python {ver} がインストールされていない");
            //    await InstallPyVer(ver);
            //}
            //else
            //{
            //    Debug.Log($"Python {ver} は既にインストールされている");
            //}
            //-----------------------------------------
            // pyenv localを指定バージョンに設定
            //-----------------------------------------
            await SetLocalVer(dir, ver);
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
        Debug.Log($"pyenv バージョン確認開始...");
        string version = await PowerShellAPI.Command("pyenv --version");
        Debug.Log($"pyenv バージョン確認完了　Ver：{version}");
    }


    ///==============================================<summary>
    /// Python 3.12.5をインストール
    ///</summary>=============================================
    static async UniTask InstallPyEnv()
    {
        Debug.Log($"pyenv インストール開始...");

        // C:/Users/[ユーザ名] フォルダ
        string usersDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        string ps = @"
$ErrorActionPreference = 'Stop';
Set-Location -LiteralPath $env:USERPROFILE;
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12;
Set-ExecutionPolicy RemoteSigned -Scope Process -Force;

$dl = Join-Path $env:TEMP 'install-pyenv-win.ps1';
Invoke-WebRequest -UseBasicParsing -Uri 'https://raw.githubusercontent.com/pyenv-win/pyenv-win/master/pyenv-win/install-pyenv-win.ps1' -OutFile $dl;
& $dl;
exit 0
";

        string result = await PowerShellAPI.Command(ps, usersDir);

        //// pyenv インストールコマンド
        //string result = await PowerShellAPI.Command(
        //    // 外部スクリプトの実行が許可
        //    "Set-ExecutionPolicy RemoteSigned -Scope Process" +
        //    // 質問が来たら承認
        //    " -Force;" +
        //    // インストール用 .bat を DL
        //    "Invoke-WebRequest -UseBasicParsing -Uri \"https://raw.githubusercontent.com/pyenv-win/pyenv-win/master/pyenv-win/install-pyenv-win.ps1\" " +
        //    // インストール用 .bat からインストール
        //    "-OutFile \"./install-pyenv-win.ps1\"; & \"./install-pyenv-win.ps1\"" +
        //    //// 質問が来たら承認
        //    //" -Force",
        //    // C:/Users/[ユーザ名] フォルダで実行 (どこで実行しても C:/Users/[ユーザ名] にインストールされる)
        //    usersDir
        //);
        
        Debug.Log($"pyenv インストール完了\n{result}");

        // インストール直後に現在プロセスの PATH を更新
        RefreshPathForCurrentProcess();
    }

    static void RefreshPathForCurrentProcess()
    {
        Debug.Log($"PATH 更新開始");
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
        Debug.Log($"PATH 更新完了");
    }


    ///==============================================<summary>
    /// Python 3.12.5がインストールされているか確認
    ///</summary>=============================================
    static async UniTask IsInstalled_PyVer(string ver)
    {
        Debug.Log($"pyenv インストール済 Python バージョン確認開始...");
        string result = await PowerShellAPI.Command("pyenv versions");
        Debug.Log($"pyenv インストール済 Python バージョン確認完了\n一覧：\n {result}");
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
    static async UniTask SetLocalVer(string dir, string ver)
    {
        Debug.Log($"{dir} の pyenv local を {ver} に設定開始...");
        string result = await PowerShellAPI.Command($"pyenv local {ver}", dir);
        Debug.Log($"{dir} の pyenv local を {ver} に設定完了");

        //-----------------------------------------
        // .python-versionファイルが作成されたか確認
        //-----------------------------------------
        string verFile = $"{dir}/.python-version";
        if (File.Exists(verFile))
        {
            string content = await File.ReadAllTextAsync(verFile);
            Debug.Log($".python-version ファイルが作成された：{content.Trim()}");
        }
        else
        {
            throw new Exception($".python-version ファイルが作成されなかった");
        }
    }
}