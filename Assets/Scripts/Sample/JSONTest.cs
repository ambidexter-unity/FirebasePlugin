using Google.Cloud.Firestore;
using Newtonsoft.Json;
using UnityEngine;

public class JSONTest : MonoBehaviour
{
    class Enemy
    {
        public string Name { get; set; }
        public int AttackDamage { get; set; }
        public int MaxHealth { get; set; }
    }
    private async void Start()
    {
        string json = @"{
            'Name': 'Ninja',
            'AttackDamage': '40'
            }";

        var enemy = JsonConvert.DeserializeObject<Enemy>(json);

        Debug.Log($"{enemy.Name} deals {enemy.AttackDamage} damage.");
        // Output:
        // Ninja deals 40 damage.
        
        
        FirestoreDb db = FirestoreDb.Create("coinseast");
        
        // Create a document with a random ID in the "users" collection.
        CollectionReference collection = db.Collection("test");
        DocumentReference document = await collection.AddAsync(new { Name = new { First = "Ada", Last = "Lovelace" }, Born = 1815 });

        // A DocumentReference doesn't contain the data - it's just a path.
        // Let's fetch the current document.
        DocumentSnapshot snapshot = await document.GetSnapshotAsync();

        // We can access individual fields by dot-separated path
        Debug.Log(snapshot.GetValue<string>("Name.First"));
        Debug.Log(snapshot.GetValue<string>("Name.Last"));
        Debug.Log(snapshot.GetValue<int>("Born"));
    }
}