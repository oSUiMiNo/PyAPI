using System.Collections.Generic;
using UnityEngine;
using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UniRx;
using Cysharp.Threading.Tasks;


//****************************************************
// ������̃R�[�h�̃��O�� Unity �̃R���\�[���ɕ\������
// 1�C���X�^���X�ɂ�1���O�V�F�A�t�@�C��������
//****************************************************
public class SharedLog
{
    // �Ď�����t�@�C���̃p�X
    string LogPath;
    // �O�񃍃O�̃^�C���X�^���v
    DateTime lastWriteTime;
    // ���O�����������甭��
    public Subject<string> OnLog = new Subject<string>();
    // �{�����̃A�N�e�B�u���
    public bool isActive = false;

    public SharedLog(string logPath)
    {
        LogPath = logPath;
        // ���O�V�F�A�e�L�X�g�t�@�C���쐬
        CreateLogFileAsync().Forget();
        // �A�N�e�B�u�Ƃ������Ƃɂ���
        isActive = true;
    }

    //==================================================
    // ���O�V�F�A�p�̃e�L�X�g�t�@�C���쐬
    //==================================================
    async UniTask CreateLogFileAsync()
    {
        await UniTask.SwitchToThreadPool();

        // ���������Ƀ��O�t�@�C�����폜
        if (File.Exists(LogPath))
            try
            {
                File.Delete(LogPath);
                //Debug.Log($"���O�t�@�C���폜�i���������j: {LogPath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"���������̃��O�t�@�C���폜�Ɏ��s: {e.Message}");
            }

        try
        {
            string directory = Path.GetDirectoryName(LogPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            File.Create(LogPath).Close();
            //Debug.Log($"���O�t�@�C���쐬: {LogPath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"���O�t�@�C���쐬���s: {e.Message}");
            return;
        }

        try
        {
            lastWriteTime = File.GetLastWriteTime(LogPath);
        }
        catch (Exception e)
        {
            Debug.LogError($"�ŏI�X�V�����擾���s: {e.Message}");
            return;
        }
        await UniTask.SwitchToMainThread();
    }

    //==================================================
    // -> ���O�V�F�A�p�̃e�L�X�g�t�@�C���̍X�V�����m
    // -> OnLog �Ƀ��O�𗬂�
    // �� �O������C�ӂ̃^�C�~���O�ŌĂ�
    //==================================================
    public async void ReadLogFile()
    {
        await UniTask.SwitchToThreadPool();
        //Debug.Log("���O�ǂݎ��");
        //if (!File.Exists(LogPath))
        //    try
        //    {
        //        File.Create(LogPath).Close();
        //        Debug.LogWarning($"���O�t�@�C�����폜���ꂽ���ߍč쐬: {LogPath}");
        //        lastWriteTime = File.GetLastWriteTime(LogPath);
        //    }
        //    catch (Exception e)
        //    {
        //        Debug.LogError($"���O�t�@�C���č쐬���s: {e.Message}");
        //        return;
        //    }
        
        // �^�C���X�^���v�Ƃ��đO��t�@�C���Ƀ��O���ǋL���ꂽ�������擾
        DateTime currentWriteTime = File.GetLastWriteTime(LogPath);

        // �O��^�C���X�^���v����X�V����Ă�����
        if (currentWriteTime != lastWriteTime)
        {
            // �^�C���X�^���v�X�V
            lastWriteTime = currentWriteTime;
            try
            {
                // ������������ێ�����ϐ�
                string unprocessedLogs = "";

                // Python �Ɠ��� txt �t�@�C���𑀍삷��ۂ̋�����h�~
                using (FileStream fs = new FileStream(LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (StreamReader sr = new StreamReader(fs))
                {
                    // ��؂育�Ƃɕ������ď���
                    string[] logs = sr.ReadToEnd().Split(new[] { "___" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string log in logs)
                    {
                        string trimmedLog = log.Trim();
                        if (!string.IsNullOrEmpty(trimmedLog))
                        {
                            OnLog.OnNext(trimmedLog);
                        }
                    }
                }

                // �������������t�@�C���Ɉ��S�ɏ����߂�
                if (!string.IsNullOrEmpty(unprocessedLogs))
                {
                    using (FileStream fs = new FileStream(LogPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite))
                    {
                        fs.SetLength(0); // �t�@�C�����e���N���A
                        using (StreamWriter sw = new StreamWriter(fs))
                        {
                            sw.Write(unprocessedLogs.Trim());
                        }
                    }
                }
                else
                {
                    // �S�ď����ς݂Ȃ�t�@�C�������S�ɃN���A
                    using (FileStream fs = new FileStream(LogPath, FileMode.Truncate, FileAccess.Write, FileShare.ReadWrite))
                    {
                        // FileMode.Truncate ���g�p����ƁA�t�@�C�����J�����u�Ԃɂ��̓��e�������I�ɍ폜����A�t�@�C���T�C�Y��0�Ƀ��Z�b�g�����B���̉ӏ��ɋ�̓I�ȏ����������K�v�͖���
                    }
                }
            }
            catch (Exception e) { Debug.LogError($"���O�ǂݎ��G���[: {e.Message}"); }
        }
        await UniTask.SwitchToMainThread();
    }

    //==================================================
    // -> Observable �I��
    // -> ���O�t�@�C���폜
    // -> �K�x�R��
    //==================================================
    public async void Close()
    {
        await UniTask.SwitchToThreadPool();
        isActive = false;
        OnLog.OnCompleted();
        //OnLog.Dispose();
        await UniTask.Delay(1);
        if (File.Exists(LogPath))
        try
        {
            File.Delete(LogPath);
            Debug.Log($"���O�t�@�C���폜�i�I�����j{LogPath} {File.Exists(LogPath)}");
        }
        catch (Exception e)
        {
            Debug.LogError($"�I�����̃��O�t�@�C���폜�Ɏ��s: {e.Message}");
        }
        GC.Collect();
        await UniTask.SwitchToMainThread();
    }
}
