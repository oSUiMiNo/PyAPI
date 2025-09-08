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
    /// �w��t�H���_�� pyenv local �o�[�W������ݒ肷��t���[
    ///</summary>=============================================
    public static async UniTask ExeFlow(string dir, string ver)
    {
        try
        {
            Debug.Log($"Pyenv �Z�b�g�A�b�v�J�n...\n{dir}");

            //-----------------------------------------
            // pyenv �̃C���X�g�[���������Ȃ�C���X�g�[��
            //-----------------------------------------
            try
            {
                await IsInstalled_PyEnv();
            }
            catch (Exception e)
            {
                Debug.Log($"pyenv ���C���X�g�[������Ă��Ȃ�\n {e}");
                await InstallPyEnv();
            }
            //-----------------------------------------
            // �w��t�H���_�����݂��Ȃ���΍쐬
            //-----------------------------------------
            if (!Directory.Exists(dir))
            {
                Debug.Log($"�t�H���_�����݂��Ȃ����ߍ쐬�F{dir}");
                Directory.CreateDirectory(dir);
            }
            //-----------------------------------------
            // pyenv �Ɏw��o�[�W�������C���X�g�[������Ă��Ȃ���΃C���X�g�[��
            //-----------------------------------------
            try
            {
                await IsInstalled_PyVer(ver);
            }
            catch
            {
                Debug.Log($"Python {ver} ���C���X�g�[������Ă��Ȃ�");
                await InstallPyVer(ver);
            }
            //if (!await IsInstalled_PyVer(ver))
            //{
            //    Debug.Log($"Python {ver} ���C���X�g�[������Ă��Ȃ�");
            //    await InstallPyVer(ver);
            //}
            //else
            //{
            //    Debug.Log($"Python {ver} �͊��ɃC���X�g�[������Ă���");
            //}
            //-----------------------------------------
            // pyenv local���w��o�[�W�����ɐݒ�
            //-----------------------------------------
            await SetLocalVer(dir, ver);
        }
        catch { throw; }
        //{
        //    //throw new Exception($"�G���[: {e.Message}");
        //    //Debug.LogError($"�G���[: {e.Message}");
        //}
        Debug.Log($"PyEnv �Z�b�g�A�b�v����\n{dir}");
    }


    ///==============================================<summary>
    /// Python 3.12.5���C���X�g�[������Ă��邩�m�F
    ///</summary>=============================================
    static async UniTask IsInstalled_PyEnv()
    {
        Debug.Log($"pyenv �o�[�W�����m�F�J�n...");
        string version = await PowerShellAPI.Command("pyenv --version");
        Debug.Log($"pyenv �o�[�W�����m�F�����@Ver�F{version}");
    }


    ///==============================================<summary>
    /// Python 3.12.5���C���X�g�[��
    ///</summary>=============================================
    static async UniTask InstallPyEnv()
    {
        Debug.Log($"pyenv �C���X�g�[���J�n...");

        // C:/Users/[���[�U��] �t�H���_
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

        //// pyenv �C���X�g�[���R�}���h
        //string result = await PowerShellAPI.Command(
        //    // �O���X�N���v�g�̎��s������
        //    "Set-ExecutionPolicy RemoteSigned -Scope Process" +
        //    // ���₪�����珳�F
        //    " -Force;" +
        //    // �C���X�g�[���p .bat �� DL
        //    "Invoke-WebRequest -UseBasicParsing -Uri \"https://raw.githubusercontent.com/pyenv-win/pyenv-win/master/pyenv-win/install-pyenv-win.ps1\" " +
        //    // �C���X�g�[���p .bat ����C���X�g�[��
        //    "-OutFile \"./install-pyenv-win.ps1\"; & \"./install-pyenv-win.ps1\"" +
        //    //// ���₪�����珳�F
        //    //" -Force",
        //    // C:/Users/[���[�U��] �t�H���_�Ŏ��s (�ǂ��Ŏ��s���Ă� C:/Users/[���[�U��] �ɃC���X�g�[�������)
        //    usersDir
        //);
        
        Debug.Log($"pyenv �C���X�g�[������\n{result}");

        // �C���X�g�[������Ɍ��݃v���Z�X�� PATH ���X�V
        RefreshPathForCurrentProcess();
    }

    static void RefreshPathForCurrentProcess()
    {
        Debug.Log($"PATH �X�V�J�n");
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var pyenvRoot = Path.Combine(userProfile, ".pyenv", "pyenv-win");
        var bin = Path.Combine(pyenvRoot, "bin");
        var shims = Path.Combine(pyenvRoot, "shims");

        var current = Environment.GetEnvironmentVariable("PATH") ?? "";

        // �Z�~�R�����ŕ������ă��X�g���i�啶����������ʂ��Ȃ��j
        var paths = current.Split(';', StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim())
                    .ToList();

        // ���ɓ����Ă��Ȃ���ΐ擪�ɒǉ�
        if (!paths.Any(p => string.Equals(p, bin, StringComparison.OrdinalIgnoreCase)))
            paths.Insert(0, bin);

        if (!paths.Any(p => string.Equals(p, shims, StringComparison.OrdinalIgnoreCase)))
            paths.Insert(0, shims);

        var updated = string.Join(";", paths);
        Environment.SetEnvironmentVariable("PATH", updated, EnvironmentVariableTarget.Process);
        Debug.Log($"PATH �X�V����");
    }


    ///==============================================<summary>
    /// Python 3.12.5���C���X�g�[������Ă��邩�m�F
    ///</summary>=============================================
    static async UniTask IsInstalled_PyVer(string ver)
    {
        Debug.Log($"pyenv �C���X�g�[���� Python �o�[�W�����m�F�J�n...");
        string result = await PowerShellAPI.Command("pyenv versions");
        Debug.Log($"pyenv �C���X�g�[���� Python �o�[�W�����m�F����\n�ꗗ�F\n {result}");
    }


    ///==============================================<summary>
    /// Python 3.12.5���C���X�g�[��
    ///</summary>=============================================
    static async UniTask InstallPyVer(string ver)
    {
        Debug.Log($"Python {ver} �C���X�g�[���J�n...");
        string result = await PowerShellAPI.Command($"pyenv install {ver}");
        Debug.Log($"Python {ver} �C���X�g�[������");
    }


    ///==============================================<summary>
    /// �w��t�H���_�� pyenv local��ݒ�
    ///</summary>=============================================
    static async UniTask SetLocalVer(string dir, string ver)
    {
        Debug.Log($"{dir} �� pyenv local �� {ver} �ɐݒ�J�n...");
        string result = await PowerShellAPI.Command($"pyenv local {ver}", dir);
        Debug.Log($"{dir} �� pyenv local �� {ver} �ɐݒ芮��");

        //-----------------------------------------
        // .python-version�t�@�C�����쐬���ꂽ���m�F
        //-----------------------------------------
        string verFile = $"{dir}/.python-version";
        if (File.Exists(verFile))
        {
            string content = await File.ReadAllTextAsync(verFile);
            Debug.Log($".python-version �t�@�C�����쐬���ꂽ�F{content.Trim()}");
        }
        else
        {
            throw new Exception($".python-version �t�@�C�����쐬����Ȃ�����");
        }
    }
}