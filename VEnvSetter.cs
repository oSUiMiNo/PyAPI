using Cysharp.Threading.Tasks;
using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Debug = UnityEngine.Debug;


public static class VEnvSetter
{
    //================================================
    // (1) pyenv exec �� .venv ���쐬  
    // (2) venv �� python �� requirements.txt ���C���X�g�[��
    // �O��F
    // - dir �� pyenv local �����ɐݒ�ς�
    // - dir �� requirements.txt ������
    //================================================
    public static async UniTask ExeFlow(string dir)
    {
        Debug.Log($"VEnv �Z�b�g�A�b�v�J�n...\n{dir}");
        //-------------------------------
        // .venv ��������΍쐬
        //-------------------------------
        string venvDir = $"{dir}/.venv";
        if (!Directory.Exists(venvDir))
        {
            Debug.Log($".venv �𐶐�...");
            // ���ϐ����C�ɂ��� pyenv �o�R�� python ���N�����ăR�}���h�����s
            await CommandUtil.ExeToolCommand("pyenv exec python -m venv .venv", dir);
        }
        else
            Debug.Log(".venv �͊��ɑ��݂���");
        //-------------------------------
        // requirements.txt ��������΃X�L�b�v
        //-------------------------------
        string libList = $"{dir}/requirements.txt";
        if (!File.Exists(libList))
        {
            Debug.LogWarning($"{libList} �������̂ŃC���X�g�[�����X�L�b�v");
            return;
        }
        //-------------------------------
        // venv �� python �̃t���p�X��g�ݗ���
        //-------------------------------
        string venvPy = $"{venvDir}/Scripts/python.exe";
        if (!File.Exists(venvPy))
            throw new FileNotFoundException($"���z���� python.exe ��������Ȃ�: {venvPy}");
        //-------------------------------
        // ���ɓ�����Ȃ�X�L�b�v
        //-------------------------------
        if (await IsAlreadySatisfiedAsync(venvPy, libList, dir))
        {
            Debug.Log("requirements.txt �Ɠ���������ɍ\�z�ς݁B�C���X�g�[�����X�L�b�v");
            return;
        }
        //-------------------------------
        // requirements.txt ������ΑS���C�u�����C���X�g�[��
        //-------------------------------
        Debug.Log("���X�g�����ƂɑS���C�u�������C���X�g�[��...");
        await CommandUtil.ExeToolCommand(
            $"{venvPy} -m pip install -r requirements.txt",
            dir);
        Debug.Log($"VEnv �Z�b�g�A�b�v����\n{dir}");
    }


    //================================================
    // requirements.txt �� venv �� pip freeze ���r
    //================================================
    static async UniTask<bool> IsAlreadySatisfiedAsync(string venvPy, string reqPath, string dir)
    {
        // A) requirements.txt �𐮌`
        var required = File.ReadLines(reqPath)
                           .Select(l => l.Split('#')[0].Trim())       // �R�����g����
                           .Where(l => !string.IsNullOrWhiteSpace(l)) // ��s����
                           .Select(NormalizePkg)
                           .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // B) venv �̌�����擾
        string freeze = await CommandUtil.ExeToolCommand($"{venvPy} -m pip freeze", dir);
        var installed = freeze.Split('\n')
                              .Select(l => l.Trim())
                              .Where(l => !string.IsNullOrWhiteSpace(l))
                              .Select(NormalizePkg)
                              .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // C) �����`�F�b�N
        bool ok = required.IsSubsetOf(installed);
        if (!ok)
        {
            var missing = required.Except(installed);
            Debug.Log($"���C���X�g�[���̃p�b�P�[�W: {string.Join(", ", missing)}");
        }
        return ok;
    }

    // �uPackage==Version�v�܂ō��킹�Ĕ�r�������̂Ő��K��        ��
    private static string NormalizePkg(string line) =>
        line.Replace(" ", "").Replace("\r", "").ToLowerInvariant();
}
