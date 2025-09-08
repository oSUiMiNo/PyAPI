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
            // �w��t�H���_�����݂��Ȃ���΍쐬
            //-----------------------------------------
            if (!Directory.Exists(dir))
            {
                Debug.Log($"�t�H���_�����݂��Ȃ����ߍ쐬: {dir}");
                Directory.CreateDirectory(dir);
            }
            //-----------------------------------------
            // pyenv �Ɏw��o�[�W�������C���X�g�[������Ă��Ȃ���΃C���X�g�[��
            //-----------------------------------------
            if (!await IsInstalled_PyVer(ver))
            {
                Debug.Log($"Python {ver} ���C���X�g�[������Ă��Ȃ�");
                await InstallPyVer(ver);
                //try
                //{
                //    await InstallPyVer(ver);
                //}
                //catch { throw; }
                
                //if (!installSuccess)
                //{
                //    throw new Exception($"Python {ver} �C���X�g�[�����s");
                //    //Debug.LogError($"Python {ver} �C���X�g�[�����s");
                //    //return;
                //}
            }
            else
            {
                Debug.Log($"Python {ver} �͊��ɃC���X�g�[������Ă���");
            }
            //-----------------------------------------
            // pyenv local���w��o�[�W�����ɐݒ�
            //-----------------------------------------
            bool setLocalSuccess = await SetLocalVer(dir, ver);
            if (setLocalSuccess)
            {
                Debug.Log($"pyenv local {ver}�̐ݒ芮��: {dir}");
            }
            else
            {
                throw new Exception($"pyenv local�̐ݒ莸�s");
                //Debug.LogError("pyenv local�̐ݒ莸�s");
            }
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
        Debug.Log($"pyenv �C���X�g�[���󋵊m�F�J�n...");
        string version = await PowerShellAPI.Command("pyenv --version");
        Debug.Log($"pyenv �C���X�g�[���󋵊m�F�����@�o�[�W�����F{version}");
        Debug.Log($"pyenv ���C���X�g�[��");
        
        //string version = await CommandUtil.ExeToolCommand("pyenv --version");
        //try
        //{
        //    Debug.Log($"pyenv �C���X�g�[���󋵊m�F�J�n...");
        //    string version = await CommandUtil.ExeToolCommand("pyenv --version");
        //    Debug.Log($"pyenv �C���X�g�[���󋵊m�F�����@�o�[�W�����F{version}");
        //    Debug.Log($"pyenv ���C���X�g�[��");
        //}
        //catch (Exception e)
        //{
        //    throw;
        //}
    }


    ///==============================================<summary>
    /// Python 3.12.5���C���X�g�[��
    ///</summary>=============================================
    static async UniTask<bool> InstallPyEnv()
    {
        Debug.Log($"pyenv �C���X�g�[���J�n...");
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        string psCmd =
            "Invoke-WebRequest -UseBasicParsing -Uri \"https://raw.githubusercontent.com/pyenv-win/pyenv-win/master/pyenv-win/install-pyenv-win.ps1\" " +
            "-OutFile \"./install-pyenv-win.ps1\"; & \"./install-pyenv-win.ps1\"";

        // �C���X�g�[������Ɍ��݃v���Z�X�� PATH ���X�V
        RefreshPathForCurrentProcess();

        string result = await PowerShellAPI.Command(psCmd, workingDir: userProfile);
        Debug.Log($"pyenv �C���X�g�[������");
        return true;
    }

    static void RefreshPathForCurrentProcess()
    {
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
    }



    ///==============================================<summary>
    /// Python 3.12.5���C���X�g�[������Ă��邩�m�F
    ///</summary>=============================================
    static async UniTask<bool> IsInstalled_PyVer(string ver)
    {
        Debug.Log($"pyenv �C���X�g�[���� Python �o�[�W�����m�F�J�n...");
        string result = await PowerShellAPI.Command("pyenv versions");
        Debug.Log($"pyenv �C���X�g�[���� Python �o�[�W�����m�F����\n�ꗗ\n {result}");
        return result.Contains(ver);
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
    static async UniTask<bool> SetLocalVer(string targDir, string ver)
    {
        string result = await PowerShellAPI.Command($"pyenv local {ver}", targDir);

        // .python-version�t�@�C�����쐬���ꂽ���m�F
        string versionFile = $"{targDir}/.python-version";
        if (File.Exists(versionFile))
        {
            string content = await File.ReadAllTextAsync(versionFile);
            Debug.Log($".python-version�t�@�C�����쐬���ꂽ: {content.Trim()}");
            return true;
        }
        return false;
    }
}