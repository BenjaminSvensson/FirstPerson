using UnityEngine;

public class PrefabSpawner : MonoBehaviour
{
    [Header("Spawner Settings")]
    public GameObject prefabToSpawn;     // The prefab you want to spawn
    public Transform spawnPoint;         // Where it should appear (can be an empty GameObject)
    public float spawnInterval = 3f;     // Time in seconds between spawns

    private float timer;

    private void Update()
    {
        // Count up every frame
        timer += Time.deltaTime;

        // Check if it's time to spawn
        if (timer >= spawnInterval)
        {
            SpawnPrefab();
            timer = 0f; // reset timer
        }
    }

    private void SpawnPrefab()
    {
        if (prefabToSpawn == null || spawnPoint == null)
        {
            Debug.LogWarning("Spawner is missing prefab or spawn point!");
            return;
        }

        Instantiate(prefabToSpawn, spawnPoint.position, spawnPoint.rotation);
    }
}
