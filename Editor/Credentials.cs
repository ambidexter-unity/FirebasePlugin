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
    
    public class Credentials : ScriptableObject
    {
        private const string GOOGLE_CREDENTIAL_VAR_NAME = "GOOGLE_APPLICATION_CREDENTIALS";

        private const string JSON_FILENAME = "tmp.json";
        
#pragma warning disable 649
        [JsonProperty]
        [Rename("Type"), SerializeField]
        private string type;
        
        [JsonProperty]
        [Rename("Project ID"), SerializeField]
        private string project_id;
        
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
        
        [MenuItem("Firebase Plugin/Connect to Firestore")]
        private static async void TestFirestoreConnection()
        {
            bool validate = await Validate();
            
            if (!validate)
                return;

            Debug.Log("Successfully connected to Firebase Firestore");
        }
        
        public static async Task<bool> Validate()
        {
            if (!ValidateCredentialsAssetExist())
                return false;
            
            if (!ValidateCredentialsNotEmpty())
                return false;
            
            if (!ValidateJsonFileExist())
                return false;

            if (!ValidateEnvironmentVariableIsSet())
                return false;

            bool conn = await ValidateConnectionToFirestore();
            
            if (!conn)
                return false;

            return true;

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
            Credentials credentials = FindCredentialsAsset();
            
            foreach(FieldInfo fi in credentials.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if(fi.FieldType == typeof(string))
                {
                    string value = (string)fi.GetValue(credentials);
                    if(string.IsNullOrEmpty(value))
                    {
                        Debug.LogError($"'FirebasePlugin/CredentialsSettings' {fi.Name} is empty");
                        return false;
                    }
                }
            }
            return true;
        }

        private static bool ValidateJsonFileExist()
        {
            Credentials credentials = FindCredentialsAsset();
            
            string credentialsJson = JsonConvert.SerializeObject(credentials, Formatting.Indented,
                new JsonSerializerSettings { ContractResolver = new ShouldSerializeContractResolver() });

            var filePathTmp = Path.Combine(Application.temporaryCachePath, JSON_FILENAME);
            var filePathServer = Path.Combine(Application.dataPath, "Server~/src/main/resources", JSON_FILENAME);

            try
            {
                File.WriteAllText(filePathTmp, credentialsJson.Replace("\\\\", "\\"));
            }
            catch (Exception e)
            {
                Debug.LogError($"Could not write file to {filePathTmp}. ");
                Debug.LogError(e.Message);
                return false;
            }

            try
            {
                File.WriteAllText(filePathServer, credentialsJson.Replace("\\\\", "\\"));
            }
            catch (Exception e)
            {
                Debug.LogError($"Could not write file to {filePathServer}. ");
                Debug.LogError(e.Message);
                return false;
            }

            return true;
        }
        
        private static bool ValidateEnvironmentVariableIsSet()
        {
            Environment.SetEnvironmentVariable(GOOGLE_CREDENTIAL_VAR_NAME, Path.Combine(Application.temporaryCachePath, JSON_FILENAME));

            if (String.IsNullOrEmpty(Environment.GetEnvironmentVariable(GOOGLE_CREDENTIAL_VAR_NAME)))
            {
                Debug.LogError($"Env. var {GOOGLE_CREDENTIAL_VAR_NAME} could not be found");
                return false;
            }
            return true;
        }
        
        private static async Task<bool> ValidateConnectionToFirestore()
        {
            string collectionName = "Test_firestore_collection";
            string docName = "Test_firestore_document";
            Credentials credentials = FindCredentialsAsset();

            FirestoreDb db = FirestoreDb.Create(credentials.project_id);

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

        public static Credentials FindCredentialsAsset()
        {
            Credentials instance = null;
            AssetDatabase.FindAssets($"t:{typeof(Credentials).FullName}").Any(guid =>
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                instance = AssetDatabase.LoadAssetAtPath<Credentials>(path);
                return true;
                
            });
            return instance;
        }
        
        private static Credentials FindOrCreateNewScriptableObject()
        {
            Credentials instance = FindCredentialsAsset();

            if (instance != null)
                return instance;

            instance = ScriptableObject.CreateInstance<Credentials>();

            if (!System.IO.Directory.Exists(ManagerPath))
                System.IO.Directory.CreateDirectory(ManagerPath);
			
            AssetDatabase.CreateAsset(instance, $"{ManagerPath}/{typeof(Credentials).Name}.asset");
            AssetDatabase.SaveAssets();

            return instance;
        }
    }
}
#endif