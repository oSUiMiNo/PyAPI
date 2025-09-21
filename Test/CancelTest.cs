using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;


public class CancelTest : MonoBehaviour
{
    CancellationTokenSource cts;


    void Start()
    {
        // CancellationTokenSource 作成
        cts = new CancellationTokenSource();
        // 非同期処理開始
        PerformAsyncTask(cts.Token);
    }


    async void PerformAsyncTask(CancellationToken ct)
    {
        try
        {
            // 非同期処理
            await UniTask.Delay(5000, cancellationToken: ct); // 5秒待つ（途中でキャンセル可能）
            Debug.Log("Task Completed");
        }
        catch (OperationCanceledException)
        {
            // キャンセル時の処理
            Debug.Log("Task Canceled");
        }
    }
    

    void OnApplicationQuit()
    {
        // アプリ終了時に全てのの非同期処理をキャンセル
        cts?.Cancel();
        cts?.Dispose();
    }
}
