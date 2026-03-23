using Cysharp.Threading.Tasks;
using Maku;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;


public static class CommandUtil
{
    ///==============================================<summary>
    /// ツールのコマンド実行
    ///</summary>=============================================
    //public static async UniTask<string> ExeToolCommand(string command, string workingDir = null) =>
    //    await PowerShellAPI.Command(command, workingDir);


    // レガシー
    // PowerShell を介さずに直でコマンドを呼ぶ感じ

    ////-----------------------------------------
    //// 実行ファイル登録
    ////-----------------------------------------
    //// pyenv-win を想定（bat／cmd どちらでも OK）
    //static string PyenvBat => $@"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}\.pyenv\pyenv-win\bin\pyenv.bat";
    //static string GitExe => "git";
    //static string PythonExe = "python";   // PATH に通っている想定
    //static string PSExe => "powershell.exe";

    //public static async UniTask<string> ExeToolCommand_Old(string command, string workingDir = null)
    //{
    //    var psi = new ProcessStartInfo
    //    {
    //        FileName = command.ExtractTool(),
    //        Arguments = command.ExtractCommand(),
    //        // 実行するディレクトリ
    //        WorkingDirectory = workingDir ?? Environment.CurrentDirectory,
    //        UseShellExecute = false,
    //        RedirectStandardOutput = true,
    //        RedirectStandardError = true,
    //        CreateNoWindow = true,
    //        StandardOutputEncoding = Encoding.UTF8,
    //        StandardErrorEncoding = Encoding.UTF8
    //    };

    //    using var proc = Process.Start(psi);
    //    string output = await proc.StandardOutput.ReadToEndAsync();
    //    string error = await proc.StandardError.ReadToEndAsync();
    //    await UniTask.WaitUntil(() => proc.HasExited);

    //    if (proc.ExitCode != 0)
    //        throw new Exception($"エラーでコマンドを中止 {proc.ExitCode}\n{error}");

    //    return output.TrimEnd();
    //}

    /////==============================================<summary>
    ///// コマンドからツールを判断し実行ファイルを返す
    /////</summary>=============================================
    //static string ExtractTool(this string command)
    //{
    //    // 先頭のトークンを取得
    //    string toolName = command.Split(' ', 2)[0];
    //    Debug.Log($"ツール {toolName}");
    //    // 絶対パス指定ならそのまま返す
    //    if (toolName.IsAbsolutePath()) return toolName;
    //    return toolName switch
    //    {
    //        "pyenv" => PyenvBat,
    //        "git" => GitExe,
    //        "python" => PythonExe,
    //        _ => PSExe
    //        //_ => throw new Exception($"{toolName} の実行ファイルは登録されていない")
    //    };
    //}


    /////==============================================<summary>
    ///// ツール名以降のコマンドを切り出し
    /////</summary>=============================================
    //static string ExtractCommand(this string command) => command.CropStr_R(" ");


    /////==============================================<summary>
    ///// 追加: 絶対パスかどうかを簡易判定
    /////</summary>=============================================
    //static bool IsAbsolutePath(this string s) =>
    //    s.Contains(Path.DirectorySeparatorChar) || s.Contains(Path.AltDirectorySeparatorChar);
}