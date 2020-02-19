#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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

	[MenuItem("Firebase Plugin/Deploy development docker container locally")]
	private static async Task DeployDevelopmentLocally()
	{
		await Deploy(new ServerProperties {IsLocal = true, IsProduction = false}, LOCAL_RUN_FILENAME);
	}
	
	
	[MenuItem("Firebase Plugin/Deploy production docker container to gcloud")]
	private static async Task DeployProductionGcloud()
	{
		await Deploy(new ServerProperties {IsLocal = false, IsProduction = true}, GCLOUD_RUN_FILENAME);
	}
	
	[MenuItem("Firebase Plugin/Deploy development docker container to gcloud")]
	private static async Task DeployDevelopmentGcloud()
	{
		await Deploy(new ServerProperties {IsLocal = false, IsProduction = false}, GCLOUD_RUN_FILENAME);
	}
	
	private static async Task Deploy(ServerProperties properties, string filename)
	{
//		bool validate = await CredentialList.Validate(properties);
//            
//		if (!validate)
//			return;
//
//		bool createSettings = CredentialList.CreateServerPropertiesFile(properties);
//
//		if (!createSettings)
//			return;
//		
		CredentialList credentialList = CredentialList.FindCredentialsAsset();
//
//		string arg1;
//		string arg2;
//
//#if UNITY_EDITOR_WIN
//		arg1 = $"\"{Path.Combine(credentials.GoogleSDKPath, "google-cloud-sdk\\bin\\gcloud")}\"";
//		arg2 = $"{credentials.ProjectId}";
//#elif UNITY_EDITOR_OSX
//		arg1 = $"{Path.Combine(credentialList.GoogleSDKPath, "bin/gcloud")}";
//		arg2 = $"{CredentialList.GetProjectId(properties, credentialList)}";
//#endif

		//***
		//This is commented cause after Production/Development environment implementation
		//it was not tested properly
		//***
		//LaunchExternalFile(GetFilePath(filename), new[] {arg1, arg2});
		//***
		//Instead of executing commands below directly in terminal
		//we are simply printing them out to Unity console
		//***
		PrintLaunchCmdFor(CredentialList.GetProjectId(properties, credentialList));
		
	}

	[MenuItem("Firebase Plugin/Destroy all local docker containers")]
	private static async Task DestroyAll()
	{
		await Destroy(new ServerProperties {IsLocal = true, IsProduction = false});
		await Destroy(new ServerProperties {IsLocal = true, IsProduction = true});
	}

	private static async Task Destroy(ServerProperties properties)
	{
		bool validate = await CredentialList.Validate(properties);
            
		if (!validate)
			return;
		
		CredentialList credentialList = CredentialList.FindCredentialsAsset();
		
		string gcloudBashFilePath = GetFilePath("local_stop");

		string arg;

#if UNITY_EDITOR_WIN
		arg = $"\"{Path.Combine(credentialList.GoogleSDKPath, "google-cloud-sdk\\bin\\gcloud")}\"";
#elif UNITY_EDITOR_OSX
		arg = $"{Path.Combine(credentialList.GoogleSDKPath, "bin/gcloud")}";
#endif

		ShellRequest req = LaunchExternalFile(gcloudBashFilePath, new[] {arg});
	}

	private static void PrintLaunchCmdFor(string projectId)
	{
		Debug.Log("Execute following commands in server directory. Terminal (macOS) or gcloud console (windows):");
		
		Debug.LogError("Don't forget to change 'config.properties' file in server's src/main/resources directory");

		Debug.Log($@"
			Commands are inside:
			----------------------------
gcloud init
gcloud builds submit --tag gcr.io/{projectId}/server
gcloud beta run deploy --memory=512Mi --image gcr.io/{projectId}/server --platform managed --region europe-west1 --allow-unauthenticated server
			----------------------------"
		);
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

	private const string GCLOUD_RUN_FILENAME = "gcloud_run";
	private const string LOCAL_RUN_FILENAME = "local_run";
}
#endif
