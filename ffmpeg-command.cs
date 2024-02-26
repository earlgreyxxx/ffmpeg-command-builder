﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace ffmpeg_command_builder
{
  internal abstract class ffmpeg_command
  {
    public abstract string GetCommandLineArguments(string strInputPath);
    public abstract ffmpeg_command vcodec(string strCodec, int indexOfGpuDevice = 0);

    // instances
    protected Dictionary<string,string> filters;
    protected Dictionary<string, string> options;
    protected string OutputPath;
    protected string ffmpegPath = "ffmpeg";
    protected bool bAudioOnly = false;
    protected string EncoderType = "hevc";
    protected int IndexOfGpuDevice = 0;

    protected void Initialize(string ffmpegPath)
    {
      filters = new Dictionary<string, string>();
      options = new Dictionary<string, string>
      {
        ["default"] = "-hide_banner -y",
        ["tag:v"] = "-tag:v hvc1",
        ["acodec"] = "-c:a copy",
        ["b:v"] = string.Empty,
        ["preset"] = string.Empty
      };
      OutputPath = ".";

      if (!String.IsNullOrEmpty(ffmpegPath) && File.Exists(ffmpegPath))
        this.ffmpegPath = ffmpegPath;
    }

    public ffmpeg_command Starts(string strTime)
    {
      if (!string.IsNullOrEmpty(strTime))
      {
        var m1 = Regex.Match(strTime, @"^(?:\d{2}:)?\d{2}:\d{2}(?:\.\d+)?$");
        var m2 = Regex.Match(strTime, @"^\d+(?:\.\d+)?(?:s|ms|us)?$",RegexOptions.IgnoreCase);

        options["ss"] = null;
        if (m1.Success || m2.Success)
          this.options["ss"] = $"-ss {strTime}";
      }
      else
      {
        options["ss"] = null;
      }

      return this;
    }

    public ffmpeg_command To(string strTime)
    {
      if (!string.IsNullOrEmpty(strTime))
      {
        var m1 = Regex.Match(strTime, @"^(?:\d{2}:)?\d{2}:\d{2}(?:\.\d+)?$");
        var m2 = Regex.Match(strTime, @"^\d+(?:\.\d+)?(?:s|ms|us)?$",RegexOptions.IgnoreCase);

        options["to"] = null;
        if (m1.Success || m2.Success)
          options["to"] = $"-to {strTime}";
      }
      else
      {
        options["to"] = null;
      }

      return this;
    }

    public ffmpeg_command acodec(string strCodec)
    {
      if (!String.IsNullOrEmpty(strCodec))
        this.options["acodec"] = $"-c:a {strCodec}";

      return this;
    }

    public ffmpeg_command vBitrate(int value,bool bCQ = false)
    {
      if (value <= 0)
      {
        options["b:v"] = EncoderType == "hevc" ? "-b:v 0 -cq 25" : "-crf 23";
        return this;
      }

      if (bCQ)
        options["b:v"] = EncoderType == "hevc" ? $"-b:v 0 -cq {value}" : $"-crf {value}";
      else
        options["b:v"] = $"-b:v {value}K";

      return this;
    }

    public ffmpeg_command aBitrate(int bitrate)
    {
      if (bitrate > 0)
        this.options["b:a"] = $"-b:a {bitrate}K";
      else
        this.options.Remove("b:a");
      return this;
    }

    public ffmpeg_command setFilter(string name,string value)
    {
      filters[name] = value;
      return this;
    }
    public ffmpeg_command removeFilter(string name)
    {
      if(filters.ContainsKey(name))
        filters.Remove(name);
      return this;
    }

    public ffmpeg_command preset(string str)
    {
      options["preset"] = $"-preset {str}";
      return this;
    }

    public ffmpeg_command outputPath(string path)
    {
      OutputPath = string.IsNullOrEmpty(path) ? "." : path;
      return this;
    }

    public ffmpeg_command audioOnly(bool b)
    {
      bAudioOnly = b;
      return this;
    }

    public string GetCommandLine(string strInputPath)
    {
      string command = ffmpegPath;
      if (Regex.IsMatch(ffmpegPath, @"\s"))
        command = $"\"{ffmpegPath}\"";

      return $"{command} {GetCommandLineArguments(strInputPath)}";
    }

    public Process InvokeCommand(string strInputPath,bool suspend = false)
    {
      var psi = new ProcessStartInfo()
      {
        FileName = ffmpegPath,
        Arguments = GetCommandLineArguments(strInputPath),
        UseShellExecute = false,
        RedirectStandardError = true,
        RedirectStandardInput = true,
        CreateNoWindow = true
      };

      var process = new Process()
      {
        StartInfo = psi,
        EnableRaisingEvents = true,
      };

      if(!suspend)
        process.Start();

      return process;
    }
  }
}