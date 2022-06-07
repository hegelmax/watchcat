/*
Compilation:
	C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe /target:exe /out:C:\LEMEX\Watcher\watchcat_v1.3.exe C:\LEMEX\Watcher\watchcat_v1.3.cs /win32icon:C:\LEMEX\Watcher\eyes-on-speaker.ico /reference:C:\LEMEX\Watcher\Newtonsoft.Json.dll

Using example:
	watchcat.exe -p="C:\Windows" -w -c -s -l -f:*.csv -r="C:\WatchCat\run_SQL_job.bat"
	C:\LEMEX\Watcher\watchcat_v1.3.exe -p="D:\ZoneData_Import\final" -w -c -s -l -f:"*.csv" -r="C:\LEMEX\run_SQL_job\run_SQL_job.bat" -a="/changes:"
	
	Available options:
	  -h -help		- show this info
	  -s -subfolder	- scan all subfolders
	  -d -debug		- debug function
	  -c -console	- show changes in console
	  -l -logfile	- save all info in log file (in same folder if param 'log_file_name' is not set)
	  -w -wait		- wait for exit when running program

	Available params:
	  -p -path		="C:\Windows"						- Path for watching
	  -f -filter	="*.scv"							- Watch only this type of files
	  -l -logfile	="C:\Full path To\Log_file.log"		- Save all info in log file (Param is path to log file. If param is empty, then log file creates in same folder)
	  -e -events	=CUDR								- Set types of events, that should be handled (C - Created, U - Updated, D - Deleted, R - Renamed)
	  -r -runfile	="C:\Full path To\Your Script.exe"	- Path to file (execute a program, vb script or batch file), that need to be executed on event
	  -a -argument	="/changes:"						- Send info about event to program, vb script or batch with specified prefix (if param is set, but empty, send without prefix)
	  
	Keys can be called in two ways:
	  -h -help	- with sign -
	  /h /help	- with sign /
	
	Params can be set in two ways:
	  -e=CUDR	- with sign =
	  -e:CUDR	- with sign :
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
//using System.Web.Script.Serialization;

[assembly:AssemblyVersionAttribute("1.3")]
[assembly:AssemblyCompanyAttribute("LEMEX")]
[assembly:AssemblyCopyrightAttribute("Maxim Hegel")]
[assembly:AssemblyProductAttribute("WatchCat")]
[assembly:AssemblyTitleAttribute("WatchCat - Filesystem monitoring")]
[assembly:AssemblyDescriptionAttribute("Filesystem monitoring program")]

namespace LEMEX {
	
	public class Start {
		public static void Main() {
			WatchCat watcherInstance = new WatchCat();
			watcherInstance.Run();
		}
	}
	
	public class WatchCatObj {
		public string		version			= null;
		public string		start_datetime	= null;
		public int			process_id		= 0;
		
		public WatchCatInputParams		input_params;
		public WatchCatInputEventInfo	event_info;
	}
	
	public class WatchCatInputParams {
		public bool			help			= false;
		public bool			debug			= false;
		public bool			scan_subfolder	= false;
		public bool			log_console		= false;
		public bool			log_file		= false;
		public bool			run_bat			= false;
		public bool			run_vbs			= false;
		public bool			run_exe			= false;
		public bool			wait			= false;
		public bool			event_created	= false;
		public bool			event_updated	= false;
		public bool			event_deleted	= false;
		public bool			event_renamed	= false;
		public bool			send_argumnet	= false;
		
		public string		filter			= null;
		public string		directory_path	= null;
		public string		log_file_path	= null;
		public string		run_file_path	= null;
		public string		events			= null;
		public string		argument		= null;
	}
	
	public class WatchCatInputEventInfo {
		public string		datetime		= null;
		public string		change_type		= null;
		public string		full_path		= null;
		public string		old_full_path	= null;
		public string		file_checksum	= null;
	}
	
	public class WatchCat {
		
		public enum console_colors {
			 Default		= 0
			,Black			= 1
			,White			= 2
			,Gray			= 3
			,Red			= 4
			,Green			= 5
			,Blue			= 6
			,Cyan			= 7
			,Magenta		= 8
			,Yellow			= 9
			,DarkGray		= 10
			,DarkRed		= 11
			,DarkGreen		= 12
			,DarkBlue		= 13
			,DarkCyan		= 14
			,DarkMagenta	= 15
			,DarkYellow		= 16
		}
		
		// global variables
		public WatchCatObj				_obj				= new WatchCatObj{};
		public WatchCatInputParams		_wcip				= new WatchCatInputParams{};
		public WatchCatInputEventInfo	_info				= new WatchCatInputEventInfo{};
		public ConsoleColor				_original_color		= Console.ForegroundColor;
		public bool						_is_panding			= true;
		public bool						_process_is_running	= false;
		public Process					_proc;
		public DateTime					_start_watch;
		
		private string getVersion() {
			return typeof(WatchCat).Assembly.GetName().Version.ToString();
		}
		
		//Entry point
		//PermissionSet(SecurityAction.Demand, Name="FullTrust")
		public void Run() {
			_obj.start_datetime	= getNow();
			_obj.process_id		= Process.GetCurrentProcess().Id;
			_obj.version		= getVersion();
			_obj.input_params	= _wcip;
			_obj.event_info		= _info;
			
			if (!setArguments()) return;
			
			if (_wcip.help) {
				ShowMoreInfo();
				return;
			}
			
			if (String.IsNullOrEmpty(_wcip.directory_path)) {
				ShowError("Directory path is not set!");
				return;
			} else {
				if (!Directory.Exists(_wcip.directory_path)) {
					ShowError("Directory '" + _wcip.directory_path + "' is not exists or user doesn`t have permissions!");
					return;
				}
			}
			
			// Create a new FileSystemWatcher and set its properties.
			FileSystemWatcher watcher = new FileSystemWatcher();
			watcher.Path = _wcip.directory_path;
			
			// Watch for changes in LastAccess and LastWrite times, and the renaming of files or directories.
			watcher.NotifyFilter = NotifyFilters.LastAccess
								 | NotifyFilters.LastWrite
								 | NotifyFilters.FileName
								 | NotifyFilters.DirectoryName;
			
			// Only watch files with filter (default all files)
			watcher.Filter = _wcip.filter;
			
			// Scan subfolders (default do not scan)
			watcher.IncludeSubdirectories = _wcip.scan_subfolder;
			
			// Add event handlers.
			if (_wcip.event_created) watcher.Changed += new FileSystemEventHandler(OnChanged);
			if (_wcip.event_updated) watcher.Created += new FileSystemEventHandler(OnChanged);
			if (_wcip.event_deleted) watcher.Deleted += new FileSystemEventHandler(OnChanged);
			if (_wcip.event_renamed) watcher.Renamed += new RenamedEventHandler(OnRenamed);
			
			// Begin watching.
			watcher.EnableRaisingEvents = true;
			
			Watch();
		}
		
		// get Commandline Argument and sets variables
		private bool setArguments() {
			var arguments		= new Dictionary<string, string>();
			
			string[] args = System.Environment.GetCommandLineArgs();
			
			if (args.Length == 1) {
				ShowInfo();
				return false;
			}
			
			foreach (string argument in args) {
				string[] splitted = argument.Split('=');
				
				if (splitted.Length == 2) {
					arguments[splitted[0]] = splitted[1];
				} else {
					splitted = argument.Split(':');
					
					if (splitted.Length == 2) {
						arguments[splitted[0]] = splitted[1];
					}
				}
				
			// set options
				if (argument == "-h")			_wcip.help				= true;
				if (argument == "/h")			_wcip.help				= true;
				if (argument == "-help")		_wcip.help				= true;
				if (argument == "/help")		_wcip.help				= true;
				if (argument == "-s")			_wcip.scan_subfolder		= true;
				if (argument == "/s")			_wcip.scan_subfolder		= true;
				if (argument == "-subfolder")	_wcip.scan_subfolder		= true;
				if (argument == "/subfolder")	_wcip.scan_subfolder		= true;
				if (argument == "-d")			_wcip.debug				= true;
				if (argument == "/d")			_wcip.debug				= true;
				if (argument == "-debug")		_wcip.debug				= true;
				if (argument == "/debug")		_wcip.debug				= true;
				if (argument == "-c")			_wcip.log_console		= true;
				if (argument == "/c")			_wcip.log_console		= true;
				if (argument == "-console")		_wcip.log_console		= true;
				if (argument == "/console")		_wcip.log_console		= true;
				if (argument == "-l")			_wcip.log_file			= true;
				if (argument == "/l")			_wcip.log_file			= true;
				if (argument == "-logfile")		_wcip.log_file			= true;
				if (argument == "/logfile")		_wcip.log_file			= true;
				if (argument == "-w")			_wcip.wait				= true;
				if (argument == "/w")			_wcip.wait				= true;
				if (argument == "-wait")		_wcip.wait				= true;
				if (argument == "/wait")		_wcip.wait				= true;
				if (argument == "-a")			_wcip.send_argumnet		= true;
				if (argument == "/a")			_wcip.send_argumnet		= true;
				if (argument == "-argument")	_wcip.send_argumnet		= true;
				if (argument == "/argument")	_wcip.send_argumnet		= true;
			}
			
		// set named parameters
			if (arguments.ContainsKey("-p"))		arguments.TryGetValue("-p",			out _wcip.directory_path);
			if (arguments.ContainsKey("/p"))		arguments.TryGetValue("/p",			out _wcip.directory_path);
			if (arguments.ContainsKey("-path"))		arguments.TryGetValue("-path",		out _wcip.directory_path);
			if (arguments.ContainsKey("/path"))		arguments.TryGetValue("/path",		out _wcip.directory_path);
			if (arguments.ContainsKey("-f"))		arguments.TryGetValue("-f",			out _wcip.filter);
			if (arguments.ContainsKey("/f"))		arguments.TryGetValue("/f",			out _wcip.filter);
			if (arguments.ContainsKey("-filter"))	arguments.TryGetValue("-filter",	out _wcip.filter);
			if (arguments.ContainsKey("/filter"))	arguments.TryGetValue("/filter",	out _wcip.filter);
			if (arguments.ContainsKey("-r"))		arguments.TryGetValue("-r",			out _wcip.run_file_path);
			if (arguments.ContainsKey("/r"))		arguments.TryGetValue("/r",			out _wcip.run_file_path);
			if (arguments.ContainsKey("-runfile"))	arguments.TryGetValue("-runfile",	out _wcip.run_file_path);
			if (arguments.ContainsKey("/runfile"))	arguments.TryGetValue("/runfile",	out _wcip.run_file_path);
			if (arguments.ContainsKey("-e"))		arguments.TryGetValue("-e",			out _wcip.events);
			if (arguments.ContainsKey("/e"))		arguments.TryGetValue("/e",			out _wcip.events);
			if (arguments.ContainsKey("-events"))	arguments.TryGetValue("-events",	out _wcip.events);
			if (arguments.ContainsKey("/events"))	arguments.TryGetValue("/events",	out _wcip.events);
			if (arguments.ContainsKey("-l"))		arguments.TryGetValue("-l",			out _wcip.log_file_path);
			if (arguments.ContainsKey("/l"))		arguments.TryGetValue("/l",			out _wcip.log_file_path);
			if (arguments.ContainsKey("-logfile"))	arguments.TryGetValue("-logfile",	out _wcip.log_file_path);
			if (arguments.ContainsKey("/logfile"))	arguments.TryGetValue("/logfile",	out _wcip.log_file_path);
			if (arguments.ContainsKey("-a"))		arguments.TryGetValue("-a",			out _wcip.argument);
			if (arguments.ContainsKey("/a"))		arguments.TryGetValue("/a",			out _wcip.argument);
			if (arguments.ContainsKey("-argument"))	arguments.TryGetValue("-argument",	out _wcip.argument);
			if (arguments.ContainsKey("/argument"))	arguments.TryGetValue("/argument",	out _wcip.argument);
			
		// set Log FileName
			string log_file_directory;
			string log_file_name;
			string default_log_file_name = "WatchCat_" + _obj.start_datetime.Replace("-", "").Replace(":", "").Replace(" ", "").Replace("T", "") + "_" + _obj.process_id + ".log";
			if (!String.IsNullOrEmpty(_wcip.log_file_path)) { //if (arguments.TryGetValue("log_file_name", out _log_file_path)) {
				
				if (!File.Exists(_wcip.log_file_path) && !Directory.Exists(_wcip.log_file_path)) {
					ShowError("Log file or directory '" + _wcip.log_file_path + "' doesn`t exists!");
					return false;
				}
				
			//detect whether its a directory or file
				if (File.GetAttributes(_wcip.log_file_path).HasFlag(FileAttributes.Directory)) {
					log_file_directory	= _wcip.log_file_path;
					log_file_name		= default_log_file_name;
				} else {
					log_file_directory	= Path.GetDirectoryName(_wcip.log_file_path);
					log_file_name		= Path.GetFileName(_wcip.log_file_path);
				}
				
				if (!HasWritePermissionOnDir(log_file_directory)) {
					ShowError("User doesn`t have permissions to write log file in directory '" + log_file_directory + "'");
					return false;
				}
				
				_wcip.log_file_path	= log_file_directory + "\\" + log_file_name;
				_wcip.log_file		= true;
			} else {
				if (_wcip.log_file) {
					log_file_directory	= Directory.GetCurrentDirectory();
					log_file_name		= default_log_file_name;
					_wcip.log_file_path		= log_file_directory + "\\" + log_file_name;
				}
			}
			
			WriteLog("WatchCat ver " + getVersion() + " by Maxim Hegel");
			
		// set type of events to handle
			if (!String.IsNullOrEmpty(_wcip.events)) {
				if (_wcip.events.ToLower().Contains("c")) _wcip.event_created = true;
				if (_wcip.events.ToLower().Contains("u")) _wcip.event_updated = true;
				if (_wcip.events.ToLower().Contains("d")) _wcip.event_deleted = true;
				if (_wcip.events.ToLower().Contains("r")) _wcip.event_renamed = true;
			} else {
				_wcip.event_created = true;
				_wcip.event_updated = true;
				_wcip.event_deleted = true;
				_wcip.event_renamed = true;
			}
			
		// check event is selected
			if (!_wcip.event_created && !_wcip.event_updated && !_wcip.event_deleted && !_wcip.event_renamed) {
				ShowError("No one event wasn`t selected!");
				return false;
			}
			
		// set Run fileName
			if (!String.IsNullOrEmpty(_wcip.run_file_path)) {
				if (File.Exists(_wcip.run_file_path)) {
					if (Path.GetExtension(_wcip.run_file_path) == ".vbs")	_wcip.run_vbs	= true;
					if (Path.GetExtension(_wcip.run_file_path) == ".exe")	_wcip.run_exe	= true;
					if (Path.GetExtension(_wcip.run_file_path) == ".bat")	_wcip.run_bat	= true;
				} else {
					ShowError("File for execution '" + _wcip.run_file_path + "' doesn`t exists!");
					return false;
				}
			}
			
			if (String.IsNullOrEmpty(_wcip.argument)) {
				if (_wcip.send_argumnet) _wcip.argument = "";
			} else {
				_wcip.send_argumnet = true;
			}
			
		// save user options to log
			if (_wcip.debug) {
				Console.WriteLine("Debug: {0}",				_wcip.debug);
				Console.WriteLine("Help: {0}",				_wcip.help);
				Console.WriteLine("Scan Subfolder: {0}",	_wcip.scan_subfolder);
				Console.WriteLine("Console: {0}",			_wcip.log_console);
				Console.WriteLine("Log: {0}",				_wcip.log_file);
				Console.WriteLine("Run Batch: {0}",			_wcip.run_bat);
				Console.WriteLine("Run VB Script: {0}",		_wcip.run_vbs);
				Console.WriteLine("Run EXE: {0}",			_wcip.run_exe);
				Console.WriteLine("Wait for running: {0}",	_wcip.wait);
				Console.WriteLine("Event Created: {0}",		_wcip.event_created);
				Console.WriteLine("Event Updated: {0}",		_wcip.event_updated);
				Console.WriteLine("Event Deleted: {0}",		_wcip.event_deleted);
				Console.WriteLine("Event Renamed: {0}",		_wcip.event_renamed);
				Console.WriteLine("Run Send argument: {0}",	_wcip.send_argumnet);
				Console.WriteLine("Filter: {0}",			_wcip.filter);
				Console.WriteLine("Directory Path: {0}",	_wcip.directory_path);
				Console.WriteLine("Log FileName: {0}",		_wcip.log_file_path);
				Console.WriteLine("Run FileName: {0}",		_wcip.run_file_path);
				Console.WriteLine("Events List: {0}",		_wcip.events);
				Console.WriteLine("Argument: {0}",			_wcip.argument);
				
				foreach (KeyValuePair<string, string> argument in arguments) {
					//Console.WriteLine("Key: {0}, Value: {1}", argument.Key, argument.Value);
				}
			}
			
			//PropertyReader classProperties = new PropertyReader();
			//var x = classProperties.getProperties(typeof(WatchCat));
			
			if (_wcip.debug) return false;
			
			// all arguments is OK
			return true;
		}
		
		// Define the event handlers.
		private void OnChanged(object source, FileSystemEventArgs e) {
			//WatchCatInputEventInfo	info	= new WatchCatInputEventInfo{};
			_info.datetime		= getNow();
			_info.change_type	= e.ChangeType.ToString();
			_info.full_path		= e.FullPath;
			_info.old_full_path	= null;
			_info.file_checksum	= (e.ChangeType.ToString() == "Deleted" ? null : getFileChecksum(e.FullPath));
			
			SaveEventInfo();
		}
		
		private void OnRenamed(object source, RenamedEventArgs e) {
			//WatchCatInputEventInfo	info	= new WatchCatInputEventInfo{};
			_info.datetime		= getNow();
			_info.change_type	= "Renamed";
			_info.full_path		= e.FullPath;
			_info.old_full_path	= e.OldFullPath;
			_info.file_checksum	= getFileChecksum(e.FullPath);
			
			SaveEventInfo();
		}
		
		private void SaveEventInfo() {
			_is_panding = false;
			ConsoleWright("");
			
			string log_info		= _info.datetime + "; " + _info.change_type + "; " + (String.IsNullOrEmpty(_info.old_full_path) ? "" : _info.old_full_path + " => ") + _info.full_path + "; " + _info.file_checksum;
			string json_info	= getJson(_obj);//new WatchCatObj {input_params = _wcip, event_info = info});
			
			// Specify what is done when a file is renamed.
			// Specify what is done when a file is changed, created, or deleted.
			if (_info.change_type + "" == "Created") ShowConsole(log_info, console_colors.Gray);
			if (_info.change_type + "" == "Changed") ShowConsole(log_info, console_colors.DarkCyan);
			if (_info.change_type + "" == "Deleted") ShowConsole(log_info, console_colors.Magenta);
			if (_info.change_type + "" == "Renamed") ShowConsole(log_info, console_colors.Cyan);
			
			WriteLog(log_info);
			RunScript(json_info);
			RunBatch(json_info);
			
			_start_watch = DateTime.Now;
			_is_panding = true;
		}
		
		private void Watch() {
			ShowConsole("WatchCat is RUNNING! Press \"q\" to quit the watcher", console_colors.Green);
			WriteLog(getNow() + "; WatchCat is RUNNING!");
			
			// Wait for the user to quit the program.
			_start_watch = DateTime.Now;
			
			var buf			= new byte[2048];
			var inputStream	= Console.OpenStandardInput();
			int amtRead		= 0;
			inputStream.BeginRead(buf,0,buf.Length,ar=>{
				amtRead	= inputStream.EndRead(ar);
			},null);
			
			while (1==1) {
				ConsoleShowWaitTime();
				Thread.Sleep(1000);
				if (amtRead > 0) break;
			}
			WriteLog(getNow() + "; User quit the program");
		}
		
		private void ConsoleShowWaitTime(string wait_type = "Standby") {
			if (_wcip.log_console && (_is_panding || _process_is_running)) {
				ConsoleWright(getCosoleWaitMessage(wait_type));
			}
		}
		
		private string getCosoleWaitMessage(string wait_type){
			TimeSpan T = DateTime.Now - _start_watch;
			string S = ("00"+T.Seconds).Right(2);
			string M = ("00"+T.Minutes).Right(2);
			string H = ("00"+T.Hours).Right(2);
			double D = T.Days;
			return wait_type + " " + (D>0?D+" days ":"") + H+":"+M+":"+S +"...";
		}
		
		private void ShowConsole(string info, console_colors new_color = 0) {
			if (_wcip.log_console) {
				if (new_color != 0) Console.ForegroundColor = getColorByCode(new_color);
				Console.WriteLine(info);
				if (new_color != 0) Console.ForegroundColor = _original_color;
			}
		}
		
		private void ConsoleWright(string info, console_colors new_color = 0) {
			if (_wcip.log_console) {
				if (new_color != 0) Console.ForegroundColor = getColorByCode(new_color);
				ClearCurrentConsoleLine();
				Console.Write("\r{0}", info);
				if (new_color != 0) Console.ForegroundColor = _original_color;
			}
		}
		
		private void WriteLog(string info) {
			if (_wcip.log_file) {
				File.AppendAllText(_wcip.log_file_path, info + Environment.NewLine);
			}
		}
		
		private string getJson(object obj){
			StringBuilder sb = new StringBuilder();
			using (StringWriter sw = new StringWriter(sb))
			using (JsonTextWriter writer = new JsonTextWriter(sw))
			{
				writer.QuoteChar = '\'';
				JsonSerializer ser = new JsonSerializer();
				ser.Serialize(writer, obj);
			}
			return "{'watchcat':"+sb.ToString()+"}";
		}
		
		private string getFileChecksum(string filename){
			using (var md5 = MD5.Create()) {
				try {
					using (var stream = File.OpenRead(filename)) {
						var hash = md5.ComputeHash(stream);
						return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
					}
				}
				catch (IOException) {
					System.Threading.Thread.Sleep(1000);
					return getFileChecksum(filename);
				}
			}
		}
		
		private bool IsFileLocked(string filename) {
			try {
				using(FileStream stream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.None)) {
					stream.Close();
				}
			}
			catch (IOException) {
				//the file is unavailable because it is:
				//still being written to
				//or being processed by another thread
				//or does not exist (has already been processed)
				return true;
			}
			//file is not locked
			return false;
		}
		
		private void RunScript(string info) {
			if (_wcip.run_vbs) {
				try {
					ConsoleWright("Running Script...");
					_start_watch = DateTime.Now;
					
					string directoriyName	= Path.GetDirectoryName(_wcip.run_file_path);
					string fileName			= Path.GetFileName(_wcip.run_file_path);
					
					_proc = new Process();
					_proc.StartInfo.FileName = "cscript";
					_proc.StartInfo.WorkingDirectory = directoriyName;
					_proc.StartInfo.Arguments = "//B /Nologo " + fileName + (_wcip.send_argumnet ? " " + _wcip.argument + info : "");
					_proc.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
					
					StartProcessAndWait_v3();
				}
				catch (Exception ex) {
					Console.WriteLine(ex.StackTrace.ToString());
				}
				
				ConsoleWright("");
			}
		}
		
		private void RunBatch(string info) {
			if (_wcip.run_bat) {
				try {
					ConsoleWright("Running Batch...");
					_start_watch			= DateTime.Now;
					
					string directoryName	= Path.GetDirectoryName(_wcip.run_file_path);
					string fileName			= Path.GetFileName(_wcip.run_file_path);
					
					_proc								= new Process();
					_proc.StartInfo.WorkingDirectory	= directoryName;
					_proc.StartInfo.FileName			= fileName;
					if (_wcip.send_argumnet) _proc.StartInfo.Arguments = _wcip.argument + "\"" + info + "\"";
					_proc.StartInfo.CreateNoWindow		= false;
					
					StartProcessAndWait_v3();
				}
				catch (Exception ex) {
					Console.WriteLine(ex.StackTrace.ToString());
				}
				
				ConsoleWright("");
			}
		}
		
		private void ProcExitHandler(object sender, System.EventArgs e) {
			_process_is_running = false;
			_proc.Close();
			ShowConsole("Process Exit");
		}
		
		// Ver 1
		private void StartProcessAndWait_v1() {
			_proc.Exited += new EventHandler(ProcExitHandler);
			_proc.Start();
			
			if (_wcip.wait) {
				_process_is_running = true;
				
				while (_process_is_running) {
					ConsoleShowWaitTime("Script is running");
				}
			}
		}
		
		// Ver 2
		private void StartProcessAndWait_v2() {
			int proc_id;
			
			_proc.Start();
			
			if (_wcip.wait) {
				_process_is_running = true;
				
				while (_process_is_running) {
					ConsoleWright(getCosoleWaitMessage("Batch is running"));
					
					try {
						proc_id = _proc.Id;
					}
					catch {
						_process_is_running = false;
						_proc.Close();
					}
				}
			}
		}
		
		// Ver 3
		private void StartProcessAndWait_v3() {
			_proc.Start();
			
			if (_wcip.wait) {
				_process_is_running = true;
				
				_proc.WaitForExit();
				_proc.Close();
				
				_process_is_running = false;
			}
		}
		
		private void ShowError(string msg) {
			msg = "Error! "+msg;
			
			ShowConsole("\n"+msg, console_colors.Red);
			WriteLog(getNow() + "; " + msg);
			
			ShowInfo();
		}
		
		private string getNow() {
			return DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
		}
		
		private ConsoleColor getColorByCode(console_colors color) {
			ConsoleColor return_var;
			
			switch (color) {
				case console_colors.Black:			return_var = ConsoleColor.Black;		break;
				case console_colors.White:			return_var = ConsoleColor.White;		break;
				case console_colors.Gray:			return_var = ConsoleColor.Gray;			break;
				case console_colors.Red:			return_var = ConsoleColor.Red;			break;
				case console_colors.Green:			return_var = ConsoleColor.Green;		break;
				case console_colors.Blue:			return_var = ConsoleColor.Blue;			break;
				case console_colors.Cyan:			return_var = ConsoleColor.Cyan;			break;
				case console_colors.Magenta:		return_var = ConsoleColor.Magenta;		break;
				case console_colors.Yellow:			return_var = ConsoleColor.Yellow;		break;
				case console_colors.DarkGray:		return_var = ConsoleColor.DarkGray;		break;
				case console_colors.DarkRed:		return_var = ConsoleColor.DarkRed;		break;
				case console_colors.DarkGreen:		return_var = ConsoleColor.DarkGreen;	break;
				case console_colors.DarkBlue:		return_var = ConsoleColor.DarkBlue;		break;
				case console_colors.DarkCyan:		return_var = ConsoleColor.DarkCyan;		break;
				case console_colors.DarkMagenta:	return_var = ConsoleColor.DarkMagenta;	break;
				case console_colors.DarkYellow:		return_var = ConsoleColor.DarkYellow;	break;
				default:							return_var = _original_color;			break;
			}
			
			return return_var;
		}
		
		public static bool HasWritePermissionOnDir(string path) {
			var writeAllow			= false;
			var writeDeny			= false;
			var accessControlList	= Directory.GetAccessControl(path);
			
			if (accessControlList == null) return false;
			
			var accessRules = accessControlList.GetAccessRules(true, true, typeof(System.Security.Principal.SecurityIdentifier));
			
			if (accessRules == null) return false;
			
			foreach (FileSystemAccessRule rule in accessRules) {
				if ((FileSystemRights.Write & rule.FileSystemRights) != FileSystemRights.Write) 
					continue;
				
				if (rule.AccessControlType == AccessControlType.Allow)
					writeAllow = true;
				else if (rule.AccessControlType == AccessControlType.Deny)
					writeDeny = true;
			}
			
			return writeAllow && !writeDeny;
		}
		
		private void ShowInfo() {
			_wcip.log_console = true;
			ShowConsole("");
			ShowConsole("WatchCat ver " + getVersion() + " by Maxim Hegel", console_colors.Cyan);
			ShowConsole("");
			ShowConsole("Usage: watchcat.exe -path=\"C:\\Full Path To\\Watched Directory\" [options] [params]", console_colors.Gray);
			ShowConsole("Use '-h' for more Info about function: watchcat.exe -h", console_colors.Gray);
			ShowConsole("");
		}
		
		private void ShowMoreInfo() {
			ShowInfo();
			
			ShowConsole("");
			ShowConsole("Available options:", console_colors.Magenta);
			ShowConsole("  -h -help	- show this info");
			ShowConsole("  -s -subfolder	- scan all subfolders");
			ShowConsole("  -d -debug	- debug function");
			ShowConsole("  -c -console	- show changes in console");
			ShowConsole("  -w -wait	- wait for exit when running program");
			ShowConsole("");
			ShowConsole("");
			ShowConsole("Available params:", console_colors.Green);
			ShowConsole("  -p -path", console_colors.White);
			ShowConsole("      Path for watching", console_colors.Gray);
			ShowConsole("     -path=\"C:\\Windows\"", console_colors.DarkGray);
			ShowConsole("");
			ShowConsole("  -f -filter", console_colors.White);
			ShowConsole("      Watch only this type of files", console_colors.Gray);
			ShowConsole("     -filter=\"*.scv\"", console_colors.DarkGray);
			ShowConsole("");
			ShowConsole("  -l -logfile", console_colors.White);
			ShowConsole("      Save all info in log file (Param is path to log file. If param is empty, then log file creates in same folder)", console_colors.Gray);
			ShowConsole("     -logfile=\"C:\\Full path To\\Log_file.log\"", console_colors.DarkGray);
			ShowConsole("");
			ShowConsole("  -r -runfile", console_colors.White);
			ShowConsole("     Path to file (execute a program, vb script or batch file), that need to be executed on event", console_colors.Gray);
			ShowConsole("     -runfile=\"C:\\Full path To\\Your Script.exe\"", console_colors.DarkGray);
			ShowConsole("");
			ShowConsole("  -e -events", console_colors.White);
			ShowConsole("     Set types of events, that should be handled (C - Created, U - Updated, D - Deleted, R - Renamed)", console_colors.Gray);
			ShowConsole("     -events=CUDR", console_colors.DarkGray);
			ShowConsole("");
			ShowConsole("");
			ShowConsole("Additional features:", console_colors.Yellow);
			ShowConsole("Keys can be called in two ways:");
			ShowConsole("  -h -help	- with sign -");
			ShowConsole("  /h /help	- with sign /");
			ShowConsole("");
			ShowConsole("Params can be set in two ways:");
			ShowConsole("  -e=CUDR	- with sign =");
			ShowConsole("  -e:CUDR	- with sign :");
			ShowConsole("");
			/*
			ShowConsole("test", console_colors.Magenta		);
			ShowConsole("test", console_colors.Default		);
			ShowConsole("test", console_colors.Black		);
			ShowConsole("test", console_colors.White		);
			ShowConsole("test", console_colors.Gray			);
			ShowConsole("test", console_colors.Red			);
			ShowConsole("test", console_colors.Green		);
			ShowConsole("test", console_colors.Blue			);
			ShowConsole("test", console_colors.Cyan			);
			ShowConsole("test", console_colors.Magenta		);
			ShowConsole("test", console_colors.Yellow		);
			ShowConsole("test", console_colors.DarkGray		);
			ShowConsole("test", console_colors.DarkRed		);
			ShowConsole("test", console_colors.DarkGreen	);
			ShowConsole("test", console_colors.DarkBlue		);
			ShowConsole("test", console_colors.DarkCyan		);
			ShowConsole("test", console_colors.DarkMagenta	);
			ShowConsole("test", console_colors.DarkYellow	);
			*/
		}
		
		private static void ClearCurrentConsoleLine() {
			int currentLineCursor = Console.CursorTop;
			Console.SetCursorPosition(0, Console.CursorTop);
			Console.Write(new string(' ',Console.WindowWidth));
			Console.SetCursorPosition(0, currentLineCursor);
		}
	}
	
	static class Extensions {
		public static string Right(this string value, int lenth) {
			if (String.IsNullOrEmpty(value)) return string.Empty;
			
			return value.Length <= lenth ? value : value.Substring(value.Length - lenth);
		}
	}
	/*
	public class PropertyReader {
		//simple struct to store the type and name of variables
		public struct Variable {
			public string name;
			public Type type;
		}

		//for instances of classes that inherit PropertyReader
		private Variable[] _fields_cache;
		private Variable[] _props_cache;
		
		public Variable[] getFields() {
			if (_fields_cache == null)
				_fields_cache = getFields (this.GetType());
			
			return _fields_cache;
		}
		
		public Variable[] getProperties() {
			if (_props_cache == null)
				_props_cache = getProperties (this.GetType());
			
			return _props_cache;
		}

		//getters and setters for instance values that inherit PropertyReader
		public object getValue(string name) {
			return this.GetType().GetProperty(name).GetValue(this,null);
		}
		
		public void setValue(string name, object value) {
			this.GetType().GetProperty(name).SetValue(this,value,null);
		}

		//static functions that return all values of a given type
		public static Variable[] getFields(Type type) {
			var fieldValues = type.GetFields();
			var result = new Variable[fieldValues.Length];
			for (int i = 0; i < fieldValues.Length; i++) {
				result[i].name = fieldValues[i].Name;
				result[i].type = fieldValues[i].GetType();
			}
			
			return result;
		}
		
		public static Variable[] getProperties(Type type) {
			var propertyValues = type.GetProperties();
			var result = new Variable[propertyValues.Length];
			for (int i = 0; i < propertyValues.Length; i++) {
				result[i].name = propertyValues[i].Name;
				result[i].type = propertyValues[i].GetType();
			}
			
			return result;
		}
	}
	*/
}