using System.Collections.Generic;
using UnityEngine;

public class VisualPool : MonoBehaviour
{
    public static VisualPool Instance { get; private set; }

    public List<VisualPoolSettings> poolSettings;

    private Dictionary<GameObject, Queue<GameObject>> visualPoolDictionary = new Dictionary<GameObject, Queue<GameObject>>();
    private Dictionary<GameObject, int> visualPoolLimits = new Dictionary<GameObject, int>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializePools();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializePools()
    {
        foreach (var settings in poolSettings)
        {
            if (settings.visualPrefab != null)
            {
                Queue<GameObject> newPool = new Queue<GameObject>();
                visualPoolDictionary[settings.visualPrefab] = newPool;
                visualPoolLimits[settings.visualPrefab] = settings.poolSize;

                for (int i = 0; i < settings.poolSize; i++)
                {
                    GameObject newVisual = Instantiate(settings.visualPrefab, transform);
                    newVisual.SetActive(false);
                    newPool.Enqueue(newVisual);
                }
            }
        }
    }

    public GameObject GetVisual(GameObject visualPrefab)
    {
        if (visualPoolDictionary.ContainsKey(visualPrefab) && visualPoolDictionary[visualPrefab].Count > 0)
        {
            GameObject visual = visualPoolDictionary[visualPrefab].Dequeue();
            visual.SetActive(true);
            return visual;
        }

        // If the pool is empty (all visuals are in use), we can't create more.
        return null;
    }

    public void ReturnVisual(GameObject visual)
    {
        VisualInfo info = visual.GetComponent<VisualInfo>();
        if (info != null && visualPoolDictionary.ContainsKey(info.Prefab))
        {
            visual.SetActive(false);
            visualPoolDictionary[info.Prefab].Enqueue(visual);
        }
        else
        {
            Destroy(visual);
        }
    }
}