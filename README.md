# WatchCat

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
