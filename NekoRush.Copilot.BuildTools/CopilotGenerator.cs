using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using NekoRush.JsonChan;

namespace Generators;

public class Program
{
    private static string _projectFile;
    private static string _projectPath;
    private static string _outputPath;
    private static (string Url, string Key, string Model) _copilotApi;

    public static void Main(string[] cmdline)
    {
        Print("------ NekoRush.Copilot.BuildTools ------");

        // Get project path from cmdline
        _projectFile = GetProjFile(cmdline);
        _projectPath = string.Join("/", _projectFile.Split('/')[..^1]);
        Print($"Project: {_projectPath}");
        Print($"Project File: {_projectFile}");

        // Get output path
        _outputPath = GetOutputPath(cmdline);
        Directory.Delete(_outputPath, true);
        Directory.CreateDirectory(_outputPath);
        Print($"Clean: {_outputPath}");

        // Enum copilot files and compiles
        EnumProjectFile(_projectFile, OnParseCopilotAPI, OnParseCopilotFile);

    }

    /// <summary>
    /// Parse xml expression and get the copilot prompt string
    /// </summary>
    /// <param name="node"></param>
    private static void OnParseCopilotFile(XmlNode node)
    {
        var prompt = "";
        TryGetAttr(node.Attributes, "Include", out var attrInclude);
        {
            if (attrInclude != "")
            {
                Print($"Copilot File: {_projectPath}/{attrInclude}");
                prompt = File.ReadAllText($"{_projectPath}/{attrInclude}");
            }
        }

        // No prompt found, return
        if (prompt == "")
        {
            Print("ERROR: Tje prompt text is empty");
            return;
        }

        // Request API then fetch the result
        var result = GetCopilotResult(prompt).Result;
        if (result == "")
        {
            Print("ERROR: Failed to fetch API result");
            return;
        }

        // Code generation
        var filename = attrInclude.Replace("\\", "/");
        var outdir = FormatPathString( $"{_outputPath}/{GetFilePath(filename)}");
        var outfile = FormatPathString($"{outdir}/{GetFileName(filename)}.g.cs");
        {
            Directory.CreateDirectory(outdir);
            File.WriteAllText(outfile, result);
        }

        Print($"Generated file: {outfile}");
    }

    private static void OnParseCopilotAPI(XmlNode node)
    {
        TryGetAttr(node.Attributes, "Api", out _copilotApi.Url);
        TryGetAttr(node.Attributes, "ApiKey", out _copilotApi.Key);
        TryGetAttr(node.Attributes, "Model", out _copilotApi.Model);
        Print($"Use API: {_copilotApi.Url}");
        Print($"Use Model: {_copilotApi.Model}");
    }

    /// <summary>
    /// Enum project files and generates source code via copilot
    /// </summary>
    /// <param name="proj"></param>
    /// <param name="action"></param>
    /// <returns></returns>
    private static void EnumProjectFile(string proj, Action<XmlNode> onApi, Action<XmlNode> onFile)
    {
        var xml = new XmlDocument();
        xml.LoadXml(File.ReadAllText(proj));
        EnumXml(xml.ChildNodes, onApi, onFile);
    }

    private static void EnumXml(XmlNodeList nodes, Action<XmlNode> onApi, Action<XmlNode> onFile)
    {
        foreach (XmlNode node in nodes)
        {
            if (node.ChildNodes.Count != 0)
                EnumXml(node.ChildNodes, onApi, onFile);

            switch (node.Name)
            {
                case "ItemGroup":
                    break;

                // Parse API entry
                case "CopilotApi":
                    onApi.Invoke(node);
                    break;

                // Parse Copilot files
                case "CopilotGenerate":
                    onFile.Invoke(node);
                    break;
            }
        }
    }

    /// <summary>
    /// Get .csproj file path from cmdline
    /// </summary>
    /// <param name="cmdline"></param>
    /// <returns></returns>
    private static string GetProjFile(string[] cmdline)
        => FormatPathString(FindCmdLine(cmdline, "--proj"));

