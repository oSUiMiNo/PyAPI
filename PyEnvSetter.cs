using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;


public class PyEnvSetter
{
    //================================================
    // �w��t�H���_�� pyenv local �o�[�W������ݒ肷��t���[
    //================================================
    public static async UniTask ExeFlow(string dir, string ver)
    {
        try
        {
            Debug.Log($"Pyenv �Z�b�g�A�b�v�J�n...\n{dir}");
            //-------------------------------
            // �w��t�H���_�����݂��Ȃ���΍쐬
            //-------------------------------
            if (!Directory.Exists(dir))
            {
                Debug.Log($"�t�H���_�����݂��Ȃ����ߍ쐬���܂�: {dir}");
                Directory.CreateDirectory(dir);
            }
            //-------------------------------
            // pyenv �Ɏw��o�[�W�������C���X�g�[������Ă��Ȃ���΃C���X�g�[��
            //-------------------------------
            bool isInstalled = await InstalledPyVer(ver);
            if (!isInstalled)
            {
                Debug.Log($"Python {ver} ���C���X�g�[������Ă��Ȃ�");
                bool installSuccess = await InstallPyVer(ver);
                if (!installSuccess)
                {
                    Debug.LogError($"Python {ver} �C���X�g�[�����s");
                    return;
                }
            }
            else
            {
                Debug.Log($"Python {ver} �͊��ɃC���X�g�[������Ă���");
            }
            //-------------------------------
            // pyenv local���w��o�[�W�����ɐݒ�
            //-------------------------------
            bool setLocalSuccess = await SetLocalVer(dir, ver);
            if (setLocalSuccess)
            {
                Debug.Log($"pyenv local {ver}�̐ݒ肪�������܂���: {dir}");
            }
            else
            {
                Debug.LogError("pyenv local�̐ݒ�Ɏ��s���܂���");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"�G���[���������܂���: {e.Message}");
        }
        Debug.Log($"PyEnv �Z�b�g�A�b�v����\n{dir}");
    }

    //================================================
    // Python 3.12.5���C���X�g�[������Ă��邩�m�F
    //================================================
    static async UniTask<bool> InstalledPyVer(string ver)
    {
        string result = await CommandUtil.ExeToolCommand("pyenv versions");
        Debug.Log($"pyenv �o�[�W�����ꗗ\n {result}");
        return result.Contains(ver);
    }

    //================================================
    // Python 3.12.5���C���X�g�[��
    //================================================
    static async UniTask<bool> InstallPyVer(string ver)
    {
        Debug.Log($"Python {ver} �C���X�g�[�����J�n...");
        string result = await CommandUtil.ExeToolCommand($"pyenv install {ver}");
        Debug.Log($"Python {ver} �C���X�g�[��������");
        return true;
    }

    //================================================
    // �w��t�H���_�� pyenv local��ݒ�
    //================================================
    static async UniTask<bool> SetLocalVer(string targDir, string ver)
    {
        string result = await CommandUtil.ExeToolCommand($"pyenv local {ver}", targDir);

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