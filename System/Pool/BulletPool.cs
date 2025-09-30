using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GNB;

public class BulletPool : MonoBehaviour
{
    public static BulletPool Instance { get; private set; }

    // List to hold our settings for each bullet type
    public List<BulletPoolSettings> poolSettings;

    // Dictionary to hold the pools
    private Dictionary<Bullet, Queue<Bullet>> poolDictionary = new Dictionary<Bullet, Queue<Bullet>>();
    private Dictionary<Bullet, int> poolLimits = new Dictionary<Bullet, int>();
    
    // A flag to ensure we don't try to access the pools before they're fully loaded.
    public bool IsPoolsInitialized { get; private set; } = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            // Start the coroutine to initialize the pools gradually.
            StartCoroutine(InitializePoolsAsync());
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private IEnumerator InitializePoolsAsync()
    {
        Debug.Log("Starting to initialize bullet pools gradually...");

        // Define how many bullets to instantiate per frame
        const int chunkSize = 100;

        foreach (var settings in poolSettings)
        {
            if (settings.bulletPrefab != null)
            {
                // Create a new queue and add it to the dictionary
                Queue<Bullet> newPool = new Queue<Bullet>();
                poolDictionary[settings.bulletPrefab] = newPool;

                // Store the limit for this pool
                poolLimits[settings.bulletPrefab] = settings.poolSize;

                // Pre-populate the pool in chunks.
                for (int i = 0; i < settings.poolSize; i++)
                {
                    Bullet newBullet = Instantiate(settings.bulletPrefab, transform);
                    newBullet.gameObject.SetActive(false);
                    newPool.Enqueue(newBullet);
                    
                    // The key change: yield control back to the main game loop
                    // after instantiating a chunk of bullets.
                    if ((i + 1) % chunkSize == 0)
                    {
                        yield return null; 
                    }
                }
            }
        }
        
        IsPoolsInitialized = true;
        Debug.Log("Bullet pool initialization complete!");
    }

    public Bullet GetBullet(Bullet prefab, Vector3 position, Quaternion rotation, Transform parent)
    {
        // First, check if the pools are even ready to be used.
        if (!IsPoolsInitialized)
        {
            Debug.LogError("Attempting to get a bullet before pools are fully initialized!");
            return null;
        }

        // Check if there's an available bullet in the pool.
        if (poolDictionary.ContainsKey(prefab) && poolDictionary[prefab].Count > 0)
        {
            Bullet bullet = poolDictionary[prefab].Dequeue();
            bullet.transform.position = position;
            bullet.transform.rotation = rotation;
            bullet.transform.SetParent(parent);
            bullet.gameObject.SetActive(true);
            return bullet;
        }
        
        // If the pool is empty, check the hard limit.
        if (poolLimits.ContainsKey(prefab) && poolLimits[prefab] > 0)
        {
            // If the queue is empty, all bullets are currently active.
            return null;
        }

        // If the pool doesn't exist, create a new one and a new bullet.
        if (!poolDictionary.ContainsKey(prefab))
        {
            poolDictionary[prefab] = new Queue<Bullet>();
            poolLimits[prefab] = 10; // Set a default limit.
        }

        // This part is for dynamic pools that can grow. 
        // With your pre-populated design, this code would likely be unreachable
        // once the async initialization is complete.
        Bullet newBullet = Instantiate(prefab, position, rotation, parent);
        return newBullet;
    }


    public void ReturnBullet(Bullet bullet)
    {
        // Get the original prefab from the instance's own component
        Bullet originalPrefab = bullet.Prefab;

        // Deactivate the bullet and return it to the correct pool
        if (originalPrefab != null && poolDictionary.ContainsKey(originalPrefab))
        {
            bullet.gameObject.SetActive(false);
            poolDictionary[originalPrefab].Enqueue(bullet);
        }
        else
        {
            // If we can't find the correct pool, just destroy the bullet.
            Destroy(bullet.gameObject);
        }
    }
}