    private static string GetOutputPath(string[] cmdline)
        => FormatPathString(FindCmdLine(cmdline, "--out"));

    /// <summary>
    /// Find cmd line
    /// </summary>
    /// <param name="cmdline"></param>
    /// <param name="find"></param>
    /// <returns></returns>
    private static string FindCmdLine(string[] cmdline, string find)
    {
        var cmd = find + "=";
        foreach (string part in cmdline)
        {
            if (part.StartsWith(cmd))
            {
                return part.Replace(cmd, "");
            }
        }

        return "";
    }

    private static void Print(string msg) => Console.WriteLine(msg);

    private static bool TryGetAttr(XmlAttributeCollection xac, string s, out string v)
    {
        try
        {
            v = xac[s].Value;
            return true;
        }
        catch
        {
            v = "";
            return false;
        }
    }

    /// <summary>
    /// Get file name
    /// </summary>
    /// <param name="file"></param>
    /// <returns></returns>
    private static string GetFileName(string file)
    {
        var name = file.Split("/").Last();
        if (name == "") return "";

        var extension = name.Split(".").Last();
        name = name[..(name.Length - extension.Length - 1)];
        return name;
    }

    /// <summary>
    /// Get the path of the file
    /// </summary>
    /// <param name="file"></param>
    /// <returns></returns>
    private static string GetFilePath(string file)
    {
        var name = file.Split("/")[..^1];
        if (name.Length == 0) return "";

        return string.Join("/", name);
    }

    private static string FormatPathString(string path)
        => path.Trim('\'').Replace("\\", "/").Replace("//", "/");

    /// <summary>
    /// Http post
    /// </summary>
    /// <param name="url"></param>
    /// <param name="json"></param>
    /// <param name="header"></param>
    /// <param name="param"></param>
    /// <param name="timeout"></param>
    /// <returns></returns>
    private static async Task<byte[]> PostJson(string url, string json,
        Dictionary<string, string> header = null, Dictionary<string, string> param = null,
        int timeout = 8000)
    {
        // Append params to url
        if (param != null)
        {
            var args = param.Aggregate("", (current, i)
                => current + $"&{i.Key}={i.Value}");

            url += $"?{args[1..]}";
        }

        // Create request
        var client = new HttpClient();
        {
            client.Timeout = new TimeSpan(0, 0, timeout);

            // Append request header
            if (header != null)
            {
                foreach (var (k, v) in header)
                    client.DefaultRequestHeaders.Add(k, v);
            }
        }

        // Receive the response data
        var reqStream = await client.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"));
        return await reqStream.Content.ReadAsByteArrayAsync();
    }

    private static async Task<string> GetCopilotResult(string prompt)
    {
        var fmt = prompt.Replace("\"", "'").Replace("\n", "");
        var data = $"{{\"model\":\"{_copilotApi.Model}\"," +
            $"\"temperature\":0.7," +
            $"\"messages\":[{{\"role\":\"user\",\"content\":\"{fmt}\"}}]}}";

        Print($"Request: {data}");

        // Request API
        var result = await PostJson(_copilotApi.Url, data, new()
        {
            { "Authorization", $"Bearer {_copilotApi.Key}"}
        });

        try
        {
            var text = Encoding.UTF8.GetString(result);
            Print($"Response: {text}");

            // Parse json
            var json = Json.Parse(text);
            var markdown = json.choices[0].message.content as string;

            // split with \n character
            var split = markdown.Split("\\n");
            var codeStart = 0;
            var codeEnd = 0;
            for (var i = 0; i < split.Length; ++i)
            {
                if (codeStart == 0 && split[i].StartsWith("```"))
                    codeStart = i + 1;
                if (codeEnd == 0 && split[i].EndsWith("```"))
                    codeEnd = i;
            }

            // Remove markdown symbols
            return string.Join("\n", split[codeStart..codeEnd]);
        }
        catch (Exception ex)
        {
            Print(ex.Message);
            Print(ex.StackTrace);
            return "";
        }
    }
}
