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
    /// �c�[���̃R�}���h���s
    ///</summary>=============================================
    //public static async UniTask<string> ExeToolCommand(string command, string workingDir = null) =>
    //    await PowerShellAPI.Command(command, workingDir);


    // ���K�V�[
    // PowerShell ������ɒ��ŃR�}���h���ĂԊ���

    ////-----------------------------------------
    //// ���s�t�@�C���o�^
    ////-----------------------------------------
    //// pyenv-win ��z��ibat�^cmd �ǂ���ł� OK�j
    //static string PyenvBat => $@"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}\.pyenv\pyenv-win\bin\pyenv.bat";
    //static string GitExe => "git";
    //static string PythonExe = "python";   // PATH �ɒʂ��Ă���z��
    //static string PSExe => "powershell.exe";

    //public static async UniTask<string> ExeToolCommand_Old(string command, string workingDir = null)
    //{
    //    var psi = new ProcessStartInfo
    //    {
    //        FileName = command.ExtractTool(),
    //        Arguments = command.ExtractCommand(),
    //        // ���s����f�B���N�g��
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
    //        throw new Exception($"�G���[�ŃR�}���h�𒆎~ {proc.ExitCode}\n{error}");

    //    return output.TrimEnd();
    //}

    /////==============================================<summary>
    ///// �R�}���h����c�[���𔻒f�����s�t�@�C����Ԃ�
    /////</summary>=============================================
    //static string ExtractTool(this string command)
    //{
    //    // �擪�̃g�[�N�����擾
    //    string toolName = command.Split(' ', 2)[0];
    //    Debug.Log($"�c�[�� {toolName}");
    //    // ��΃p�X�w��Ȃ炻�̂܂ܕԂ�
    //    if (toolName.IsAbsolutePath()) return toolName;
    //    return toolName switch
    //    {
    //        "pyenv" => PyenvBat,
    //        "git" => GitExe,
    //        "python" => PythonExe,
    //        _ => PSExe
    //        //_ => throw new Exception($"{toolName} �̎��s�t�@�C���͓o�^����Ă��Ȃ�")
    //    };
    //}


    /////==============================================<summary>
    ///// �c�[�����ȍ~�̃R�}���h��؂�o��
    /////</summary>=============================================
    //static string ExtractCommand(this string command) => command.CropStr_R(" ");


    /////==============================================<summary>
    ///// �ǉ�: ��΃p�X���ǂ������ȈՔ���
    /////</summary>=============================================
    //static bool IsAbsolutePath(this string s) =>
    //    s.Contains(Path.DirectorySeparatorChar) || s.Contains(Path.AltDirectorySeparatorChar);
}