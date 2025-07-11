using Cysharp.Threading.Tasks;
using MyUtil;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;


public static class CommandUtil
{
    //-------------------------------
    // 実行ファイル登録
    //-------------------------------
    // pyenv-win を想定（bat／cmd どちらでも OK）
    static string PyenvBat => $@"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}\.pyenv\pyenv-win\bin\pyenv.bat";
    static string GitExe => "git";
    static string PythonExe = "python";   // PATH に通っている想定

    //================================================
    // コマンド実行
    //================================================
    public static async UniTask<string> ExeToolCommand(string command, string workingDir = null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = ExeFile(command),
            Arguments = ToolCommand(command),
            // 実行するディレクトリ
            WorkingDirectory = workingDir ?? Environment.CurrentDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var proc = Process.Start(psi);
        string output = await proc.StandardOutput.ReadToEndAsync();
        string error = await proc.StandardError.ReadToEndAsync();
        await UniTask.WaitUntil(() => proc.HasExited);

        if (proc.ExitCode != 0)
            throw new Exception($"エラーでコマンドを中止 {proc.ExitCode}\n{error}");

        return output.TrimEnd();
    }

    //================================================
    // コマンドからツールを判断し実行ファイルを返す
    //================================================
    static string ExeFile(string command)
    {
        //string toolName = command.TrimStr_R(" ", 100);
        // 先頭のトークンを取得
        string toolName = command.Split(' ', 2)[0];
        Debug.Log($"ツール {toolName}");
        // 絶対パス指定ならそのまま返す
        if (IsAbsolutePath(toolName)) return toolName;
        return toolName switch
        {
            "pyenv" => PyenvBat,
            "git" => GitExe,
            "python" => PythonExe,
            _ => throw new Exception($"{toolName} の実行ファイルは登録されていない")
        };
    }

    //================================================
    // ツール名以降のコマンドを切り出し
    //================================================
    static string ToolCommand(string command) => command.CropStr_R(" ");

    //================================================
    // 追加: 絶対パスかどうかを簡易判定
    //================================================
    static bool IsAbsolutePath(string s) =>
        s.Contains(Path.DirectorySeparatorChar) || s.Contains(Path.AltDirectorySeparatorChar);
}