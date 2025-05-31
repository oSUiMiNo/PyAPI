using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using System.IO;
using System;
using System.Collections.Generic;
using UniRx;
using System.Security.Cryptography;
using System.Text;
using MyUtil;

public class PyAPIHandler : SingletonCompo<PyAPIHandler>
{
    protected sealed override void Awake0() => PyAPI.InitLog();
    private void OnApplicationQuit() => PyAPI.Close();
    private void OnDestroy() => PyAPI.Close(); // �p�b�P�[�W�C���|�[�g��Ŏ��s����ĂȂ�
}


public class PyAPI
{
    string PyInterpFile;
    string PyDir;


    public PyAPI(string pyDir, string pyInterpFile = "")
    {
        PyDir = pyDir;
        if (string.IsNullOrEmpty(pyInterpFile)) PyInterpFile = $"{pyDir}/.venv/Scripts/python.exe";
        else PyInterpFile = pyInterpFile;
    }

    //==================================================
    // �������s�������֐����쐬���ăA�C�h�����O������
    //==================================================
    public async UniTask<PyFnc> Idle(string pyFileName, int processCount = 1, int threadCount = 1)
    {
        // Python�t�@�C���p�X
        string pyFile = @$"{PyDir}\{pyFileName}";
        if (!File.Exists(PyInterpFile)) Debug.LogError($"���̎��s�t�@�C���͖���{PyInterpFile}");
        if (!File.Exists(pyFile)) Debug.LogError($"����Py�t�@�C���͖���{pyFile}");
        PyFnc pyFnc;
        if (processCount <= 1) pyFnc = await PyFnc.Create(PyInterpFile, pyFile);
        else pyFnc = await PyFnc.Create(PyInterpFile, pyFile, processCount: processCount, threadCount: threadCount);
        pyFnc.Start();
        GC.Collect();
        return pyFnc;
    }

    //==================================================
    // �P�V���b�g���s����֐����쐬���đҋ@������
    //==================================================
    public async UniTask<PyFnc> Wait(string pyFileName, float timeout = 0)
    {
        return await Wait(pyFileName, new JObject(), timeout);
    }
    public async UniTask<PyFnc> Wait(string pyFileName, JObject inJO, float timeout = 0, bool largeInput = false)
    {
        // Python�t�@�C���p�X
        string pyFile = @$"{PyDir}\{pyFileName}";
        if (!File.Exists(PyInterpFile)) Debug.LogError($"���̎��s�t�@�C���͖���{PyInterpFile}");
        if (!File.Exists(pyFile)) Debug.LogError($"����Py�t�@�C���͖���{pyFile}");
        //// ["] �� [\""] �ɃG�X�P�[�v����Json
        //string sendData = JsonConvert.SerializeObject(inJO).Replace("\"", "\\\"\"");
        PyFnc pyFnc = await PyFnc.Create(PyInterpFile, pyFile, inJO, largeInput: largeInput);
        GC.Collect();
        return pyFnc;
    }


    static string LogPath => $"{Application.dataPath}/PyLog.txt"; // �Ď�����t�@�C��
    static SharedLog Log = new SharedLog(LogPath);
    static IObservable<long> OnRead => logActive.UpdateWhileEqualTo(Log.isActive, 0.05f);
    static BoolReactiveProperty logActive = new BoolReactiveProperty(true);
    public static void InitLog()
    {
        OnRead.Subscribe(_ =>
        {
            if (!File.Exists(LogPath)) return; // �Ȃ񂩃I�y���[�^�����蔲����̂Ńu���b�N���Ƃ�
            //Debug.Log($"���O {File.Exists(LogPath)}");
            Log.ReadLogFile();
        }).AddTo(PyAPIHandler.Compo);

        Log.OnLog.Subscribe(msg =>
        {
            Debug.Log(msg.HexColor("#90E3C4"));
        }).AddTo(PyAPIHandler.Compo);
    }
    public static async void Close()
    {
        // �I�����͂͑҂�����0����Ȃ��ƃp�b�P�[�W���p��Ŏ��s����Ȃ�
        PyFnc.CloseAll(0);
        logActive.Dispose();
        // �I����ɑ҂������̂ł�����Delay.Second�ł͂���
        await UniTask.Delay(1);
        Log.Close();
        Debug.Log("PyAPI �N���[�Y����");
    }
}



public class PyFnc
{
    static List<PyFnc> IdolingFncs = new List<PyFnc>();
    static int InPathNum = 0;

    public string FncName { get; private set; }
    public string OutPath { get; private set; } // �Ď�����t�@�C��
    float Timeout = 0;
    SharedLog Output;

