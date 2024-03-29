﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace ffmpeg_command_builder
{
  using StringListItem = ListItem<string>;
  using StringListItems = List<ListItem<string>>;

  partial class Form1 : Form
  {
    // Static members
    // ---------------------------------------------------------------------------------
    private static string[] FindInPath(string CommandName)
    {
      //環境変数%PATH%取得し、カレントディレクトリを連結。配列への格納
      IEnumerable<string> dirPathList =
        Environment
          .ExpandEnvironmentVariables(Environment.GetEnvironmentVariable("PATH"))
          .Split([';'])
          .Prepend(Directory.GetCurrentDirectory());

      //正規表現に使用するため、%PATHEXT%の取得・ピリオド文字の変換及び配列への格納
      string[] pathext = Environment.GetEnvironmentVariable("PATHEXT").Replace(".", @"\.").Split([';']);

      //検索するファイル名の正規表現
      var regex = new Regex(
        $"^{CommandName}(?:{String.Join("|", pathext)})?$",
        RegexOptions.IgnoreCase
      );

      return
        dirPathList
          .Where(dirPath => Directory.Exists(dirPath))
          .SelectMany(dirPath => Directory.GetFiles(dirPath).Where(file => regex.IsMatch(Path.GetFileName(file))))
          .ToArray();
    }

    private static void CheckDirectory(string path)
    {
      if (string.IsNullOrEmpty(path))
        throw new Exception("出力先フォルダを指定してください。");

      if (!Directory.Exists(path))
        Directory.CreateDirectory(path);
    }

    private static bool IsFile(string path)
    {
      return (File.GetAttributes(path) & FileAttributes.Archive) == FileAttributes.Archive || (File.GetAttributes(path) & FileAttributes.Normal) == FileAttributes.Normal;
    }

    private static bool IsDirectory(string path)
    {
      return (File.GetAttributes(path) & FileAttributes.Directory) == FileAttributes.Directory;
    }

    // Instance members
    // ---------------------------------------------------------------------------------
    private CustomProcess CurrentProcess;
    Action<string> WriteOutput;
    Action<string> ProcessExit;
    Action ProcessDoneCallback;
    private StreamWriter LogWriter;
    private Encoding CP932;

    private void InitializeMembers()
    {
      Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
      CP932 = Encoding.GetEncoding(932);

      WriteOutput = output => { OutputStderr.Text = output; };
      ProcessExit = f =>
      {
        var item = FileListBindingSource.OfType<StringListItem>().FirstOrDefault(item => item.Value == f);
        if(item != null)
          FileListBindingSource.Remove(item);
      };
      ProcessDoneCallback = () =>
      {
        btnStop.Enabled = btnStopAll.Enabled = false;
        OpenLogFile.Enabled = true;
        if(FileListBindingSource.Count > 0)
          btnSubmitInvoke.Enabled = true;

        MessageBox.Show("変換処理が終了しました。");
      };
    }

    private ffmpeg_command CreateFFMpegCommandInstance()
    {
      ffmpeg_command ffcommand;

      var codec = UseVideoEncoder.SelectedValue as Codec;
      if (codec.GpuSuffix == "nvenc")
        ffcommand = new ffmpeg_command_cuda(ffmpeg.Text);
      else if (codec.GpuSuffix == "qsv")
        ffcommand = new ffmpeg_command_qsv(ffmpeg.Text);
      else
        ffcommand = new ffmpeg_command(ffmpeg.Text);

      return ffcommand;
    }

    private ffmpeg_command CreateAudioCommand()
    {
      var ffcommand = CreateFFMpegCommandInstance().audioOnly(true);

      ffcommand
        .audioOnly(true)
        .OutputPrefix(FilePrefix.Text)
        .OutputSuffix(FileSuffix.Text);

      if (chkEncodeAudio.Checked)
        ffcommand.acodec(UseAudioEncoder.SelectedValue.ToString()).aBitrate((int)aBitrate.Value);
      else
        ffcommand.acodec("copy").aBitrate(0);

      if (!string.IsNullOrEmpty(txtSS.Text))
        ffcommand.Starts(txtSS.Text);
      if (!string.IsNullOrEmpty(txtTo.Text))
        ffcommand.To(txtTo.Text);

      ffcommand.OutputPath = string.IsNullOrEmpty(cbOutputDir.Text) ? "." : cbOutputDir.Text;
      ffcommand.OutputExtension = FileContainer.SelectedValue.ToString();

      return ffcommand; 
    }

    private ffmpeg_command CreateCommand(bool isAudioOnly = false)
    {
      if (isAudioOnly)
        return CreateAudioCommand();

      var ffcommand = CreateFFMpegCommandInstance();

      if (!string.IsNullOrEmpty(txtSS.Text))
        ffcommand.Begin = txtSS.Text;
      if (!string.IsNullOrEmpty(txtTo.Text))
        ffcommand.End = txtTo.Text;

      var codec = UseVideoEncoder.SelectedValue as Codec;
      if(codec.Name == "copy")
      {
        if (!string.IsNullOrEmpty(cbOutputDir.Text))
          ffcommand.OutputPath = cbOutputDir.Text;

        ffcommand
          .OutputPrefix(FilePrefix.Text)
          .OutputSuffix(FileSuffix.Text);

        if (chkEncodeAudio.Checked)
          ffcommand.acodec(UseAudioEncoder.SelectedValue.ToString()).aBitrate((int)aBitrate.Value);
        else
          ffcommand.acodec("copy").aBitrate(0);

        return ffcommand;
      }

      ffcommand
        .vcodec(UseVideoEncoder.SelectedValue.ToString(), cbDevices.Items.Count > 1 ? cbDevices.SelectedIndex : 0)
        .vBitrate((int)vBitrate.Value, chkConstantQuality.Checked)
        .lookAhead((int)LookAhead.Value)
        .preset(cbPreset.SelectedValue.ToString());

      if (chkUseHWDecoder.Checked)
        ffcommand.hwdecoder(HWDecoder.SelectedValue.ToString());

      if (chkCrop.Checked)
        {
          if (chkUseHWDecoder.Checked && HWDecoder.SelectedValue.ToString().EndsWith("_cuvid"))
            ffcommand.size(decimal.ToInt32(VideoWidth.Value), decimal.ToInt32(VideoHeight.Value)).crop(true, CropWidth.Value, CropHeight.Value, CropX.Value, CropY.Value);
          else
            ffcommand.crop(CropWidth.Value, CropHeight.Value, CropX.Value, CropY.Value);
        }

      if (chkEncodeAudio.Checked)
        ffcommand.acodec(UseAudioEncoder.SelectedValue.ToString()).aBitrate((int)aBitrate.Value);
      else
        ffcommand.acodec("copy").aBitrate(0);

      if(!string.IsNullOrEmpty(cbOutputDir.Text))
        ffcommand.OutputPath = cbOutputDir.Text;

      ffcommand
        .OutputPrefix(FilePrefix.Text)
        .OutputSuffix(FileSuffix.Text);

      var deinterlaces = new List<string>() { "bwdif","bwdif_cuda","yadif","yadif_cuda","bob","adaptive" };

      if (chkFilterDeInterlace.Checked)
      {
        string value = cbDeinterlaceAlg.SelectedValue.ToString();
        if (cbDeinterlaceAlg.Text == "bwdif")
        {
          if (codec.GpuSuffix == "nvenc")
            ffcommand.setFilter("bwdif_cuda",value);
          else if (codec.GpuSuffix == "qsv")
            ffcommand.setFilter("bwdif",value);
        }
        else if(cbDeinterlaceAlg.Text == "yadif")
        {
          if (codec.GpuSuffix == "nvenc")
            ffcommand.setFilter("yadif_cuda",value);
          else if (codec.GpuSuffix == "qsv")
            ffcommand.setFilter("yadif",value);
        }
        else if(value == "bob" || value == "adaptive")
        {
          ffcommand.setFilter(value, value);
        }
      }
      else
      {
        foreach (var name in deinterlaces)
          ffcommand.removeFilter(name);
      }

      var tag = int.Parse(GetCheckedRadioButton(ResizeBox).Tag as string);
      var size = tag switch
      {
        0 => 0,
        720 or 1080 => int.Parse(tag.ToString()),
        -1 => (int)resizeTo.Value,
        _ => throw new Exception("size error"),
      };

      if (size > 0)
      {
        if (codec.GpuSuffix == "nvenc")
          ffcommand.setFilter("scale_cuda", rbLandscape.Checked ? $"-2:{size}" : $"{size}:-2");
        else if (codec.GpuSuffix == "qsv")
          ffcommand.setFilter("scale_qsv", rbLandscape.Checked ? $"-1:{size}" : $"{size}:-1");
      }
      else
      {
        ffcommand.removeFilter("scale_cuda").removeFilter("scale_qsv");
      }

      var rotate = int.Parse(GetCheckedRadioButton(RotateBox).Tag.ToString());
      if (rotate == 0)
        ffcommand.removeFilter("transpose");
      else
        ffcommand.setFilter("transpose", rotate.ToString());

      if(!string.IsNullOrEmpty(FreeOptions.Text))
        ffcommand.setOptions(FreeOptions.Text);

      return ffcommand;
    }

    private static RadioButton GetCheckedRadioButton(GroupBox groupBox)
    {
      return groupBox.Controls.OfType<RadioButton>().FirstOrDefault(radio => radio.Checked);
    }

    private void BeginFFmpegProcess()
    {
      string filename = FileListBindingSource.OfType<StringListItem>().First().Value;
      try
      {
        var process = CurrentCommand.InvokeCommand(filename, true);

        process.Exited += Process_Exited;
        process.ErrorDataReceived += new DataReceivedEventHandler(StdErrRead);
        process.Start();
        process.BeginErrorReadLine();

        CurrentProcess = process;
      }
      catch(Exception e)
      {
        MessageBox.Show(e.Message);
        Process_Exited(null, null);
      }
    }

    private void StdErrRead(object sender, DataReceivedEventArgs e)
    {
      if (e.Data == null)
        return;

      byte[] bytes = CP932.GetBytes(e.Data);
      LogWriter.WriteLine(Encoding.UTF8.GetString(bytes));

      if (!String.IsNullOrEmpty(e.Data))
        Invoke(WriteOutput, [e.Data]);
    }

    private void Process_Exited(object sender, EventArgs e)
    {
      try
      {
        string filename = CurrentProcess.CustomFileName;
        CurrentProcess = null;
        Invoke(ProcessExit,[filename]);
        BeginFFmpegProcess();
      }
      catch (InvalidOperationException) {
        Invoke(ProcessDoneCallback);
        LogWriter?.Dispose();
      }
    }

    private static string GetLogFileName()
    {
      return Path.Combine(
        Environment.ExpandEnvironmentVariables(Environment.GetEnvironmentVariable("TEMP")),
        "ffmpeg.stderr.log"
      );
    }

    private void OpenLogWriter()
    {
      LogWriter = new StreamWriter(GetLogFileName(), false);
    }

    private void StopProcess(bool stopAll = false)
    {
      if (stopAll)
        FileListBindingSource.Clear();

      if (CurrentProcess == null || CurrentProcess.HasExited)
        return;

      using StreamWriter sw = CurrentProcess.StandardInput;
      sw.Write('q');
    }

    private void OpenOutputView(string executable, string arg)
    {
      var form = new StdoutForm();
      if (HelpFormSize.Width > 0 && HelpFormSize.Height > 0)
      {
        form.Width = HelpFormSize.Width;
        form.Height = HelpFormSize.Height;
      }

      form.Shown += (s, e) => form.StartProcess(executable, arg);
      form.FormClosing += (s, e) =>
      {
        HelpFormSize.Width = form.Width;
        HelpFormSize.Height = form.Height;
      };

      form.Show();
    }

    private void InitPresetAndDevice(Codec codec)
    {
      cbPreset.DataSource = PresetList[codec.FullName];
      string hardwareName = string.Empty;

      if (codec.GpuSuffix == "nvenc")
      {
        cbPreset.SelectedIndex = 16;
        hardwareName = "nvidia";
      }
      else if (codec.GpuSuffix == "qsv")
      {
        cbPreset.SelectedIndex = 3;
        hardwareName = "intel";
      }

      if (codec.Name == "copy")
        return;

      cbDevices.SelectedIndex = cbDevices.FindString(hardwareName);

      if(chkConstantQuality.Checked)
        vQualityLabel.Text = codec.GpuSuffix == "qsv" ? "ICQ" : "-cq";
    }

    private static StringListItems GetGPUDeviceList()
    {
      var videoDevices = new ManagementObjectSearcher("select * from Win32_VideoController");
      var deviceList = new StringListItems();

      foreach (ManagementObject device in videoDevices.Get().Cast<ManagementObject>())
      {
        deviceList.Add(
          new StringListItem(
            device["AdapterCompatibility"].ToString(),
            device["Name"].ToString()
          )
        );
      }

      return deviceList;
    }

  }
}
