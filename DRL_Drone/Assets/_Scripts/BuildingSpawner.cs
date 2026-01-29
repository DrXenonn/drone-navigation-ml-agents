using System;
using UnityEngine;
using System.Collections.Generic;
using Random = UnityEngine.Random;

public class BuildingSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private GameObject[] BuildingPrefabs;

    [Header("Spawn Settings")]
    [SerializeField] private int BuildingCount = 20;
    [SerializeField] private float MinDistanceBetween = 1.5f;

    [Header("Spawn Area")]
    [SerializeField] private Vector2 AreaSize = new Vector2(50f, 50f);
    [SerializeField] private Transform CenterPoint;

    private List<Vector3> _spawnedPositions = new List<Vector3>();
    private List<float> _prefabRadii = new List<float>();

    void Start()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;
    }

    public void Generate()
    {
        if (BuildingPrefabs == null || BuildingPrefabs.Length == 0)
        {
            Debug.LogError("No BuildingPrefabs assigned!");
            return;
        }

        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }

        _prefabRadii.Clear();
        foreach (var prefab in BuildingPrefabs)
            _prefabRadii.Add(EstimatePrefabRadius(prefab));

        SpawnBuildings();
    }

    private void SpawnBuildings()
    {
        _spawnedPositions.Clear();

        for (int i = 0; i < BuildingCount; i++)
        {
            int attempt = 0;
            bool placed = false;

            int prefabIndex = Random.Range(0, BuildingPrefabs.Length);
            float prefabRadius = _prefabRadii[Mathf.Clamp(prefabIndex, 0, _prefabRadii.Count - 1)];

            while (!placed && attempt < 1000)
            {
                attempt++;
                Vector3 candidate = GetRandomPointInArea();
                candidate.y = 0f + GetGroundOffset();

                if (IsPositionValid(candidate, prefabRadius))
                {
                    Quaternion rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                    Instantiate(BuildingPrefabs[prefabIndex], candidate, rot, transform);
                    _spawnedPositions.Add(candidate);
                    placed = true;
                }
            }

            if (!placed)
                Debug.LogWarning($"Could not place building #{i} after many attempts. Area may be too crowded.");
        }
    }

    private Vector3 GetRandomPointInArea()
    {
        float halfX = AreaSize.x * 0.5f;
        float halfZ = AreaSize.y * 0.5f;

        float rx = Random.Range(-halfX, halfX);
        float rz = Random.Range(-halfZ, halfZ);

        Vector3 center = CenterPoint != null ? CenterPoint.position : transform.position;
        return new Vector3(center.x + rx, center.y, center.z + rz);
    }

    private bool IsPositionValid(Vector3 candidate, float candidateRadius)
    {
        for (int i = 0; i < _spawnedPositions.Count; i++)
        {
            float otherRadius = ApproximateRadiusAtIndex(i);
            float required = otherRadius + candidateRadius + MinDistanceBetween;
            if (Vector3.Distance(_spawnedPositions[i], candidate) < required)
                return false;
        }

        return true;
    }

    private float ApproximateRadiusAtIndex(int index)
    {
        if (_prefabRadii.Count == 0) return 1f;
        float sum = 0f;
        for (int i = 0; i < _prefabRadii.Count; i++) sum += _prefabRadii[i];
        return sum / _prefabRadii.Count;
    }

    private float EstimatePrefabRadius(GameObject prefab)
    {
        if (prefab == null) return 1f;

        Renderer r = prefab.GetComponentInChildren<Renderer>();
        if (r != null)
        {
            Vector3 s = r.bounds.size;
            float radius = Mathf.Max(s.x, s.z) * 0.5f;
            if (radius > 0f) return radius;
        }

        Collider c = prefab.GetComponentInChildren<Collider>();
        if (c != null)
        {
            Vector3 s = c.bounds.size;
            float radius = Mathf.Max(s.x, s.z) * 0.5f;
            if (radius > 0f) return radius;
        }

        // fallback
        return 1f;
    }

    private float GetGroundOffset()
    {
        return 0f;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = CenterPoint != null ? CenterPoint.position : transform.position;
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(center, new Vector3(AreaSize.x, 0.1f, AreaSize.y));
    }
}
