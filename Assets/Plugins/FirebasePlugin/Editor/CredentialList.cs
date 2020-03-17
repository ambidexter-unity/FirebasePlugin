
using Google.Cloud.Firestore.V1;
using Object = System.Object;
#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Google.Cloud.Firestore;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEditor;
using UnityEngine;
using Application = UnityEngine.Application;

namespace Plugins.FirebasePlugin.Editor
{   
    public class ShouldSerializeContractResolver : DefaultContractResolver
    {
        public new static readonly ShouldSerializeContractResolver Instance = new ShouldSerializeContractResolver();

        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            JsonProperty property = base.CreateProperty(member, memberSerialization);

            if (property.PropertyName == "hideFlags" 
                || property.PropertyName == "name" 
                || property.PropertyName == "GoogleSDKPath"
                )
            {
                property.Ignored = true;
            }

            return property;
        }
    }

    [Serializable]
    public struct Credentials
    {
        [JsonProperty]
        [Rename("Type"), SerializeField]
        private string type;
        
        [JsonProperty]
        [Rename("Project ID"), SerializeField]
        private string project_id;

        public string ProjectId => project_id;
        
        [JsonProperty]
        [Rename("Private key ID"), SerializeField]
        private string private_key_id;
        
        [JsonProperty]
        [Rename("Private key"), SerializeField]
        private string private_key;
            
        [JsonProperty]
        [Rename("Client Email"), SerializeField]
        private string client_email;
            
        [JsonProperty]
        [Rename("Client ID"), SerializeField]
        private string client_id;
            
        [JsonProperty]
        [Rename("Auth URI"), SerializeField]
        private string auth_uri;
            
        [JsonProperty]
        [Rename("Token URI"), SerializeField]
        private string token_uri;
            
        [JsonProperty]
        [Rename("Auth Provider URL"), SerializeField] 
        private string auth_provider_x509_cert_url;
            
        [JsonProperty]
        [Rename("Client Cert URL"), SerializeField]
        private string client_x509_cert_url;
    }

    public class ServerProperties
    {
        public bool IsLocal { get; set; }
        
        public bool IsProduction { get; set; }
    }
    
    [Serializable]
    public class CredentialList : ScriptableObject
    {
        private const string GOOGLE_CREDENTIAL_VAR_NAME = "GOOGLE_APPLICATION_CREDENTIALS";

        private const string JSON_FILENAME_PROD = "tmp_prod.json";
        private const string JSON_FILENAME_DEV = "tmp_dev.json";
        private const string SERVER_SETTINGS_FILENAME = "config.properties";
        
#pragma warning disable 649

        [SerializeField]
        private Credentials prodCredentials;
        [SerializeField]
        private Credentials devCredentials;
            
        [Rename("Google Cloud SDK path"), SerializeField]
        private string googl_cloud_sdk;

        public string GoogleSDKPath => googl_cloud_sdk;
        
#pragma warning restore 649

        private const string ManagerPath = "Assets/Scripts/Common/Manager";

        [MenuItem("Firebase Plugin/Credentials Settings")]
        private static void GetAndSelectSettingsInstance()
        {
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = FindOrCreateNewScriptableObject();
        }
        
        [MenuItem("Firebase Plugin/Connect to Production Firestore")]
        private static async void TestFirestoreProdConnection()
        {
            bool validate = await Validate(new ServerProperties{IsLocal = false, IsProduction = true});
            
            if (!validate)
                return;

            Debug.Log("Successfully connected to Firebase Firestore");
        }
        
        [MenuItem("Firebase Plugin/Connect to Development Firestore")]
        private static async void TestFirestoreDevConnection()
        {
            bool validate = await Validate(new ServerProperties{IsLocal = false, IsProduction = false});
            
            if (!validate)
                return;

            Debug.Log("Successfully connected to Firebase Firestore");
        }
        
        public static async Task<bool> Validate(ServerProperties properties)
        {
            if (!ValidateCredentialsAssetExist())
                return false;
            
            if (!ValidateCredentialsNotEmpty())
                return false;
            
            if (!ValidateJsonFileExist(properties))
                return false;

            if (!ValidateEnvironmentVariableIsSet(properties))
                return false;

            bool conn = await ValidateConnectionToFirestore(properties);
            
            if (!conn)
                return false;

            return true;

        }

        public static bool CreateServerPropertiesFile(ServerProperties properties)
        {
            CredentialList credentialList = FindCredentialsAsset();

            Credentials credentials =
                properties.IsProduction ? credentialList.prodCredentials : credentialList.devCredentials;
            
            string credentialsJson = JsonConvert.SerializeObject(credentials, Formatting.Indented,
                new JsonSerializerSettings { ContractResolver = new ShouldSerializeContractResolver() });

            string serverSettings = $"host.local={properties.IsLocal}{System.Environment.NewLine}" +
                                    $"host.production={properties.IsProduction}{System.Environment.NewLine}" +
                                    $"host.prod.credentials={JSON_FILENAME_PROD}{System.Environment.NewLine}" +
                                    $"host.prod.projectId={GetProjectId(true, credentialList)}{System.Environment.NewLine}" + 
                                    $"host.dev.credentials={JSON_FILENAME_DEV}{System.Environment.NewLine}" +
                                    $"host.dev.projectId={GetProjectId(false, credentialList)}";

            var filePathServerCredentials = Path.Combine(Application.dataPath, "Server~/src/main/resources", GetJsonFilename(properties));
            var filePathServerSettings = Path.Combine(Application.dataPath, "Server~/src/main/resources", SERVER_SETTINGS_FILENAME);

            try
            {
                File.WriteAllText(filePathServerCredentials, credentialsJson.Replace("\\\\", "\\"));
                File.WriteAllText(filePathServerSettings, serverSettings);
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
                return false;
            }

            return true;
        }

        private static string GetJsonFilename(ServerProperties properties)
        {
            return properties.IsProduction ? JSON_FILENAME_PROD : JSON_FILENAME_DEV;
        }
        
        public static string GetProjectId(ServerProperties properties, CredentialList credentialList)
        {
            return GetProjectId(properties.IsProduction, credentialList);
        }

        private static string GetProjectId(bool isProd, CredentialList credentialList)
        {
            return isProd ? credentialList.prodCredentials.ProjectId : credentialList.devCredentials.ProjectId;
        }
        private static bool ValidateCredentialsAssetExist()
        {
            bool credentialsExist = FindCredentialsAsset() != null;
            if (!credentialsExist)
                Debug.LogError("'FirebasePlugin/CredentialsSettings' are empty");
                    
            return credentialsExist;

        }
        
        private static bool ValidateCredentialsNotEmpty()
        {
            CredentialList credentialList = FindCredentialsAsset();
            
            foreach(FieldInfo fi in credentialList.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if(fi.FieldType == typeof(string))
                {
                    string value = (string)fi.GetValue(credentialList);
                    if(string.IsNullOrEmpty(value))
                    {
                        Debug.LogError($"'FirebasePlugin/CredentialsSettings' {fi.Name} is empty");
                        return false;
                    }
                }
            }
            return true;
        }

        private static bool ValidateJsonFileExist(ServerProperties properties)
        {
            CredentialList credentialList = FindCredentialsAsset();
            
            Credentials credentials =
                properties.IsProduction ? credentialList.prodCredentials : credentialList.devCredentials;
            
            string credentialsJson = JsonConvert.SerializeObject(credentials, Formatting.Indented,
                new JsonSerializerSettings { ContractResolver = new ShouldSerializeContractResolver() });

            var filePathTmp = Path.Combine(Application.temporaryCachePath, GetJsonFilename(properties));

            try
            {
                File.WriteAllText(filePathTmp, credentialsJson.Replace("\\\\", "\\"));
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
                return false;
            }

            return true;
        }
        
        private static bool ValidateEnvironmentVariableIsSet(ServerProperties properties)
        {
            Environment.SetEnvironmentVariable(GOOGLE_CREDENTIAL_VAR_NAME, Path.Combine(Application.temporaryCachePath, GetJsonFilename(properties)));

            if (String.IsNullOrEmpty(Environment.GetEnvironmentVariable(GOOGLE_CREDENTIAL_VAR_NAME)))
            {
                Debug.LogError($"Env. var {GOOGLE_CREDENTIAL_VAR_NAME} could not be found");
                return false;
            }  
            return true;
        }
        
        private static async Task<bool> ValidateConnectionToFirestore(ServerProperties properties)
        {
            string collectionName = "Test_firestore_collection";
            string docName = "Test_firestore_document";
            CredentialList credentialList = FindCredentialsAsset();

            FirestoreDb db = FirestoreDb.Create(GetProjectId(properties, credentialList));
            
            // Create a document with a random ID in the "Test_firestore_collection" collection.
            CollectionReference collection = db.Collection(collectionName);

            DocumentReference document =
                await collection.AddAsync(new {Name = docName});

            // A DocumentReference doesn't contain the data - it's just a path.
            // Let's fetch the current document.
            DocumentSnapshot snapshot = await document.GetSnapshotAsync();

            string receivedDocName = snapshot.GetValue<string>("Name");

            if (receivedDocName != docName)
            {
                await document.DeleteAsync();
                Debug.LogError($"Could not write a test document to firebase");
                return false;
            }
            
            await document.DeleteAsync();
            
            return true;
        }

        public static CredentialList FindCredentialsAsset()
        {
            CredentialList instance = null;
            AssetDatabase.FindAssets($"t:{typeof(CredentialList).FullName}").Any(guid =>
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                instance = AssetDatabase.LoadAssetAtPath<CredentialList>(path);
                return true;
                
            });
            return instance;
        }
        
        private static CredentialList FindOrCreateNewScriptableObject()
        {
            CredentialList instance = FindCredentialsAsset();

            if (instance != null)
                return instance;

            instance = ScriptableObject.CreateInstance<CredentialList>();

            if (!System.IO.Directory.Exists(ManagerPath))
                System.IO.Directory.CreateDirectory(ManagerPath);
            
            AssetDatabase.CreateAsset(instance, $"{ManagerPath}/{typeof(CredentialList).Name}.asset");
            AssetDatabase.SaveAssets();

            return instance;
        }
    }
}
#endif