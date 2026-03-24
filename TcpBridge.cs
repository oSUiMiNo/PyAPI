using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UniRx;
using UnityEngine;


///*******************************************************<summary>
/// TCP ソケットによるプロセス間通信
/// 1インスタンスにつき1接続を管理する（SharedLog の置換）
///</summary>******************************************************
public class TcpBridge
{
    TcpClient client;
    NetworkStream stream;
    SemaphoreSlim sendLock = new(1, 1);

    public Subject<string> OnMessage = new Subject<string>();
    public bool isActive = false;


    string host;
    int port;

    public TcpBridge(string host, int port)
    {
        this.host = host;
        this.port = port;
    }


    ///==============================================<summary>
    /// TCP 接続確立（リトライ付き）
    ///</summary>=============================================
    public async UniTask Connect(int maxRetry = 3, int retryIntervalMs = 100)
    {
        await UniTask.SwitchToThreadPool();
        Exception lastEx = null;
        for (int i = 0; i < maxRetry; i++)
        {
            try
            {
                client = new TcpClient();
                client.Connect(host, port);
                stream = client.GetStream();
                isActive = true;
                await UniTask.SwitchToMainThread();
                return;
            }
            catch (Exception e)
            {
                lastEx = e;
                client?.Close();
                if (i < maxRetry - 1) await UniTask.Delay(retryIntervalMs);
            }
        }
        await UniTask.SwitchToMainThread();
        throw new Exception($"TCP接続失敗 {host}:{port} ({maxRetry}回リトライ後): {lastEx?.Message}");
    }


    ///==============================================<summary>
    /// JObject を長さプレフィクス付きで送信（スレッドセーフ）
    ///</summary>=============================================
    public async UniTask Send(JObject data)
    {
        if (!isActive || stream == null) return;
        byte[] body = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(data));
        byte[] header = BitConverter.GetBytes((uint)body.Length);
        //--------------------------------------
        // big-endian に変換（BitConverter は little-endian）
        //--------------------------------------
        if (BitConverter.IsLittleEndian) Array.Reverse(header);

        await sendLock.WaitAsync();
        try
        {
            await stream.WriteAsync(header, 0, 4);
            await stream.WriteAsync(body, 0, body.Length);
            await stream.FlushAsync();
        }
        catch (Exception e)
        {
            Debug.LogError($"TCP送信エラー: {e.Message}");
        }
        finally
        {
            sendLock.Release();
        }
    }


    ///==============================================<summary>
    /// 非同期受信ループ開始（バックグラウンドスレッドで実行）
    ///</summary>=============================================
    public void StartReceiveLoop()
    {
        UniTask.RunOnThreadPool(async () =>
        {
            while (isActive)
            {
                try
                {
                    string msg = await ReadMessage();
                    if (msg == null) break;
                    OnMessage.OnNext(msg);
                }
                catch (Exception e)
                {
                    if (isActive) Debug.LogError($"TCP受信エラー: {e.Message}");
                    break;
                }
            }
            if (isActive)
            {
                isActive = false;
                OnMessage.OnCompleted();
            }
        }).Forget();
    }


    ///==============================================<summary>
    /// 1メッセージ読み取り（長さプレフィクス方式）
    ///</summary>=============================================
    async UniTask<string> ReadMessage()
    {
        //--------------------------------------
        // 4バイトヘッダ読み取り
        //--------------------------------------
        byte[] header = await ReadExact(4);
        if (header == null) return null;

        //--------------------------------------
        // big-endian → uint32 に変換
        //--------------------------------------
        if (BitConverter.IsLittleEndian) Array.Reverse(header);
        uint length = BitConverter.ToUInt32(header, 0);

        //--------------------------------------
        // ペイロード読み取り
        //--------------------------------------
        byte[] body = await ReadExact((int)length);
        if (body == null) return null;

        return Encoding.UTF8.GetString(body);
    }


    ///==============================================<summary>
    /// 指定バイト数を確実に読み取る
    ///</summary>=============================================
    async UniTask<byte[]> ReadExact(int count)
    {
        byte[] buf = new byte[count];
        int offset = 0;
        while (offset < count)
        {
            int read = await stream.ReadAsync(buf, offset, count - offset);
            if (read == 0) return null;
            offset += read;
        }
        return buf;
    }


    ///==============================================<summary>
    /// 接続切断 + リソース解放
    ///</summary>=============================================
    public void Close()
    {
        isActive = false;
        try { OnMessage.OnCompleted(); } catch { }
        try { stream?.Close(); } catch { }
        try { client?.Close(); } catch { }
        sendLock.Dispose();
    }
}
