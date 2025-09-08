using Cysharp.Threading.Tasks;
using System;
using System.Text;


//****************************************************************
// Power Shell �̃R�[�h�����s���� API
//****************************************************************
public static class PowerShellAPI
{
    // PowerShell�� ���s�t�@�C��
    static string PsExeFile {
        get {
            // PowerShell 7 (pwsh.exe) �������邩�`�F�b�N
            string pwsh = "pwsh.exe";
            if (pwsh.Exe_Is_In_PATH()) return pwsh;
            // �Ȃ���� Windows PowerShell (powershell.exe)
            return "powershell.exe";
        }
    }


    ///==============================================<summary>
    /// �R�}���h�����s
    ///</summary>=============================================
    public static async UniTask<string> Command(string command, string workingDir = null, float timeout = 0)
    {
        // �o�͂�UTF-8�ɌŒ肵�Ă���G���R�[�h�iPowerShell 5�΍�j
        command = "[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; " + command;
        // ���S�FUTF-16LE -> Base64 �� -EncodedCommand
        command = Convert.ToBase64String(Encoding.Unicode.GetBytes(command));

        System.Diagnostics.Process process = new()
        {
            StartInfo = new(PsExeFile)
            {
                Arguments = $"-NoProfile -ExecutionPolicy Bypass" +
                            $" -EncodedCommand {command}",
                WorkingDirectory = workingDir ?? Environment.CurrentDirectory,
                UseShellExecute = false, // �V�F�����g�p���Ȃ�
                RedirectStandardOutput = true, // �W���o�͂����_�C���N�g
                RedirectStandardError = true, // �W���G���[�����_�C���N�g
                CreateNoWindow = true, // PowerShell�E�B���h�E��\�����Ȃ�
                StandardOutputEncoding = Encoding.UTF8, // �o�͂̕��������h�~
                StandardErrorEncoding = Encoding.UTF8, // �G���[���b�Z�̕��������h�~
            }
        };
        return await process.ExeAsync_Light(timeout);
    }


    ///==============================================<summary>
    /// .ps1 �X�N���v�g�����s
    ///</summary>=============================================
    public static async UniTask<string> Script(string scriptPath, float timeout = 0)
    {
        // ' �� '' �ɓ�d���iPowerShell�K���j
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
                UseShellExecute = false, // �V�F�����g�p���Ȃ�
                RedirectStandardOutput = true, // �W���o�͂����_�C���N�g
                RedirectStandardError = true, // �W���G���[�����_�C���N�g
                CreateNoWindow = true, // PowerShell�E�B���h�E��\�����Ȃ�
                StandardOutputEncoding = Encoding.UTF8, // �o�͂̕��������h�~
                StandardErrorEncoding = Encoding.UTF8, // �G���[���b�Z�̕��������h�~
            }
        };
        return await process.ExeAsync_Light(timeout);
    }
}
