#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Plugins.FirebasePlugin.Editor;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class ShellHelper 
{
	[MenuItem("Firebase Plugin/Open server in IntelliJ")]
	private static void LaunchIdeaExternally()
	{
		LaunchExternalFile(GetFilePath("idea"));	
	}
	
	[MenuItem("Firebase Plugin/Deploy local docker container")]
	private static async void DeployLocally()
	{
		DestroyLocal();

		bool validate = await Credentials.Validate();
            
		if (!validate)
			return;

		bool createSettings = Credentials.CreateServerProperties(true);

		if (!createSettings)
			return;
		
		Credentials credentials = Credentials.FindCredentialsAsset();

		string gcloudBashFilePath = GetFilePath("local_run");

		string arg1;
		string arg2;

#if UNITY_EDITOR_WIN
		arg1 = $"\"{Path.Combine(credentials.GoogleSDKPath, "google-cloud-sdk\\bin\\gcloud")}\"";
		arg2 = $"{credentials.ProjectId}";
#elif UNITY_EDITOR_OSX
		arg1 = $"{Path.Combine(credentials.GoogleSDKPath, "bin/gcloud")}";
		arg2 = $"{credentials.ProjectId}";
#endif

		ShellRequest req = LaunchExternalFile(gcloudBashFilePath, new[] {arg1, arg2});
	}

	[MenuItem("Firebase Plugin/Destroy all local docker containers")]
	private static async void DestroyLocal()
	{
		bool validate = await Credentials.Validate();
            
		if (!validate)
			return;
		
		Credentials credentials = Credentials.FindCredentialsAsset();
		
		string gcloudBashFilePath = GetFilePath("local_stop");

		string arg;

#if UNITY_EDITOR_WIN
		arg = $"\"{Path.Combine(credentials.GoogleSDKPath, "google-cloud-sdk\\bin\\gcloud")}\"";
#elif UNITY_EDITOR_OSX
		arg = $"{Path.Combine(credentials.GoogleSDKPath, "bin/gcloud")}";
#endif

		ShellRequest req = LaunchExternalFile(gcloudBashFilePath, new[] {arg});
        
	}


	[MenuItem("Firebase Plugin/Deploy docker container to server")]
	private static async void Deploy()
	{
		bool validate = await Credentials.Validate();
            
		if (!validate)
			return;

		bool createSettings = Credentials.CreateServerProperties(false);

		if (!createSettings)
			return;
		
		Credentials credentials = Credentials.FindCredentialsAsset();
		
		string gcloudBashFilePath = GetFilePath("gcloud_run");

		string arg1;
		string arg2;

#if UNITY_EDITOR_WIN
		arg1 = $"\"{Path.Combine(credentials.GoogleSDKPath, "google-cloud-sdk\\bin\\gcloud")}\"";
		arg2 = $"{credentials.ProjectId}";
#elif UNITY_EDITOR_OSX
		arg1 = $"{Path.Combine(credentials.GoogleSDKPath, "bin/gcloud")}";
		arg2 = $"{credentials.ProjectId}";
#endif

		LaunchExternalFile(gcloudBashFilePath, new[] {arg1, arg2});
	}

	private static ShellRequest LaunchExternalFile(string filePath, string[] args = null)
	{
		string cmd = "";

		if (args is null)
			args = new string[0];
		
#if UNITY_EDITOR_WIN
		cmd = $"{filePath} {string.Join(" ", args)}";
#elif UNITY_EDITOR_OSX
		cmd = $"sh {filePath} {string.Join(" ", args)}";
#endif
		ShellRequest req = ProcessCMD(cmd, ".");

		req.onLog += debug;

		return req;
	}

	private static string GetFilePath(string name)
	{
#if UNITY_EDITOR_WIN
		name = $"{name}.cmd";
#elif UNITY_EDITOR_OSX
		name = $"{name}.bash";
#endif
		return Directory.GetFiles(".", name, SearchOption.AllDirectories).First();
	}
	

	private static void debug(int code, string output)
	{
		

		if (output.Contains("ERROR"))
		{
			Debug.LogError(output);
		}
		else
		{
			Debug.Log(output);
		}
	}
	
	public class ShellRequest
	{
		public int procID;
		public event System.Action<int, string> onLog;
		public event System.Action onError;
		public event System.Action onDone;

		public void Log(int type, string log)
		{
			if (onLog != null)
			{
				onLog(type, log);
			}
		}

		public void NotifyDone()
		{
			if (onDone != null)
			{
				onDone();
			}
		}

		public void Error()
		{
			if (onError != null)
			{
				onError();
			}
		}
	}


	private static string shellApp
	{
		get
		{
#if UNITY_EDITOR_WIN
			string app = "cmd.exe";
#elif UNITY_EDITOR_OSX
			string app = "bash";
#endif
			return app;
		}
	}


	private static List<System.Action> _queue = new List<System.Action>();


	static ShellHelper()
	{
		_queue = new List<System.Action>();
		EditorApplication.update += OnUpdate;
	}

	private static void OnUpdate()
	{
		for (int i = 0; i < _queue.Count; i++)
		{
			try
			{
				var action = _queue[i];
				if (action != null)
				{
					action();
				}
			}
			catch (System.Exception e)
			{
				UnityEngine.Debug.LogException(e);
			}
		}

		_queue.Clear();
	}



	public static ShellRequest ProcessCommand(string cmd, string workDirectory, List<string> environmentVars = null)
	{
		ShellRequest req = new ShellRequest();
		System.Threading.ThreadPool.QueueUserWorkItem(delegate(object state)
		{
			Process p = null;
			try
			{
				ProcessStartInfo start = new ProcessStartInfo(shellApp);

#if UNITY_EDITOR_OSX
				string splitChar = ":";
				start.Arguments = "-c";
#elif UNITY_EDITOR_WIN
				string splitChar = ";";
				start.Arguments = "/c";
#endif

				if (environmentVars != null)
				{
					foreach (string var in environmentVars)
					{
						start.EnvironmentVariables["PATH"] += (splitChar + var);
					}
				}

				start.Arguments += (" \"" + cmd + " \"");
				start.LoadUserProfile = true;
				start.CreateNoWindow = true;
				start.ErrorDialog = true;
				start.UseShellExecute = false;
				start.WorkingDirectory = workDirectory;

				if (start.UseShellExecute)
				{
					start.RedirectStandardOutput = false;
					start.RedirectStandardError = false;
					start.RedirectStandardInput = false;
				}
				else
				{
					start.RedirectStandardOutput = true;
					start.RedirectStandardError = true;
					start.RedirectStandardInput = true;
					start.StandardOutputEncoding = System.Text.UTF8Encoding.UTF8;
					start.StandardErrorEncoding = System.Text.UTF8Encoding.UTF8;
				}

				p = Process.Start(start);

				req.procID = p.Id;

				p.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
				{
					UnityEngine.Debug.LogError(e.Data);
				};
				p.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
				{
					UnityEngine.Debug.LogError(e.Data);
				};
				p.Exited += delegate(object sender, System.EventArgs e) { UnityEngine.Debug.LogError(e.ToString()); };

				bool hasError = false;
				do
				{
					string line = p.StandardOutput.ReadLine();
					if (line == null)
					{
						break;
					}

					line = line.Replace("\\", "/");

					_queue.Add(delegate() { req.Log(0, line); });

				} while (true);

				while (true)
				{
					string error = p.StandardError.ReadLine();
					if (string.IsNullOrEmpty(error))
					{
						break;
					}

					hasError = true;
					_queue.Add(delegate() { req.Log(1, error); });
				}

				p.Close();
				if (hasError)
				{
					_queue.Add(delegate() { req.Error(); });
				}
				else
				{
					_queue.Add(delegate() { req.NotifyDone(); });
				}


			}
			catch (System.Exception e)
			{
				UnityEngine.Debug.LogException(e);
				if (p != null)
				{
					p.Close();
				}
			}
		});
		return req;
	}


	private static List<string> _enviroumentVars = new List<string>();

	public static void AddEnvironmentVars(params string[] vars)
	{
		for (int i = 0; i < vars.Length; i++)
		{
			if (vars[i] == null)
			{
				continue;
			}

			if (string.IsNullOrEmpty(vars[i].Trim()))
			{
				continue;
			}

			_enviroumentVars.Add(vars[i]);
		}
	}

	public static ShellRequest ProcessCMD(string cmd, string workDir)
	{
		return ShellHelper.ProcessCommand(cmd, workDir, _enviroumentVars);
	}
}
#endif