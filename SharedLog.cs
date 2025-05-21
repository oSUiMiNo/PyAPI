using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System;
using Newtonsoft.Json.Linq;
using Cysharp.Threading.Tasks;
using UniRx;


public class SharedLog
{
    string LogPath; // �Ď�����t�@�C���̃p�X
    DateTime lastWriteTime;
    public Subject<string> OnLog = new Subject<string>();
    public bool isActive = false;

    public SharedLog(string logPath)
    {
        LogPath = logPath;
        CreateLogFileAsync().Forget();
        isActive = true;
    }



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

        DateTime currentWriteTime = File.GetLastWriteTime(LogPath);

        if (currentWriteTime != lastWriteTime)
        {
            lastWriteTime = currentWriteTime;
            try
            {
                // ������������ێ�����ϐ�
                string unprocessedLogs = "";

                // Python �Ɠ���txt�t�@�C���𑀍삷��ۂ̋�����h�~
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
            catch (Exception e)
            {
                Debug.LogError($"���O�ǂݎ��G���[: {e.Message}");
            }
        }
        await UniTask.SwitchToMainThread();
    }


    // ���O�t�@�C���폜
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
