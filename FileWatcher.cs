using UnityEngine;
using System;
using System.IO;

public class FileWatcher : MonoBehaviour
{
    private FileSystemWatcher watcher;
    private string filePath = "path/to/your/file.txt"; // �Ď�����t�@�C���̃p�X


    void Start()
    {
        watcher = new FileSystemWatcher();
        watcher.Path = Path.GetDirectoryName(filePath); // �Ď�����f�B���N�g��
        watcher.Filter = Path.GetFileName(filePath); // �Ď�����t�@�C����
        watcher.NotifyFilter = NotifyFilters.LastWrite; // �ύX���Ď�
        watcher.Changed += OnFileChanged;
        watcher.EnableRaisingEvents = true;
    }

    private void OnFileChanged(object source, FileSystemEventArgs e)
    {
        if (e.ChangeType == WatcherChangeTypes.Changed)
        {
            Debug.Log("File changed: " + e.FullPath);
            // �t�@�C�����ύX���ꂽ���̏���
            // ��F�e�L�X�g�t�@�C�����ēǂݍ���
            string text = File.ReadAllText(filePath);
            Debug.Log(text);
        }
    }

    void OnDisable()
    {
        if (watcher != null)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }
    }
}