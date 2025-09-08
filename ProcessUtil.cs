using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using System.IO;
using System;
using System.Threading.Tasks;


public static class ProcessUtil
{
    ///==============================================<summary>
    /// ���s�t�@�C������PATH��ɑ��݂��邩�m�F
    ///</summary>=============================================
    public static bool Exe_Is_In_PATH(this string exeName)
    {
        string[] paths = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator);
        if (paths == null) return false;

        foreach (var path in paths)
        {
            string fullPath = $"{path}/{exeName}";
            if (File.Exists(fullPath)) return true;
        }
        return false;
    }


    ///==============================================<summary>
    /// �����I�Ȋ����� JObject ��n���v���Z�X�����s
    ///</summary>=============================================
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


    ///==============================================<summary>
    /// �v���Z�X�̔񓯊����s (�O������L�����Z����)
    ///</summary>=============================================
    public static async UniTask ExeAsync(this System.Diagnostics.Process process, float timeout = 0, Action fncOnDispose = null, CancellationToken externalCT = default)
    {
        await UniTask.SwitchToThreadPool();
        var timeoutCTS = new CancellationTokenSource();
        var exited = new UniTaskCompletionSource();

        //-----------------------------------------
        // �^�C���A�E�g���Ԃ��ݒ肳��Ă���ꍇ�͓o�^
        //-----------------------------------------
        if (timeout > 0)
        {
            UniTask.RunOnThreadPool(() => process.Timeout(timeout, timeoutCTS.Token)).Forget();
        }

        //-----------------------------------------
        // �v���Z�X�I��������
        //-----------------------------------------
        // Exited (�v���Z�X�I��) �C�x���g��L����
        process.EnableRaisingEvents = true;
        process.Exited += async (sender, args) =>
        {
            // �C�x���g���΃^�C�~���O�̃Y���ɂ��G���[�h�~�ň�U�m���ɏI����҂�
            process.WaitForExit();
            // �G���[�ǎ�� -> ���O�o��
            string e = await process.StandardError.ReadToEndAsync();
            if (!string.IsNullOrEmpty(e)) throw new Exception($"�v���Z�X�G���[�F{e}");
            // ���s���ʂ̏o�͂��Z�b�g
            exited.TrySetResult();
            // �v���Z�X����
            process.PerfectKill();
        };

        //-----------------------------------------
        // �v���Z�X����������
        //-----------------------------------------
        process.Disposed += (sender, args) =>
        {
            timeoutCTS.Cancel();
            fncOnDispose?.Invoke();
        };

        //-----------------------------------------
        // ���s -> ���s�����ꍇ�������^�X�N���c���Ȃ�
        //-----------------------------------------
        try
        {
            // Start ���s���m���ɕ\�ʉ�
            if (!process.Start())
            {
                timeoutCTS.Cancel();
                try 
                {
                    process.Dispose();
                }
                catch { }
                throw new Exception("�v���Z�X���s���s");
            }
        }
        catch (Exception)
        {
            timeoutCTS.Cancel();
            try
            {
                process.Dispose();
            }
            catch { }
            // �I���� await �ɐi�܂Ȃ�
            throw;
        }

        //-----------------------------------------
        // �O������̃L�����Z����ݒ�
        //-----------------------------------------
        try
        {
            // �O���L�����Z���𔽉f
            await exited.Task.AttachExternalCancellation(externalCT);
            Debug.Log($"�v���Z�X����");
        }

        //-----------------------------------------
        // �O������L�����Z�����ꂽ�ꍇ�ɑ��鏈��
        //-----------------------------------------
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


    ///==============================================<summary>
    /// �v���Z�X�̔񓯊����s (�ȈՔ�)
    /// ReadToEnd() �̃o�b�t�@���������̂�
    /// [ ��o�͂��󂯎�����荂���p�����s���鏈�� ] �ł� NG
    ///</summary>=============================================
    public static async UniTask<string> ExeAsync_Light(this System.Diagnostics.Process process, float timeout = 0, Action fncOnDispose = null)
    {
        string output = "";
        var timeoutCTS = new CancellationTokenSource();
        var exited = new UniTaskCompletionSource<string>();
        
        //-----------------------------------------
        // �^�C���A�E�g���Ԃ��ݒ肳��Ă���ꍇ�͓o�^
        //-----------------------------------------
        if (timeout > 0)
        {
            UniTask.RunOnThreadPool(() => process.Timeout(timeout, timeoutCTS.Token)).Forget();
        }

        var sbOut = new System.Text.StringBuilder();
        var sbErr = new System.Text.StringBuilder();
        
        //-----------------------------------------
        // �s�P�ʂŋl�܂���
        //-----------------------------------------
        process.OutputDataReceived += (_, e) => { if (e.Data != null) sbOut.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) sbErr.AppendLine(e.Data); };

        if (!process.Start())
            throw new Exception("�v���Z�X�N���Ɏ��s");

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        //-----------------------------------------
        // �I���҂��i�C�x���g���g��Ȃ��j
        //-----------------------------------------
        var waitExit = UniTask.RunOnThreadPool(() =>
        {
            process.WaitForExit();           // �{�̏I���҂�
            process.WaitForExit(200);        // I/O �h���C���̗P�\
            return true;
        });

        
        await waitExit;
        

        var code = process.ExitCode;
        var stdout = sbOut.ToString();
        var stderr = sbErr.ToString();

        try 
        {
            process.PerfectKill();
        } catch { }

        if (code != 0)
        {
            throw new Exception($"ExitCode={code}\n{stderr}");
        }

        // �x���͕Ԃ�l�ɕt���� or ���O�ɏo������
        if (!string.IsNullOrEmpty(stderr))
        {
            stdout += "\n[STDERR]\n" + stderr; // ���邢�� Debug.LogWarning(stderr);
        }

        return stdout;
    }



    //public static UniTask<string> ExeAsync_Light(this System.Diagnostics.Process process, float timeout = 0, Action fncOnDispose = null)
    //{
    //    string output = "";
    //    var timeoutCTS = new CancellationTokenSource();
    //    var exited = new UniTaskCompletionSource<string>();

    //    //-----------------------------------------
    //    // �^�C���A�E�g���Ԃ��ݒ肳��Ă���ꍇ�͓o�^
    //    //-----------------------------------------
    //    if (timeout > 0)
    //    {
    //        UniTask.RunOnThreadPool(() => process.Timeout(timeout, timeoutCTS.Token)).Forget();
    //    }

    //    //-----------------------------------------
    //    // �v���Z�X�I���������o�^
    //    //-----------------------------------------
    //    // Exited �C�x���g��L����
    //    process.EnableRaisingEvents = true;
    //    process.Exited += async (sender, args) =>
    //    {
    //        //// �C�x���g���΃^�C�~���O�̃Y���ɂ��G���[�h�~�ň�U�m���ɏI����҂�
    //        //process.WaitForExit();
    //        //// �G���[�ǂݎ��
    //        //string e = await process.StandardError.ReadToEndAsync();
    //        //if (!string.IsNullOrEmpty(e))
    //        //{
    //        //    exited.TrySetException(new Exception($"�G���[�F{e}"));
    //        //}
    //        //output = await process.StandardOutput.ReadToEndAsync();
    //        //process.PerfectKill();

    //        try
    //        {
    //            // �C�x���g���΃^�C�~���O�̃Y���ɂ��G���[�h�~�ň�U�m���ɏI����҂�
    //            process.WaitForExit();
    //            Debug.Log($"�v���Z�X0");
    //            // �G���[�ǂݎ��
    //            string stdErr = await process.StandardError.ReadToEndAsync();
    //            // ���ʓǂݎ��
    //            string stdOut = await process.StandardOutput.ReadToEndAsync();
    //            Debug.Log($"�v���Z�X1");

    //            int code = process.ExitCode;

    //            // ����/���s�������Ŋ���������
    //            if (code != 0 || !string.IsNullOrEmpty(stdErr))
    //                exited.TrySetException(new Exception($"ExitCode={code}\n{stdErr}"));
    //            else
    //                exited.TrySetResult(stdOut);
    //        }
    //        catch (Exception e)
    //        {
    //            exited.TrySetException(e);
    //        }
    //        finally
    //        {
    //            timeoutCTS.Cancel();
    //            try
    //            {
    //                process.PerfectKill();
    //            }
    //            catch { }
    //        }
    //    };

    //    //-----------------------------------------
    //    // �v���Z�X�����������o�^
    //    //-----------------------------------------
    //    process.Disposed += (sender, args) =>
    //    {
    //        exited.TrySetResult(output);
    //        timeoutCTS.Cancel();
    //        fncOnDispose?.Invoke();
    //    };

    //    //-----------------------------------------
    //    // ���s -> ���s�����ꍇ�������^�X�N���c���Ȃ�
    //    //-----------------------------------------
    //    try
    //    {
    //        // Start ���s���m���ɕ\�ʉ�
    //        if (!process.Start())
    //        {
    //            exited.TrySetException(new Exception("�v���Z�X���s���s"));
    //            timeoutCTS.Cancel();
    //            try
    //            {
    //                process.Dispose();
    //            }
    //            catch { }
    //            return exited.Task;
    //        }
    //    }
    //    catch (Exception ex)
    //    {
    //        exited.TrySetException(ex);
    //        timeoutCTS.Cancel();
    //        try
    //        {
    //            process.Dispose();
    //        }
    //        catch { }
    //        return exited.Task;
    //    }

    //    return exited.Task;
    //}


    ///==============================================<summary>
    /// �^�C���A�E�g
    ///</summary>=============================================
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


    ///==============================================<summary>
    /// �v���Z�X���� {
    ///     -> Kill() �Ńv���Z�X�̓��쑦��
    ///     -> Dipose() �Ńv���Z�X�Ǝ��ӎ������Ǘ�������
    /// }
    /// 
    ///</summary>=============================================
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
        catch (Exception)
        {
            // ���łɏI�����Ă������O������OK
        }
        finally
        {
            // �n���h�������͊m���ɉ��
            process.Dispose();           
        }
    }
}