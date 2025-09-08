using Cysharp.Threading.Tasks;
using System;
using System.Text;


//****************************************************************
// Power Shell のコードを実行する API
//****************************************************************
public static class PowerShellAPI
{
    // PowerShellの 実行ファイル
    static string PsExeFile {
        get {
            // PowerShell 7 (pwsh.exe) が見つかるかチェック
            string pwsh = "pwsh.exe";
            if (pwsh.Exe_Is_In_PATH()) return pwsh;
            // なければ Windows PowerShell (powershell.exe)
            return "powershell.exe";
        }
    }


    ///==============================================<summary>
    /// コマンドを実行
    ///</summary>=============================================
    public static async UniTask<string> Command(string command, string workingDir = null, float timeout = 0)
    {
        // 出力をUTF-8に固定してからエンコード（PowerShell 5対策）
        command = "[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; " + command;
        // 安全：UTF-16LE -> Base64 で -EncodedCommand
        command = Convert.ToBase64String(Encoding.Unicode.GetBytes(command));

        System.Diagnostics.Process process = new()
        {
            StartInfo = new(PsExeFile)
            {
                Arguments = $"-NoProfile -ExecutionPolicy Bypass" +
                            $" -EncodedCommand {command}",
                WorkingDirectory = workingDir ?? Environment.CurrentDirectory,
                UseShellExecute = false, // シェルを使用しない
                RedirectStandardOutput = true, // 標準出力をリダイレクト
                RedirectStandardError = true, // 標準エラーをリダイレクト
                CreateNoWindow = true, // PowerShellウィンドウを表示しない
                StandardOutputEncoding = Encoding.UTF8, // 出力の文字化け防止
                StandardErrorEncoding = Encoding.UTF8, // エラーメッセの文字化け防止
            }
        };
        return await process.ExeAsync_Light(timeout);
    }


    ///==============================================<summary>
    /// .ps1 スクリプトを実行
    ///</summary>=============================================
    public static async UniTask<string> Script(string scriptPath, float timeout = 0)
    {
        // ' を '' に二重化（PowerShell規則）
        scriptPath = scriptPath.Replace("'", "''");

        System.Diagnostics.Process process = new()
        {
            StartInfo = new(PsExeFile)
            {
                Arguments =
                    $"-NoProfile -ExecutionPolicy Bypass " +
                    $"[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; " +
                    $"-Command \"chcp 65001; " +
                    $"& '{scriptPath}'\"",
                UseShellExecute = false, // シェルを使用しない
                RedirectStandardOutput = true, // 標準出力をリダイレクト
                RedirectStandardError = true, // 標準エラーをリダイレクト
                CreateNoWindow = true, // PowerShellウィンドウを表示しない
                StandardOutputEncoding = Encoding.UTF8, // 出力の文字化け防止
                StandardErrorEncoding = Encoding.UTF8, // エラーメッセの文字化け防止
            }
        };
        return await process.ExeAsync_Light(timeout);
    }
}