    int currentChildIndex = 0;
    List<System.Diagnostics.Process> children = new List<System.Diagnostics.Process>();
    CancellationTokenSource cts = new CancellationTokenSource();

    IObservable<long> OnRead => logActive.TimerWhileEqualTo(Output.isActive, 0.01f);
    BoolReactiveProperty logActive = new BoolReactiveProperty(true);

    public IObservable<JObject> OnOut => Output.OnLog
    .Select(msg =>
    {
        try
        {
            return JObject.Parse(msg);
        }
        catch (Exception ex)
        {
            // �G���[���� (�K�v�ɉ�����)
            Debug.LogError($"JSON�p�[�X�G���[: {ex.Message}");
            return null;
        }
    })
    .Where(JO => JO != null)
    .Where(JO => JO["Loaded"] == null);

    public IObservable<JObject> OnLoad => Output.OnLog
    .Select(msg =>
    {
        try
        {
            return JObject.Parse(msg);
        }
        catch (Exception ex)
        {
            // �G���[���� (�K�v�ɉ�����)
            Debug.LogError($"JSON�p�[�X�G���[: {ex.Message}");
            return null;
        }
    })
    .Where(JO => JO != null)
    .Where(JO => JO["Loaded"] != null);


    public static async UniTask<PyFnc> Create(string pyInterpFile, string pyFile, JObject inJO = null, int processCount = 1, int threadCount = 1, float timeout = 0, bool largeInput = false)
    {
        await UniTask.SwitchToThreadPool();
        var newFnc = new PyFnc();
        IdolingFncs.Add(newFnc);

        newFnc.FncName = Path.GetFileName(pyFile);
        newFnc.Timeout = timeout;

        Debug.Log($"���[�W�C���v�b�g {largeInput}");
        string inPath = "";
        if (largeInput == true)
        {
            inPath = $"{Path.GetDirectoryName(pyFile)}/LargeInput{InPathNum}.txt";
            InPathNum++;
            if (InPathNum > 50000) InPathNum = 0;
            // �t�@�C�������݂���ꍇ�͏㏑��
            StreamWriter writer = new StreamWriter(inPath, false);
            writer.WriteLine(JsonConvert.SerializeObject(inJO));//.Replace("\"", "\\\"\"")); // �������ރe�L�X�g
            writer.Close();
            inJO = null;
            Debug.Log($"�������݊���");
        }
        if (inJO == null) inJO = new JObject();
        inJO["ThreadCount"] = threadCount;
        inJO["LargeInput"] = largeInput;
        inJO["InPath"] = inPath;


        // ["] �� [\""] �ɃG�X�P�[�v����Json
        string sendData = JsonConvert.SerializeObject(inJO).Replace("\"", "\\\"\"");

        string log = $"PyFnc�N��:{newFnc.FncName} - �v���Z�X: ";
        if (processCount <= 0) processCount = 1;
        for (int i = 0; i < processCount; i++)
        {
            newFnc.children.Add(new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo(pyInterpFile)
                {
                    Arguments = $"{pyFile} {sendData}",
                    UseShellExecute = false, // �V�F�����g�p���Ȃ�
                    RedirectStandardOutput = true, // �W���o�͂����_�C���N�g
                    RedirectStandardInput = true, // �W�����͂����_�C���N�g
                    RedirectStandardError = true, // �W���G���[�����_�C���N�g
                    CreateNoWindow = true, // PowerShell�E�B���h�E��\�����Ȃ�
                }
            });
            log += $", {i.ToString()}";
        }
        log += $"\n�e�X���b�h��: {threadCount}";
        Debug.Log(log);
        await UniTask.Delay(1);
        newFnc.InitLog(pyFile);
        // Create ����await ����Ɖ��̂� onOut �����΂��Ȃ�
        //await newFnc.WaitLoad(count);
        await UniTask.SwitchToMainThread();
        return newFnc;
    }


    // �S�v���Z�X��7���ȏオ���[�h��������܂ő҂�
    // Create ����await ����Ɖ��̂� onOut �����΂��Ȃ�
    public async UniTask WaitLoad(int completionRate)
    {
        if (completionRate < 1 || 10 < completionRate)
        {
            Debug.LogError("completionRate �� 1-10 �̊Ԃ�");
            return;
        }
        // AddTo �̒��g��GO��Compo�Ȃ̂Ń��C���X���b�h����Ȃ��Ƃ���
        bool ThreadIsMain = false;
        if (Thread.CurrentThread.ManagedThreadId == 1) ThreadIsMain = true;
        if (!ThreadIsMain) await UniTask.SwitchToMainThread();
        int loadedCount = 0;
        IDisposable onOut = OnLoad
        .Subscribe(JO =>
        {
            loadedCount++;
        }).AddTo(PyAPIHandler.Compo);
        if (!ThreadIsMain) await UniTask.SwitchToThreadPool();
        await UniTask.WaitUntil(() => loadedCount >= (int)(children.Count * 0.7));
        Debug.Log($"{FncName}: {completionRate}0% �̃v���Z�X�����[�h����".Magenta());
        onOut.Dispose();
    }


    public static void CloseAll(int waittMilliSecond)
    {
        foreach (var fnc in IdolingFncs)
        {
            fnc.Close(waittMilliSecond);
        }
        IdolingFncs.Clear();
    }

    public async void Close(int waittMilliSecond)
    {
        await UniTask.SwitchToThreadPool();
        // ���s�㑦�N���[�Y���ꂽ�ꍇ�A�E�g�v�b�g������Ȃ������肷��̂ő҂�
        // �������A�v���I�����͑҂ƃp�b�P�[�W���p��ł͌Ă΂�Ȃ��̂ōŌ�͂͑҂��Ȃ�
        await UniTask.Delay(waittMilliSecond);
        cts.Cancel();
        Output.Close();
        logActive.Dispose();
        string log = $"PyFnc�N���[�Y:{FncName} - �v���Z�X ";
        for (int i = 0; i < children.Count; i++)
        {
            children[i].PerfectKill();
            log += $", {i.ToString()}";
        }
        Debug.Log(log);
        IdolingFncs.Remove(this);
        GC.Collect();
        await UniTask.SwitchToMainThread();
    }

    void InitLog(string pyFile)
    {
        // �A�E�g�v�b�g�p�t�@�C���쐬;
        OutPath = $"{pyFile.Replace("\\", "/").Replace(".py", ".txt")}";
        Output = new SharedLog(OutPath);
        OnRead.Subscribe(_ =>
        {
            if (!File.Exists(OutPath)) return; // �Ȃ񂩃I�y���[�^�����蔲����̂Ńu���b�N���Ƃ�
            //Debug.Log($"���O{File.Exists(OutPath)} {Path.GetFileName(OutPath)}");
            Output.ReadLogFile();
        }).AddTo(PyAPIHandler.Compo);
    }

    public async void Start()
    {
        foreach (var child in children)
        {
            await UniTask.SwitchToThreadPool();
            child.Start();
            await UniTask.SwitchToMainThread();
        }
    }

    //==================================================
    // Idle ���̊֐������s
    //==================================================
    public async UniTask<JObject> Exe(JObject inJO)
    {
        JObject outJO = null;

        // AddTo �̒��g��GO��Compo�Ȃ̂Ń��C���X���b�h����Ȃ��Ƃ���
        bool ThreadIsMain = false;
        if (Thread.CurrentThread.ManagedThreadId == 1) ThreadIsMain = true;
        if (!ThreadIsMain) await UniTask.SwitchToMainThread();
        IDisposable onOut = OnOut.Subscribe(JO =>
        {
            outJO = JO;
        }).AddTo(PyAPIHandler.Compo);
        if (!ThreadIsMain) await UniTask.SwitchToThreadPool();

        ExeBG(inJO);
        await UniTask.WaitUntil(() => outJO != null);
        onOut.Dispose();

        return outJO;
    }
    public void ExeBG(JObject inJO)
    {
        children[currentChildIndex].Exe(inJO);
        if (currentChildIndex == children.Count - 1) currentChildIndex = 0;
        else currentChildIndex++;
        //GC.Collect();
    }

    //==================================================
    // Wait ���̊֐������s
    //==================================================
    public async UniTask<JObject> Exe()
    {
        JObject outJO = null;

        // AddTo �̒��g��GO��Compo�Ȃ̂Ń��C���X���b�h����Ȃ��Ƃ���
        bool ThreadIsMain = false;
        if (Thread.CurrentThread.ManagedThreadId == 1) ThreadIsMain = true;
        if (!ThreadIsMain) await UniTask.SwitchToMainThread();
        IDisposable onOut = OnOut.Subscribe(JO =>
        {
            outJO = JO;
        }).AddTo(PyAPIHandler.Compo);
        if (!ThreadIsMain) await UniTask.SwitchToThreadPool();

        ExeBG();
        await UniTask.WaitUntil(() => outJO != null);
        onOut.Dispose();

        return outJO;
    }
    public async void ExeBG()
    {
        try
        {
            // Close ���邽�߂ɑҋ@����
            await children[currentChildIndex].RunAsync(Timeout, () => Output.Close(), cts.Token);
        }
        catch (OperationCanceledException) { }
        if (currentChildIndex == children.Count - 1) currentChildIndex = 0;
        else currentChildIndex++;
        Close(100);
        GC.Collect();
    }
}






