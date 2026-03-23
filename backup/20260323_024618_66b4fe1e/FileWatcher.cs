using UnityEngine;
using System;
using System.IO;

public class FileWatcher : MonoBehaviour
{
    private FileSystemWatcher watcher;
    private string filePath = "path/to/your/file.txt"; // 監視するファイルのパス


    void Start()
    {
        watcher = new FileSystemWatcher();
        watcher.Path = Path.GetDirectoryName(filePath); // 監視するディレクトリ
        watcher.Filter = Path.GetFileName(filePath); // 監視するファイル名
        watcher.NotifyFilter = NotifyFilters.LastWrite; // 変更を監視
        watcher.Changed += OnFileChanged;
        watcher.EnableRaisingEvents = true;
    }

    private void OnFileChanged(object source, FileSystemEventArgs e)
    {
        if (e.ChangeType == WatcherChangeTypes.Changed)
        {
            Debug.Log("File changed: " + e.FullPath);
            // ファイルが変更された時の処理
            // 例：テキストファイルを再読み込み
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