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


public static class ProcessUtil
{
    //==================================================
    // �����I�Ȋ����� JObject ��n���v���Z�X�����s
    //==================================================
    public static async void Exe(this System.Diagnostics.Process process, JObject inJO)
    {
        if (process.StartInfo.RedirectStandardInput == false)
        {
            Debug.LogError("StartInfo.RedirectStandardInput �� True �ɂ���");
            return;
        }
        try
        {
            await UniTask.SwitchToThreadPool();
            string sendData = JsonConvert.SerializeObject(inJO);
            StreamWriter inputWriter = process.StandardInput;
            inputWriter.WriteLine(sendData);
            inputWriter.Flush();
            await UniTask.SwitchToMainThread();
        }
        catch { }
    }

   
    //==================================================
    // �v���Z�X�̔񓯊����s (�O������L�����Z����)
    //==================================================
    public static async UniTask ExeAsync(this System.Diagnostics.Process process, float timeout = 0, Action fncOnDispose = null, CancellationToken externalCT = default)
    {
        await UniTask.SwitchToThreadPool();
        var timeoutCTS = new CancellationTokenSource();
        var exited = new UniTaskCompletionSource();

        // �^�C���A�E�g���Ԃ��ݒ肳��Ă���ꍇ�͂��̎��ԂŃ^�C���A�E�g
        if (timeout != 0)
            UniTask.RunOnThreadPool(() => process.Timeout(timeout, timeoutCTS.Token)).Forget();

        //--------------------------------------
        // �v���Z�X�I��������
        //--------------------------------------
        // Exited (�v���Z�X�I��) �C�x���g��L����
        process.EnableRaisingEvents = true;
        process.Exited += (sender, args) =>
        {
            // �G���[�ǎ�� -> ���O�o��
            string error = process.StandardError.ReadToEnd();
            if (!string.IsNullOrEmpty(error)) Debug.LogError($"PowerShell Error: {error}");
            // ���s���ʂ̏o�͂��Z�b�g
            exited.TrySetResult();
            // �v���Z�X����
            process.PerfectKill();
        };

        //--------------------------------------
        // �v���Z�X����������
        //--------------------------------------
        process.Disposed += (sender, args) =>
        {
            timeoutCTS.Cancel();
            fncOnDispose?.Invoke();
        };

        process.Start();

        //--------------------------------------
        // �O������̃L�����Z����ݒ�
        //--------------------------------------
        try
        {
            // �O���L�����Z���𔽉f
            await exited.Task.AttachExternalCancellation(externalCT);
            Debug.Log($"�v���Z�X����");
        }
        //--------------------------------------
        // �O������L�����Z�����ꂽ�ꍇ�ɑ��鏈��
        //--------------------------------------
        catch (OperationCanceledException)
        {
            Debug.Log("�O������L�����Z�����ꂽ");
            // 
            process.PerfectKill();
            // �K�v�ɉ����ė�O���ăX���[
            throw;
        }
        await UniTask.SwitchToMainThread();
    }

    //==================================================
    // �^�C���A�E�g
    //==================================================
    public static async void Timeout(this System.Diagnostics.Process process, float timeout, CancellationToken CT)
    {
        try
        {
            // �w��̎��Ԍo�߂�����v���Z�X�𖕏�
            await UniTask.WaitForSeconds(timeout, false, PlayerLoopTiming.Update, CT);
            Debug.LogAssertion("�^�C���A�E�g");
            process.PerfectKill();
        }
        catch (OperationCanceledException)
        {
            // �^�C���A�E�g�O�Ɏ��s�����������ꍇ�̓^�C���A�E�g�̗\�񎩑̃L�����Z�������
            Debug.Log("�^�C���A�E�g���L�����Z�����ꂽ");
        }
    }

    //==================================================
    // �v���Z�X���� {
    //     -> Kill() �Ńv���Z�X�̓��쑦��
    //     -> Dipose() �Ńv���Z�X�Ǝ��ӎ������Ǘ�������
    // }
    //==================================================
    public static void PerfectKill(this System.Diagnostics.Process process)
    {
        try
        {
            // ��O��ł�������Ɩ��ʃR�X�g�ɂ炵���̂Ŏ��O�Ƀv���Z�X���ғ����ȏꍇ�̂݃L��
            if (!process.HasExited)
            {
                process.Kill();           // �����I��
                process.WaitForExit();    // �I���m�F�i�^�C���A�E�g�t���Ă������j
            }
        }
        catch (InvalidOperationException)
        {
            // ���łɏI�����Ă������O������OK
        }
        finally
        {
            // �n���h�������͊m���ɉ��
            process.Dispose();           
        }
    }


    // ���K�V�[

    ////==================================================
    //// �R�}���h���s
    ////==================================================
    //public static async void Command(this System.Diagnostics.Process process, string command)
    //{
    //    if (process.StartInfo.RedirectStandardInput == false)
    //    {
    //        Debug.LogError("StartInfo.RedirectStandardInput �� True �ɂ���");
    //        return;
    //    }
    //    try
    //    {
    //        await UniTask.SwitchToThreadPool();
    //        StreamWriter inputWriter = process.StandardInput;
    //        inputWriter.WriteLine(command);
    //        inputWriter.Flush();
    //        await UniTask.SwitchToMainThread();
    //    }
    //    catch { }
    //}

    ////==================================================
    //// �v���Z�X�̔񓯊����s
    ////==================================================
    //public static UniTask<string> RunAsync(this System.Diagnostics.Process process, float timeout = 0, Action fncOnDispose = null)
    //{
    //    var cts = new CancellationTokenSource();
    //    var exited = new UniTaskCompletionSource<string>();
    //    string output = "";

    //    // �^�C���A�E�g���Ԃ��ݒ肳��Ă���ꍇ�͂��̎��ԂŃ^�C���A�E�g
    //    if (timeout != 0)
    //        UniTask.RunOnThreadPool(() => process.Timeout(timeout, cts.Token)).Forget();

    //    // Exited �C�x���g��L����
    //    process.EnableRaisingEvents = true;
    //    process.Exited += (sender, args) =>
    //    {
    //        // �G���[�ǎ��
    //        string error = process.StandardError.ReadToEnd();
    //        if (!string.IsNullOrEmpty(error)) Debug.LogError($"PowerShell Error: {error}");
    //        output = process.StandardOutput.ReadToEnd();
    //        process.PerfectKill();
    //    };

    //    process.Disposed += (sender, args) =>
    //    {
    //        exited.TrySetResult(output);
    //        cts.Cancel();
    //        fncOnDispose?.Invoke();
    //    };

    //    process.Start();

    //    return exited.Task;
    //}
}