/****************************************************************
 * Lots of code borrowed from https://stackoverflow.com/a/30517342/36965
 */

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Text;

namespace git_wrapper {
	class Program {
		private static string FixPath( string s ) {
			var t = s;
			var p = "";
			if( s.Contains( '=' ) ) {
				var idx = s.LastIndexOf( '=' );
				t = s.Substring( idx+1 );
				p = s.Substring( 0, idx );
			}

			if( !t.Contains( Path.DirectorySeparatorChar ) )
				return s;

			if( Path.IsPathRooted( t ) ) {
				t = "/mnt/" + t[0].ToString().ToLower() + t.Substring( 2 );
			}

			return p + t.Replace( '\\', '/' );
		}

#if DEBUG
		static string input = "";
		static string output = "";
		static string error = "";
#endif

		static int Main( string[] args ) {
#if DEBUG
			var log = new StreamWriter( @"D:\Projects\Personal\Utils\git-wrapper\git-wrapper\bin\Debug\log.txt", true );
			log.AutoFlush = true;
			log.WriteLine( "\n-------------------------------------------------------------------------------------------------" );
			log.Write( "Args: " );
#endif
			var sa = "";
			foreach( var a in args ) {
				var sap = FixPath(a);

				if( sap.IndexOf( ' ' ) > 0 )
					sap = '"' + sap + '"';

				sa += " " + sap;
			}
#if DEBUG
			log.WriteLine( sa );
#endif
			var exepath =  Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.System ), "wsl.exe");
#if DEBUG
			log.WriteLine( "Working Directory: " + Environment.CurrentDirectory );
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
			//p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
			p.StartInfo.WorkingDirectory = Environment.CurrentDirectory;

			p.EnableRaisingEvents = true;

			// Depending on your application you may either prioritize the IO or the exact opposite
			const ThreadPriority ioPriority = ThreadPriority.Normal;
			var outputThread = new Thread( outputReader ) { Name = "ChildIO Output", Priority = ioPriority };
			var errorThread = new Thread( errorReader ) { Name = "ChildIO Error", Priority = ioPriority };
			var inputThread = new Thread( inputReader ) { Name = "ChildIO Input", Priority = ioPriority };

			// Set as background threads (will automatically stop when application ends)
			outputThread.IsBackground = errorThread.IsBackground = inputThread.IsBackground = true;

			// Signal to end the application
			ManualResetEvent stopApp = new ManualResetEvent( false );

			p.Exited += ( e, sender ) => { stopApp.Set(); };

			p.Start();

			// Start the IO threads
			outputThread.Start( p );
			errorThread.Start( p );
			inputThread.Start( p );

			// Wait for the child app to stop
			stopApp.WaitOne();

			p.WaitForExit();

			//var ret = ((1 == p.ExitCode) ? 0 : ((0 == p.ExitCode) ? 1 : p.ExitCode));
			var ret = p.ExitCode;
#if DEBUG
			log.WriteLine( "Return: " + ret.ToString() );
			log.WriteLine();

			log.WriteLine( "Input:" );
			log.WriteLine( input );
			log.WriteLine();

			log.WriteLine("Output:");
			log.WriteLine( output );
			log.WriteLine();

			log.WriteLine( "Error:" );
			log.WriteLine( error );
			log.WriteLine();

			log.Close();
#endif
			return ret;
		}

		/// <summary>
		/// Continuously copies data from one stream to the other.
		/// </summary>
		/// <param name="instream">The input stream.</param>
		/// <param name="outstream">The output stream.</param>
#if DEBUG
		private static void passThrough( Stream instream, Stream outstream, ref string log ) {
#else
		private static void passThrough( Stream instream, Stream outstream ) {
#endif
			byte[] buffer = new byte[4096];
			while( true ) {
				int len;
				while( (len = instream.Read( buffer, 0, buffer.Length )) > 0 ) {
#if DEBUG
					log += Encoding.UTF8.GetString( buffer, 0, len );
#endif
					outstream.Write( buffer, 0, len );
					outstream.Flush();
				}
			}
		}

		private static void outputReader( object p ) {
			var process = (Process)p;
			// Pass the standard output of the child to our standard output
#if DEBUG
			passThrough( process.StandardOutput.BaseStream, Console.OpenStandardOutput(), ref output );
#else
			passThrough( process.StandardOutput.BaseStream, Console.OpenStandardOutput() );
#endif
		}

		private static void errorReader( object p ) {
			var process = (Process)p;
			// Pass the standard error of the child to our standard error
#if DEBUG
			passThrough( process.StandardError.BaseStream, Console.OpenStandardError(), ref error );
#else
			passThrough( process.StandardError.BaseStream, Console.OpenStandardError() );
#endif
		}

		private static void inputReader( object p ) {
			var process = (Process)p;
			// Pass our standard input into the standard input of the child
#if DEBUG
			passThrough( Console.OpenStandardInput(), process.StandardInput.BaseStream, ref input );
#else
			passThrough( Console.OpenStandardInput(), process.StandardInput.BaseStream );
#endif
		}
	}
}