public static class ProcessExtentions
{
    public static async void Exe(this System.Diagnostics.Process process, JObject inputJObj)
    {
        if (process.StartInfo.RedirectStandardInput == false)
        {
            Debug.LogError("StartInfo.RedirectStandardInput �� True �ɂ���");
            return;
        }
        try
        {
            await UniTask.SwitchToThreadPool();
            string sendData = JsonConvert.SerializeObject(inputJObj);
            StreamWriter inputWriter = process.StandardInput;
            inputWriter.WriteLine(sendData);
            inputWriter.Flush();
            await UniTask.SwitchToMainThread();
        }
        catch { }
    }


    public static async void Command(this System.Diagnostics.Process process, string command)
    {
        if (process.StartInfo.RedirectStandardInput == false)
        {
            Debug.LogError("StartInfo.RedirectStandardInput �� True �ɂ���");
            return;
        }
        try
        {
            await UniTask.SwitchToThreadPool();
            StreamWriter inputWriter = process.StandardInput;
            inputWriter.WriteLine(command);
            inputWriter.Flush();
            await UniTask.SwitchToMainThread();
        }
        catch { }
    }


    public static async UniTask RunAsync(this System.Diagnostics.Process process, float timeout = 0, Action fncOnDispose = null, CancellationToken externalCT = default)
    {
        await UniTask.SwitchToThreadPool();
        var timeoutCTS = new CancellationTokenSource();
        var exited = new UniTaskCompletionSource();

        if (timeout != 0)
            UniTask.RunOnThreadPool(() => process.Timeout(timeout, timeoutCTS.Token)).Forget();

        process.EnableRaisingEvents = true;
        process.Exited += (sender, args) =>
        {
            string error = process.StandardError.ReadToEnd();
            if (!string.IsNullOrEmpty(error)) Debug.LogError($"PowerShell Error: {error}");
            exited.TrySetResult();
            process.PerfectKill();
        };

        process.Disposed += (sender, args) =>
        {
            timeoutCTS.Cancel();
            fncOnDispose?.Invoke();
        };

        process.Start();

        try
        {
            // �O���L�����Z���𔽉f
            await exited.Task.AttachExternalCancellation(externalCT);
            Debug.Log($"�v���Z�X����");
        }
        catch (OperationCanceledException)
        {
            // �O������L�����Z�����ꂽ�ꍇ�̏���
            Debug.Log("�O������L�����Z�����ꂽ");
            process.PerfectKill();
            // �K�v�ɉ����ė�O���ăX���[
            throw;
        }
        await UniTask.SwitchToMainThread();
    }


