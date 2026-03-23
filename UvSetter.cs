using Cysharp.Threading.Tasks;
using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Debug = UnityEngine.Debug;



///*******************************************************<summary>
/// uv による Python 環境セットアップ
/// pyenv + venv + pip を uv 単体で代替する
///</summary>******************************************************
public class UvSetter
{
    ///==============================================<summary>
    /// 指定フォルダで uv sync --frozen を実行するフロー
    ///</summary>=============================================
    public static async UniTask Exe(string dir, string ver)
    {
        try
        {
            Debug.Log($"uv セットアップ開始...\n{dir}");
            //--------------------------------------
            // uv のインストールが未だならインストール
            //--------------------------------------
            try
            {
                await IsInstalled_Uv();
            }
            catch (Exception e)
            {
                Debug.Log($"uv がインストールされていない\n{e}");
                await InstallUv();
            }
            //--------------------------------------
            // 指定フォルダが存在しなければ作成
            //--------------------------------------
            if (!Directory.Exists(dir))
            {
                Debug.Log($"フォルダが存在しないため作成：{dir}");
                Directory.CreateDirectory(dir);
            }
            //--------------------------------------
            // uv sync --frozen で環境を同期
            //--------------------------------------
            await Sync(dir);
        }
        catch { throw; }
        Debug.Log($"uv セットアップ完了！\n{dir}");
    }


    ///==============================================<summary>
    /// uv がインストールされているか確認
    ///</summary>=============================================
    static async UniTask IsInstalled_Uv()
    {
        Debug.Log($"uv バージョン確認開始...");
        string version = await PowerShellAPI.Command("uv --version");
        Debug.Log($"uv バージョン確認完了！\n{version}");
    }


    ///==============================================<summary>
    /// uv をインストール
    ///</summary>=============================================
    static async UniTask InstallUv()
    {
        Debug.Log($"uv インストール開始...");
        string result = await PowerShellAPI.Command(
            "Set-ExecutionPolicy RemoteSigned -Scope Process -Force; " +
            "irm https://astral.sh/uv/install.ps1 | iex"
        );
        Debug.Log($"uv インストール完了！\n{result}");
        RefreshPathForCurrentProcess();
    }


    ///==============================================<summary>
    /// uv インストール後にプロセスの PATH を更新
    ///</summary>=============================================
    static void RefreshPathForCurrentProcess()
    {
        Debug.Log($"PATH 更新開始...");
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var uvBin = Path.Combine(userProfile, ".local", "bin");

        var current = Environment.GetEnvironmentVariable("PATH") ?? "";
        var paths = current.Split(';', StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim())
                    .ToList();

        if (!paths.Any(p => string.Equals(p, uvBin, StringComparison.OrdinalIgnoreCase)))
            paths.Insert(0, uvBin);

        var updated = string.Join(";", paths);
        Environment.SetEnvironmentVariable("PATH", updated, EnvironmentVariableTarget.Process);
        Debug.Log($"PATH 更新完了！");
    }


    ///==============================================<summary>
    /// uv sync --frozen で環境を同期
    ///</summary>=============================================
    static async UniTask Sync(string dir)
    {
        Debug.Log($"uv sync 開始...\nディレクトリ：{dir}");
        string result = await PowerShellAPI.Command("uv sync --frozen", dir);
        Debug.Log($"uv sync 完了！\n{result}");
    }
}
