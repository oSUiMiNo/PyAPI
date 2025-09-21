using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;


public class CancelTest : MonoBehaviour
{
    CancellationTokenSource cts;


    void Start()
    {
        // CancellationTokenSource �쐬
        cts = new CancellationTokenSource();
        // �񓯊������J�n
        PerformAsyncTask(cts.Token);
    }


    async void PerformAsyncTask(CancellationToken ct)
    {
        try
        {
            // �񓯊�����
            await UniTask.Delay(5000, cancellationToken: ct); // 5�b�҂i�r���ŃL�����Z���\�j
            Debug.Log("Task Completed");
        }
        catch (OperationCanceledException)
        {
            // �L�����Z�����̏���
            Debug.Log("Task Canceled");
        }
    }
    

    void OnApplicationQuit()
    {
        // �A�v���I�����ɑS�Ă̂̔񓯊��������L�����Z��
        cts?.Cancel();
        cts?.Dispose();
    }
}
