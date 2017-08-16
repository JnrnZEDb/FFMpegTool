using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FFMpegTest
{
    public partial class Form1 : Form
    {
        private string VideoPath = "C:\\Windows\\Temp\\";
        private const string VideoFileExtension = ".mp4";

        private string _tempFolder = "C:\\Windows\\Temp";
        private const string PrepareToCombineTemplate = "-i \"{0}\" -c copy -bsf h264_mp4toannexb -y \"{1}\"";
        private const string CombineTemplate = "-i \"concat:\"{0}\"\" -c copy -bsf:a aac_adtstoasc -y \"{1}\"";


        public Form1()
        {
            InitializeComponent();
        }

        private void PrintLog(string log)
        {
            if(richTextBox1.Text == string.Empty)
                richTextBox1.Text = log;
            else
                richTextBox1.Text += "\n" + log;
        }

        private int ParseDurationLine(string line)
        {
            var items = line.Split(" "[0], "="[0]).Where(s => string.IsNullOrEmpty(s) == false).
                Select(s => s.Trim().Replace("=", string.Empty).Replace(",", string.Empty)).ToList();

            var key = items.FirstOrDefault(i => i.ToUpper() == "Duration:".ToUpper());

            if (key == null)
                return 0;
            var idx = items.IndexOf(key) + 1;
            if (idx >= items.Count)
                return 0;

            return (int)TimeSpan.Parse(items[idx]).TotalSeconds;
        }

        private int ExecuteFfmpegCommand(string args, bool parseDuration = false)
        {
            using (var ffmpeg = new Process())
            {
                ffmpeg.StartInfo = new ProcessStartInfo()
                {
                    FileName = new FFMpeg.FFMpegWrapper().FFMpegExe,
                    RedirectStandardOutput = false,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    Arguments = args
                };

                ffmpeg.Start();
                var standardError = ffmpeg.StandardError.ReadToEnd();
                ffmpeg.WaitForExit();
                EnsureFFMpegDone(ffmpeg.ExitCode, standardError);

                if (parseDuration)
                {
                    var encodingLines =
                    standardError.Split(System.Environment.NewLine[0])
                        .Where(
                            line =>
                                string.IsNullOrWhiteSpace(line) == false &&
                                string.IsNullOrEmpty(line.Trim()) == false)
                        .Select(s => s.Trim())
                        .ToList();
                    foreach (var line in encodingLines)
                    {
                        if (line.StartsWith("Duration"))
                        {
                            return ParseDurationLine(line);
                        }
                    }
                }

                return 0;
            }
        }

        private void EnsureFFMpegDone(int exitCode, string standardError)
        {
            if (exitCode != 0)
                PrintLog(string.Format("ffmpeg.exe has failed with the error: {0}", standardError ?? ""));
        }

        private void Combine(string[] videos, string resultVideoPath)
        {
            PrintLog("Combine videos into the single file");

            var tempVideos = new List<string>();
            var args = "";

            foreach (var video in videos)
            {
                // Creating .ts (part file) as new Guid to avoid files containing space in it
                var tempFileName = String.Format(CultureInfo.InvariantCulture, "{0}.ts", Guid.NewGuid());

                var tempVideoPath = Path.Combine(_tempFolder, tempFileName);
                tempVideos.Add(tempVideoPath);

                args = String.Format(CultureInfo.InvariantCulture, PrepareToCombineTemplate, video, tempVideoPath);

                PrintLog(String.Format(CultureInfo.InvariantCulture, "Execute: ffmpeg.exe {0}", args));

                ExecuteFfmpegCommand(args);
            }


            args = String.Format(CultureInfo.InvariantCulture, CombineTemplate, String.Join("\"|\"", tempVideos), resultVideoPath);

            ExecuteFfmpegCommand(args);

            // Deleting local part files (.ts extension)
            for (int i = 0; i < tempVideos.Count; i++)
            {
                if (File.Exists(tempVideos[i]))
                {
                    PrintLog(string.Format("Removing: {0}", tempVideos[i]));
                    File.Delete(tempVideos[i]);
                }
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            string name1 = textBox1.Text;
            string name2 = textBox2.Text;
            string result = textBox3.Text;

            List<string> videos = new List<string>();
            videos.Add(VideoPath + name1 + VideoFileExtension);
            videos.Add(VideoPath + name2 + VideoFileExtension);

            richTextBox1.Text = "";

            var watch = Stopwatch.StartNew();
            Combine(videos.ToArray(), VideoPath + result + VideoFileExtension);
            watch.Stop();
            var elapsedMs = watch.ElapsedMilliseconds;

            labelTime.Text = elapsedMs.ToString();

            labelLength.Text = ExecuteFfmpegCommand("-y -i \"" + VideoPath + result + VideoFileExtension + "\"", true).ToString();
        }
    }
}
