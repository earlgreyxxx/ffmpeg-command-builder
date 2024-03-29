﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ffmpeg_command_builder
{
  internal partial class ffmpeg_command : IEnumerable<string>
  {
    // instances
    protected int FileIndex = 1;
    private string _file_prefix;
    private string _file_suffix;
    private string _file_base;
    private Encoding CP932;

    [GeneratedRegex(@"^(?:\d{2}:)?\d{2}:\d{2}(?:\.\d+)?$")]
    private static partial Regex IsDateTime();

    [GeneratedRegex(@"^\d+(?:\.\d+)?(?:s|ms|us)?$", RegexOptions.IgnoreCase, "ja-JP")]
    private static partial Regex IsSecondTime();

    [GeneratedRegex(@"^(\w+)$")]
    private static partial Regex IsWord();

    [GeneratedRegex(@"\s")]
    private static partial Regex HasSpace();
    
    [GeneratedRegex(@"[;,:]+")]
    protected static partial Regex SplitCommaColon();

    protected Dictionary<string,string> filters;
    protected Dictionary<string, string> options;
    protected string InputPath;

    public string OutputPath { get; set; }
    public string OutputExtension { get; set; }
    public string ffmpegPath {  get; set; }
    public bool bAudioOnly {  get; set; }
    public string EncoderType {  get; set; }
    public int IndexOfGpuDevice {  get; set; }
    public int Width { get; set; }
    public int Height {  get; set; }
    public string AdditionalOptions { get; set; }

    public string FilePrefix
    {
      get => _file_prefix;
      set => _file_prefix = value.Trim();
    }
    public string FileSuffix
    {
      get => _file_suffix;
      set => _file_suffix = value.Trim();
    }
    public string FileBase
    {
      get => _file_base;
      set
      {
        _file_base = value.Trim();
        FileIndex = 1;
      }
    }
    public int LookAhead
    {
      set
      {
        if (value <= 0 && options.ContainsKey("lookahead"))
          options.Remove("lookahead");

        if (value > 0)
          options["lookahead"] = value.ToString();
      }
      protected get
      {
        int rv = 0;
        if (options.TryGetValue("lookahead",out string value))
          rv = int.Parse(value);

        return rv;
      }
    }
    public string Begin
    {
      get => options["ss"];
      set => options["ss"] = EvalTimeString(value);
    }
    public string End
    {
      get => options["to"];
      set => options["to"] = EvalTimeString(value);
    }

    public string ACodec
    {
      set
      {
        if (String.IsNullOrEmpty(value))
          throw new ArgumentNullException(nameof(value));

        options["acodec"] = $"-c:a {value}";
      }
    }
    public int ABitrate
    {
      set
      {
        if (value > 0)
          this.options["b:a"] = $"-b:a {value}K";
        else
          this.options.Remove("b:a");
      }
    }
    public string Preset
    {
      set => options["preset"] = $"-preset {value}";
    }

    public ffmpeg_command(string ffmpegpath)
    {
      Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
      CP932 = Encoding.GetEncoding(932);

      filters = [];
      options = new Dictionary<string, string>
      {
        ["tag:v"] = "-tag:v hvc1",
        ["vcodec"] = "-c:v copy",
        ["acodec"] = "-c:a copy",
        ["b:v"] = string.Empty,
        ["preset"] = string.Empty
      };
      OutputPath = ".";
      ffmpegPath = "ffmpeg";
      bAudioOnly = false;
      EncoderType = "hevc";
      IndexOfGpuDevice = 0;
      FileBase = string.Empty;
      FilePrefix = string.Empty;
      FileSuffix = string.Empty;
      Width = -1;
      Height = -1;

      if (!String.IsNullOrEmpty(ffmpegpath) && File.Exists(ffmpegpath))
        ffmpegPath = ffmpegpath;
    }

    public virtual IEnumerator<string> GetEnumerator()
    {
      yield return "-hide_banner";
      yield return "-y";

      if (options.TryGetValue("ss",out string ss) && !string.IsNullOrEmpty(ss))
        yield return $"-ss {ss}";
      if (options.TryGetValue("to",out string to) && !string.IsNullOrEmpty(to))
        yield return $"-to {to}";

      yield return $"-i \"{InputPath}\"";

      if (bAudioOnly)
      {
        yield return "-vn";
      }
      else
      {
        yield return options["vcodec"];
      }

      if(!string.IsNullOrEmpty(AdditionalOptions))
        foreach(var option in SplitCommaColon().Split(AdditionalOptions))
          yield return option.Trim();

      yield return options["acodec"];
      if (options.TryGetValue("b:a", out string ba) && !string.IsNullOrEmpty(ba))
        yield return ba;
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
      return (IEnumerator)GetEnumerator();
    }

    public ffmpeg_command Starts(string strTime)
    {
      Begin = strTime;
      return this;
    }

    public ffmpeg_command To(string strTime)
    {
      End = strTime;
      return this;
    }

    public ffmpeg_command acodec(string strCodec)
    {
      ACodec = strCodec;
      return this;
    }

    public ffmpeg_command aBitrate(int bitrate)
    {
      ABitrate = bitrate;
      return this;
    }

    public virtual ffmpeg_command vcodec(string strCodec, int indexOfGpuDevice = 0)
    {
      // copy nothing to do...
      return this;
    }

    public virtual ffmpeg_command vBitrate(int value, bool bCQ = false)
    {
      // copy nothing to do...
      return this;
    }

    public virtual ffmpeg_command crop(bool hw,decimal width, decimal height, decimal x = -1, decimal y = -1)
    {
      // copy nothing to do...
      return this;
    }

    public virtual ffmpeg_command crop(decimal width, decimal height, decimal x = -1, decimal y = -1)
    {
      // copy nothing to do...
      return this;
    }

    public ffmpeg_command setFilter(string name,string value)
    {
      filters[name] = value;
      return this;
    }
    public ffmpeg_command removeFilter(string name)
    {
      filters.Remove(name);
      return this;
    }

    public ffmpeg_command setOptions(string option)
    {
      AdditionalOptions = option.Trim();
      return this;
    }

    public ffmpeg_command preset(string str)
    {
      Preset = str;
      return this;
    }

    public ffmpeg_command OutputPrefix(string prefix = "")
    {
      FilePrefix = prefix;
      return this;
    }

    public ffmpeg_command OutputSuffix(string suffix = "")
    {
      FileSuffix = suffix;
      return this;
    }

    public ffmpeg_command OutputBaseName(string basename = "")
    {
      FileBase = basename;
      return this;
    }

    public ffmpeg_command audioOnly(bool b)
    {
      bAudioOnly = b;
      return this;
    }

    public ffmpeg_command lookAhead(int frames)
    {
      LookAhead = frames;
      return this;
    }

    public ffmpeg_command hwdecoder(string decoder)
    {
      options["hwdecoder"] = decoder;
      return this;
    }

    public ffmpeg_command size(int w,int h)
    {
      Width = w;
      Height = h;
      return this;
    }

    public string GetCommandLineArguments(string strInputPath)
    {
      var args = GetArguments(strInputPath);
      return string.Join(" ", args.ToArray());
    }

    public string GetCommandLine(string strInputPath)
    {
      string command = ffmpegPath;
      if (HasSpace().IsMatch(ffmpegPath))
        command = $"\"{ffmpegPath}\"";

      return $"{command} {GetCommandLineArguments(strInputPath)}";
    }

    public IEnumerable<string> GetArguments(string strInputPath)
    {
      InputPath = strInputPath;
      var args = this.ToList();

      string strOutputFileName = CreateOutputFileName(strInputPath);
      string strOutputFilePath = Path.Combine(OutputPath, strOutputFileName);
      if (strOutputFilePath == strInputPath)
        throw new Exception("入力ファイルと出力ファイルが同じです。");

      args.Add($"\"{strOutputFilePath}\"");

      return args;
    }

    public CustomProcess InvokeCommand(string strInputPath,bool suspend = false)
    {
      var psi = new ProcessStartInfo()
      {
        FileName = ffmpegPath,
        Arguments = GetCommandLineArguments(strInputPath),
        UseShellExecute = false,
        RedirectStandardError = true,
        RedirectStandardInput = true,
        StandardErrorEncoding = CP932,
        CreateNoWindow = true
      };

      psi.Environment.Add("AV_LOG_FORCE_NOCOLOR", "1");

      var process = new CustomProcess()
      {
        StartInfo = psi,
        EnableRaisingEvents = true,
        CustomFileName = strInputPath
      };

      if(!suspend)
        process.Start();

      return process;
    }

    public async void ToBatchFile(string filename,IEnumerable<string> files)
    {
      var commandlines = files.Select(file => GetCommandLine(file));

      using var sw = new StreamWriter(filename, false, Encoding.GetEncoding(932));
      await sw.WriteLineAsync("@ECHO OFF");
      foreach (var commandline in commandlines)
        await sw.WriteLineAsync(commandline);

      await sw.WriteLineAsync("PAUSE");
    }

    protected static string EvalTimeString(string strTime)
    {
      string rv = null;
      if (!string.IsNullOrEmpty(strTime))
      {
        var m1 = IsDateTime().Match(strTime);
        var m2 = IsSecondTime().Match(strTime);

        rv = null;
        if (m1.Success || m2.Success)
          rv = strTime;
      }
      return rv;
    }

    protected string CreateOutputFileName(string strInputPath)
    {
      string strOutputFileName = Path.GetFileName(strInputPath);
      string basename = string.IsNullOrEmpty(FileBase) ? Path.GetFileNameWithoutExtension(strInputPath) : string.Format("{0}{1:D2}",FileBase,FileIndex++);

      if (string.IsNullOrEmpty(OutputExtension))
      {
        if (bAudioOnly)
        {
          var m = IsWord().Match(options["acodec"]);
          if (m.Success)
          {
            switch (m.Captures[0].Value)
            {
              case "aac":
                strOutputFileName = $"{FilePrefix}{basename}{FileSuffix}.aac";
                break;
              case "libmp3lame":
                strOutputFileName = $"{FilePrefix}{basename}{FileSuffix}.mp3";
                break;
            }
          }
        }
        else
        {
          strOutputFileName = $"{FilePrefix}{basename}{FileSuffix}.mp4";
        }
      }
      else
      {
        strOutputFileName = $"{FilePrefix}{basename}{FileSuffix}{OutputExtension}";
      }

      return strOutputFileName;
    }

  }
}
