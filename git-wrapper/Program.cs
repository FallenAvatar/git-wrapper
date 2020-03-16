/****************************************************************
 * Lots of code borrowed from https://stackoverflow.com/a/30517342/36965
 */

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace git_wrapper
{
	internal class Program
	{
		private static string FixPath(string s)
		{
			var t = s;
			var p = "";
			if (s.Contains('='))
			{
				var idx = s.LastIndexOf('=');
				t = s.Substring(idx + 1);
				p = s.Substring(0, idx);
			}

			if (!t.Contains(Path.DirectorySeparatorChar))
				return s;

			if (Path.IsPathRooted(t))
			{
				t = "/mnt/" + t[0].ToString().ToLower() + t.Substring(2);
			}

			return p + t.Replace('\\', '/');
		}

#if DEBUG
		private static TextWriter s_fullLog;
		private static TextWriter s_errorLog;
		private static TextWriter s_inLog;
		private static TextWriter s_outLog;
		private static string input = "";
		private static string output = "";
		private static string error = "";

		private static TextWriter CreateLog(string fn)
		{
			Stream s;

			if (false)
			{
				s = new FileStream(fn, FileMode.Append, FileAccess.Write);
			}
			else
			{
				s = Stream.Null;
			}

			var sw = new StreamWriter(s);
			sw.AutoFlush = true;

			return sw;
		}
#endif

		private static int Main(string[] args)
		{
#if DEBUG
			var r = new Random();
			var sessionId = r.Next(1000, 10000).ToString();
			var log_path = @"D:\Utils\git-wrapper\";

			s_fullLog = CreateLog(Path.Combine(log_path, "log-" + sessionId + ".txt"));
			s_errorLog = CreateLog(Path.Combine(log_path, "log-error-" + sessionId + ".txt"));
			s_inLog = CreateLog(Path.Combine(log_path, "log-in-" + sessionId + ".txt"));
			s_outLog = CreateLog(Path.Combine(log_path, "log-out-" + sessionId + ".txt"));

			s_fullLog.WriteLine("\n-------------------------------------------------------------------------------------------------");
			s_fullLog.Write("Args: ");
#endif
			var sa = "";
			foreach (var a in args)
			{
				var sap = FixPath(a);

				if (sap.IndexOf(' ') > 0)
					sap = '"' + sap + '"';

				sa += " " + sap;
			}
#if DEBUG
			s_fullLog.WriteLine(sa);
#endif
			var exepath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "wsl.exe");
#if DEBUG
			s_fullLog.WriteLine("Working Directory: " + Environment.CurrentDirectory);
#endif
			var p = new Process();
			p.StartInfo = new ProcessStartInfo();
			p.StartInfo.FileName = exepath;
			p.StartInfo.Arguments = "git" + sa;
			p.StartInfo.RedirectStandardError = true;
			p.StartInfo.RedirectStandardInput = true;
			p.StartInfo.RedirectStandardOutput = true;
			p.StartInfo.CreateNoWindow = true;
			p.StartInfo.UseShellExecute = false;
			p.StartInfo.WorkingDirectory = Environment.CurrentDirectory;

			p.EnableRaisingEvents = true;

			// Depending on your application you may either prioritize the IO or the exact opposite
			const ThreadPriority ioPriority = ThreadPriority.Normal;
			var outputThread = new Thread(outputReader) { Name = "ChildIO Output", Priority = ioPriority };
			var errorThread = new Thread(errorReader) { Name = "ChildIO Error", Priority = ioPriority };
			var inputThread = new Thread(inputReader) { Name = "ChildIO Input", Priority = ioPriority };

			// Set as background threads (will automatically stop when application ends)
			outputThread.IsBackground = errorThread.IsBackground = inputThread.IsBackground = true;

			// Signal to end the application
			var stopApp = new ManualResetEvent(false);

			p.Exited += (e, sender) => { stopApp.Set(); };

#if DEBUG
			//Debugger.Launch();
#endif

			p.Start();

			// Start the IO threads
			outputThread.Start(p);
			errorThread.Start(p);
			inputThread.Start(p);

			// Wait for the child app to stop
			stopApp.WaitOne();

			// Wait for the child app to clean up
			p.WaitForExit();

			//var ret = ((1 == p.ExitCode) ? 0 : ((0 == p.ExitCode) ? 1 : p.ExitCode));
			var ret = p.ExitCode;
#if DEBUG
			s_fullLog.WriteLine("Return: " + ret.ToString());
			s_fullLog.WriteLine();

			s_fullLog.WriteLine("Input:");
			s_fullLog.WriteLine(input);
			s_fullLog.WriteLine();

			s_fullLog.WriteLine("Output:");
			s_fullLog.WriteLine(output);
			s_fullLog.WriteLine();

			s_fullLog.WriteLine("Error:");
			s_fullLog.WriteLine(error);
			s_fullLog.WriteLine();

			s_fullLog.Close();
#endif
			return ret;
		}

		/// <summary>
		/// Continuously copies data from one stream to the other.
		/// </summary>
		/// <param name="instream">The input stream.</param>
		/// <param name="outstream">The output stream.</param>
#if DEBUG
		private static void passThrough(Stream instream, Stream outstream, ref string log, ref TextWriter sw)
		{
#else
		private static void passThrough( Stream instream, Stream outstream ) {
#endif
			byte[] buffer = new byte[4096];
			while (true)
			{
				int len;
				while ((len = instream.Read(buffer, 0, buffer.Length)) > 0)
				{
					outstream.Write(buffer, 0, len);
					outstream.Flush();

#if DEBUG
					var s = Encoding.UTF8.GetString(buffer, 0, len);
					log += s;
					sw.Write(s);
#endif
				}

				// Prevent wrapper from consuming too much CPU Time
				Thread.Sleep(1);
			}
		}

		private static void outputReader(object p)
		{
			var process = (Process)p;
			// Pass the standard output of the child to our standard output
#if DEBUG
			passThrough(process.StandardOutput.BaseStream, Console.OpenStandardOutput(), ref output, ref s_outLog);
#else
			passThrough( process.StandardOutput.BaseStream, Console.OpenStandardOutput() );
#endif
		}

		private static void errorReader(object p)
		{
			var process = (Process)p;
			// Pass the standard error of the child to our standard error
#if DEBUG
			passThrough(process.StandardError.BaseStream, Console.OpenStandardError(), ref error, ref s_errorLog);
#else
			passThrough( process.StandardError.BaseStream, Console.OpenStandardError() );
#endif
		}

		private static void inputReader(object p)
		{
			var process = (Process)p;
			// Pass our standard input into the standard input of the child
#if DEBUG
			passThrough(Console.OpenStandardInput(), process.StandardInput.BaseStream, ref input, ref s_inLog);
#else
			passThrough( Console.OpenStandardInput(), process.StandardInput.BaseStream );
#endif
		}
	}
}
