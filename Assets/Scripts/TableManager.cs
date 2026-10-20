using System.Collections.Generic;
using UnityEngine;

namespace TapriTycoon
{
    /// <summary>
    /// Represents a single Tapri Customer Table (1 of 3 tables) with its seat anchor,
    /// pop-up monsoon umbrella, and customer spawn timer.
    /// </summary>
    [System.Serializable]
    public class TableSlot : MonoBehaviour
    {
        public int tableId;
        public Transform seatPoint;
        public Transform cupDropPoint;
        public Transform umbrellaTransform;

        [HideInInspector] public CustomerAI currentCustomer;
        [HideInInspector] public float respawnTimer = 1.0f;

        public bool HasSeatedCustomer =>
            currentCustomer != null && currentCustomer.state == CustomerAI.CustomerState.SeatedWaiting;

        public bool IsOccupied => currentCustomer != null;

        public Vector3 GetPlayerStandPoint()
        {
            return transform.position + new Vector3(-1.35f, 0f, 0.6f);
        }

        public void CompleteCustomerOrder(bool servedByStaff, Vector3 serveOrigin)
        {
            if (currentCustomer == null) return;

            int totalDemand = currentCustomer.originalDemand;
            CustomerAI finishedCustomer = currentCustomer;
            currentCustomer = null;

            float arrivalMultiplier = GameManager.Instance.weatherSystem != null
                ? GameManager.Instance.weatherSystem.GetArrivalDelayMultiplier()
                : 1.0f;
            respawnTimer = Random.Range(1.3f, 2.7f) * arrivalMultiplier;

            finishedCustomer.LeaveTable(true);
            GameManager.Instance.OnCustomerServed(this, totalDemand, servedByStaff, serveOrigin);
        }

        public void OnCustomerWalkedOutAngry()
        {
            if (currentCustomer == null) return;

            CustomerAI angryCustomer = currentCustomer;
            currentCustomer = null;

            float arrivalMultiplier = GameManager.Instance.weatherSystem != null
                ? GameManager.Instance.weatherSystem.GetArrivalDelayMultiplier()
                : 1.0f;
            respawnTimer = Random.Range(1.5f, 3.0f) * arrivalMultiplier;

            angryCustomer.LeaveTable(false);
            GameManager.Instance.OnCustomerAngryWalkout(this);
        }
    }

    /// <summary>
    /// Manages all 3 Tapri Tables, spawns NavMesh customers based on Weather arrival rate
    /// and Chai demand (1-5 in Sunny, 2-5 in Rain, 2-4 in Fog), and animates table umbrellas.
    /// </summary>
    public class TableManager : MonoBehaviour
    {
        [Header("3 Customer Tables")]
        public List<TableSlot> tables = new List<TableSlot>();

        [Header("Customer Spawning")]
        public GameObject customerPrefab;
        public Transform customerSpawnPoint;
        public Transform customerExitPoint;

        [Header("Random Customer Outfit Palette")]
        public Color[] outfitColors = new Color[]
        {
            new Color(0.15f, 0.39f, 0.92f),
            new Color(0.85f, 0.47f, 0.02f),
            new Color(0.58f, 0.20f, 0.92f),
            new Color(0.02f, 0.59f, 0.41f),
            new Color(0.88f, 0.11f, 0.28f)
        };

        private void Start()
        {
            for (int i = 0; i < tables.Count; i++)
            {
                if (tables[i] != null)
                {
                    tables[i].tableId = i;
                    tables[i].respawnTimer = 0.3f + i * 0.8f;
                }
            }
        }

        private void Update()
        {
            bool showUmbrellas = GameManager.Instance.weatherSystem != null &&
                                 GameManager.Instance.weatherSystem.currentWeather == WeatherSystem.WeatherType.RAIN;

            foreach (TableSlot table in tables)
            {
                if (table == null) continue;

                // Smoothly scale pop-up monsoon umbrella when raining
                if (table.umbrellaTransform != null)
                {
                    Vector3 targetScale = showUmbrellas ? Vector3.one : Vector3.zero;
                    table.umbrellaTransform.localScale = Vector3.Lerp(
                        table.umbrellaTransform.localScale,
                        targetScale,
                        Time.deltaTime * 7f
                    );
                }

                // Tick spawn timer when table is empty
                if (!table.IsOccupied)
                {
                    table.respawnTimer -= Time.deltaTime;
                    if (table.respawnTimer <= 0f)
                    {
                        SpawnCustomerForTable(table);
                    }
                }
            }
        }

        private void SpawnCustomerForTable(TableSlot table)
        {
            if (customerPrefab == null) return;

            Vector3 spawnPos = customerSpawnPoint != null
                ? customerSpawnPoint.position
                : table.seatPoint.position + new Vector3(0f, 0f, -5f);

            Vector3 exitPos = customerExitPoint != null
                ? customerExitPoint.position
                : spawnPos;

            GameObject custObj = Instantiate(customerPrefab, spawnPos, Quaternion.identity);
            CustomerAI ai = custObj.GetComponent<CustomerAI>();

            if (ai != null)
            {
                // Apply random Indian kurta/outfit color before customer sits
                if (ai.bodyRenderers != null && ai.bodyRenderers.Length > 0 && outfitColors.Length > 0)
                {
                    Color chosen = outfitColors[Random.Range(0, outfitColors.Length)];
                    Material instanceMat = new Material(ai.bodyRenderers[0].sharedMaterial);
                    instanceMat.color = chosen;
                    foreach (Renderer r in ai.bodyRenderers)
                    {
                        if (r != null) r.sharedMaterial = instanceMat;
                    }
                }

                int demand = GameManager.Instance.weatherSystem != null
                    ? GameManager.Instance.weatherSystem.RollCustomerChaiDemand()
                    : Random.Range(1, 6);

                bool rainBoosted = GameManager.Instance.weatherSystem != null &&
                                   GameManager.Instance.weatherSystem.currentWeather != WeatherSystem.WeatherType.SUNNY;

                table.currentCustomer = ai;
                ai.InitializeCustomer(table, demand, exitPos, rainBoosted);
            }
        }

        public TableSlot GetMostUrgentWaitingTable()
        {
            TableSlot urgent = null;
            float highestWait = -1f;

            foreach (TableSlot table in tables)
            {
                if (table != null && table.HasSeatedCustomer)
                {
                    if (table.currentCustomer.waitTimer > highestWait)
                    {
                        highestWait = table.currentCustomer.waitTimer;
                        urgent = table;
                    }
                }
            }

            return urgent;
        }

        public void BoostAllSeatedCustomersDemand(int extraCups)
        {
            foreach (TableSlot table in tables)
            {
                if (table != null && table.HasSeatedCustomer)
                {
                    table.currentCustomer.BoostDemandFromWeather(extraCups);
                }
            }
        }
    }
}
