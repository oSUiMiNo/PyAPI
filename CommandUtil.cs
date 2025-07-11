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
    // ���s�t�@�C���o�^
    //-------------------------------
    // pyenv-win ��z��ibat�^cmd �ǂ���ł� OK�j
    static string PyenvBat => $@"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}\.pyenv\pyenv-win\bin\pyenv.bat";
    static string GitExe => "git";
    static string PythonExe = "python";   // PATH �ɒʂ��Ă���z��

    //================================================
    // �R�}���h���s
    //================================================
    public static async UniTask<string> ExeToolCommand(string command, string workingDir = null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = ExeFile(command),
            Arguments = ToolCommand(command),
            // ���s����f�B���N�g��
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
            throw new Exception($"�G���[�ŃR�}���h�𒆎~ {proc.ExitCode}\n{error}");

        return output.TrimEnd();
    }

    //================================================
    // �R�}���h����c�[���𔻒f�����s�t�@�C����Ԃ�
    //================================================
    static string ExeFile(string command)
    {
        //string toolName = command.TrimStr_R(" ", 100);
        // �擪�̃g�[�N�����擾
        string toolName = command.Split(' ', 2)[0];
        Debug.Log($"�c�[�� {toolName}");
        // ��΃p�X�w��Ȃ炻�̂܂ܕԂ�
        if (IsAbsolutePath(toolName)) return toolName;
        return toolName switch
        {
            "pyenv" => PyenvBat,
            "git" => GitExe,
            "python" => PythonExe,
            _ => throw new Exception($"{toolName} �̎��s�t�@�C���͓o�^����Ă��Ȃ�")
        };
    }

    //================================================
    // �c�[�����ȍ~�̃R�}���h��؂�o��
    //================================================
    static string ToolCommand(string command) => command.CropStr_R(" ");

    //================================================
    // �ǉ�: ��΃p�X���ǂ������ȈՔ���
    //================================================
    static bool IsAbsolutePath(string s) =>
        s.Contains(Path.DirectorySeparatorChar) || s.Contains(Path.AltDirectorySeparatorChar);
}