    public static async void Timeout(this System.Diagnostics.Process process, float timeout, CancellationToken CT)
    {
        try
        {
            await UniTask.WaitForSeconds(timeout, false, PlayerLoopTiming.Update, CT);
            Debug.LogAssertion("�^�C���A�E�g");
            process.PerfectKill();
        }
        catch (OperationCanceledException)
        {
            Debug.Log("�^�C���A�E�g���L�����Z�����ꂽ");
        }
    }


    public static void PerfectKill(this System.Diagnostics.Process process)
    {
        try
        {
            // ����Kill����Ă����ꍇ�͖��������
            process.Kill();
        }
        catch { /*Debug.Log("���� Kill ����Ă���");*/ }
        process.Dispose();
    }




    public static UniTask<string> RunAsync(this System.Diagnostics.Process process, float timeout = 0, Action fncOnDispose = null)
    {
        var cts = new CancellationTokenSource();
        var exited = new UniTaskCompletionSource<string>();
        string output = "";

        if (timeout != 0)
            UniTask.RunOnThreadPool(() => process.Timeout(timeout, cts.Token)).Forget();

        // Exited �C�x���g��L���ɂ���
        process.EnableRaisingEvents = true;
        process.Exited += (sender, args) =>
        {
            string error = process.StandardError.ReadToEnd(); // �G���[�ǎ��
            if (!string.IsNullOrEmpty(error)) Debug.LogError($"PowerShell Error: {error}");

            output = process.StandardOutput.ReadToEnd();
            process.Dispose();
        };

        process.Disposed += (sender, args) =>
        {
            exited.TrySetResult(output);
            cts.Cancel();
            fncOnDispose?.Invoke();
        };

        process.Start();

        return exited.Task;
    }
}