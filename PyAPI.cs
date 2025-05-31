using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using System.IO;
using System;
using System.Collections.Generic;
using UniRx;
using System.Text;
using MyUtil;


public class PyAPIHandler : SingletonCompo<PyAPIHandler>
{
    protected sealed override void Awake0() => PyAPI.InitLog();
    private void OnApplicationQuit() => PyAPI.Close();
    private void OnDestroy() => PyAPI.Close(); // �p�b�P�[�W�C���|�[�g��Ŏ��s����ĂȂ�
}


//****************************************************
// PyFnc �𐶐����đҋ@��������A�C�h��������
//****************************************************
public class PyAPI
{
    //==================================================
    // �p�u���b�N
    //==================================================
    public string PyInterpFile { get; }
    public string PyDir { get; }
    //==================================================
    // �X�^�e�B�b�N-���[�J��
    //==================================================
    static string LogPath => $"{Application.dataPath}/PyLog.txt";
    // Python ���O�\���p�C���X�^���X
    static SharedLog Log = new SharedLog(LogPath);
    // �V�F�A���O�ǂݎ��^�C�~���O�̃n���h��
    static IObservable<long> OnRead => logActive.UpdateWhileEqualTo(Log.isActive, 0.05f);
    static BoolReactiveProperty logActive = new BoolReactiveProperty(true);
    //==================================================
    // �R���X�g���N�^
    //==================================================
    /// <param name="pyDir">���b�v���� .py �t�@�C���̂����� Dir</param>
    /// <param name="pyInterpFile">Python �̃C���^�v���^</param>
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

    //==================================================
    // 
    //==================================================
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

    //==================================================
    // 
    //==================================================
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


//****************************************************
// Python �v���Z�X�̃��b�p
// 1�� .py �t�@�C���ɂ� (�ꍇ�ɂ��) �����̃v���Z�X���쐬��
// ������1�̊֐� (PyFnc �C���X�^���X) �Ƃ��ă��b�v
//****************************************************
public class PyFnc
{
    //==================================================
    // �X�^�e�B�b�N-���[�J��
    //==================================================
    // �A�C�h������ PyFnc �C���X�^���X�Ǘ�
    static List<PyFnc> IdolingFncs = new();
    // ������n���t�@�C����1Fnc�ɂ������g��
    static int InPathNum = 0;
    //==================================================
    // �p�u���b�N
    //==================================================
    // .py �t�@�C������ Fnc ���Ƃ���
    public string FncName { get; private set; }
    // �߂�l���������܂��t�@�C��
    public string OutPath { get; private set; }
    //==================================================
    // �v���C�x�[�g
    //==================================================
    // �q�v���Z�X�Ǘ�
    List<System.Diagnostics.Process> children = new();
    // �q�v���Z�X�̃C���f�b�N�X
    int currentChildIndex = 0;
    // �^�C���A�E�g����
    float Timeout = 0;
    // ���s�L�����Z���p�g�[�N��
    CancellationTokenSource cts = new();
    // �A�E�g�v�b�g�Ď��p
    SharedLog Output;
    // �A�E�g�v�b�g�ǂݎ��^�C�~���O�̃n���h��
    IObservable<long> OnRead => logActive.TimerWhileEqualTo(Output.isActive, 0.01f);
    BoolReactiveProperty logActive = new(true);

    //==================================================
    // �A�E�g�v�b�g�ɖ߂�l�������痬��
    //==================================================
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

    //==================================================
    // �A�E�g�v�b�g�Ƀv���Z�X�̃��[�h�����ʒm�������痬��
    //==================================================
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

    //==================================================
    // PyFnc �C���X�^���X�쐬
    //==================================================
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

    //==================================================
    // �S�v���Z�X��7���ȏオ���[�h��������܂ő҂�
    // Create ����await ����Ɖ��̂� onOut �����΂��Ȃ�
    //==================================================
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

    //==================================================
    // ���݂��Ă���S PyFnc �I��
    //==================================================
    public static void CloseAll(int waittMilliSecond)
    {
        foreach (var fnc in IdolingFncs)
        {
            fnc.Close(waittMilliSecond);
        }
        IdolingFncs.Clear();
    }

    //==================================================
    // �{ PyFunc �I��
    //==================================================
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

    //==================================================
    // �o�͊Ď��̐ݒ�
    //==================================================
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

    //==================================================
    // 1PyFnc�C���X�^���X�ŊǗ�����S�v���Z�X���N��
    //==================================================
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

        // AddTo �̒��g�� GO �� Compo �Ȃ̂Ń��C���X���b�h����Ȃ��Ƃ���
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
            // Close ���邽�߂ɑҋ@
            await children[currentChildIndex].ExeAsync(Timeout, () => Output.Close(), cts.Token);
        }
        catch (OperationCanceledException) { }
        if (currentChildIndex == children.Count - 1) currentChildIndex = 0;
        else currentChildIndex++;
        
        Close(100);
        GC.Collect();
    }
}