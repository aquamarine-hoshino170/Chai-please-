using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using TMPro;

namespace TapriTycoon
{
    /// <summary>
    /// Controls an individual 3D Customer using Unity NavMeshAgent:
    /// - Walks from road spawn point to assigned Table chair
    /// - Displays world-space Demand Bubble (1-5 Chai) and 15-second Patience Bar
    /// - Turns glowing Angry Red at >10 seconds waiting (with shake vibration)
    /// - Walks out angrily at >15 seconds waiting with -$20 penalty + camera shake
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class CustomerAI : MonoBehaviour
    {
        public enum CustomerState
        {
            WalkingToSeat,
            SeatedWaiting,
            LeavingHappy,
            LeavingAngry
        }

        [Header("State & Order Demand")]
        public CustomerState state = CustomerState.WalkingToSeat;
        public int originalDemand = 2;
        public int remainingDemand = 2;
        public float waitTimer = 0f;
        public float angryThresholdSeconds = 10f;
        public float maxWaitSeconds = 15f;

        [Header("3D Mesh & Materials (Turns RED at >10s)")]
        public Renderer[] bodyRenderers;
        public Material angryRedMaterial;
        public GameObject angryFloorRing;
        public Transform visualBodyRoot;

        [Header("World-Space Demand Bubble UI")]
        public Canvas worldBubbleCanvas;
        public TextMeshProUGUI demandCupsText;
        public TextMeshProUGUI demandPriceText;
        public TextMeshProUGUI weatherBonusTag;
        public Image patienceBarFill;
        public Image bubbleBackground;

        [Header("Patience Colors")]
        public Color healthyGreen = new Color(0.13f, 0.77f, 0.37f);
        public Color cautionAmber = new Color(0.96f, 0.62f, 0.04f);
        public Color angryRed = new Color(0.86f, 0.15f, 0.15f);

        private NavMeshAgent _agent;
        private TableSlot _assignedTable;
        private Vector3 _exitPoint;
        private bool _hasTurnedAngryRed;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            if (worldBubbleCanvas != null)
            {
                worldBubbleCanvas.gameObject.SetActive(false);
            }
            if (angryFloorRing != null)
            {
                angryFloorRing.SetActive(false);
            }
        }

        public void InitializeCustomer(TableSlot table, int chaiDemand, Vector3 exitPosition, bool rainBoosted)
        {
            _assignedTable = table;
            _exitPoint = exitPosition;
            originalDemand = Mathf.Clamp(chaiDemand, 1, 5);
            remainingDemand = originalDemand;
            waitTimer = 0f;
            _hasTurnedAngryRed = false;
            state = CustomerState.WalkingToSeat;

            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.isStopped = false;
                _agent.SetDestination(table.seatPoint.position);
            }
            else
            {
                transform.position = table.seatPoint.position;
                ArriveAtTableSeat();
            }

            UpdateDemandBubbleUI(rainBoosted);
        }

        private void Update()
        {
            if (worldBubbleCanvas != null && worldBubbleCanvas.gameObject.activeSelf && Camera.main != null)
            {
                worldBubbleCanvas.transform.rotation = Camera.main.transform.rotation;
            }

            switch (state)
            {
                case CustomerState.WalkingToSeat:
                    UpdateWalkingToSeat();
                    break;

                case CustomerState.SeatedWaiting:
                    UpdateSeatedPatience();
                    break;

                case CustomerState.LeavingHappy:
                case CustomerState.LeavingAngry:
                    UpdateLeavingWalk();
                    break;
            }
        }

        private void UpdateWalkingToSeat()
        {
            if (_agent == null || !_agent.isOnNavMesh)
            {
                ArriveAtTableSeat();
                return;
            }

            if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.15f)
            {
                ArriveAtTableSeat();
            }
        }

        private void ArriveAtTableSeat()
        {
            state = CustomerState.SeatedWaiting;
            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.isStopped = true;
            }

            if (_assignedTable != null)
            {
                transform.position = _assignedTable.seatPoint.position;
                transform.rotation = _assignedTable.seatPoint.rotation;
            }

            if (worldBubbleCanvas != null)
            {
                worldBubbleCanvas.gameObject.SetActive(true);
            }
        }

        private void UpdateSeatedPatience()
        {
            waitTimer += Time.deltaTime;
            float remainingRatio = Mathf.Clamp01(1f - (waitTimer / maxWaitSeconds));

            if (patienceBarFill != null)
            {
                patienceBarFill.fillAmount = remainingRatio;
                if (waitTimer >= angryThresholdSeconds)
                    patienceBarFill.color = angryRed;
                else if (waitTimer >= 6f)
                    patienceBarFill.color = cautionAmber;
                else
                    patienceBarFill.color = healthyGreen;
            }

            // Turn RED angry material at > 10 seconds
            if (waitTimer >= angryThresholdSeconds)
            {
                if (!_hasTurnedAngryRed)
                {
                    TriggerAngryRedState();
                }

                // Rapid angry vibration on visual mesh
                if (visualBodyRoot != null)
                {
                    float shakeX = Mathf.Sin(Time.time * 42f) * 0.08f;
                    visualBodyRoot.localPosition = new Vector3(shakeX, 0f, 0f);
                }
            }

            // Leave angrily with -$20 penalty at > 15 seconds
            if (waitTimer >= maxWaitSeconds)
            {
                if (_assignedTable != null)
                {
                    _assignedTable.OnCustomerWalkedOutAngry();
                }
            }
        }

        private void TriggerAngryRedState()
        {
            _hasTurnedAngryRed = true;

            if (angryRedMaterial != null && bodyRenderers != null)
            {
                foreach (Renderer r in bodyRenderers)
                {
                    if (r != null) r.sharedMaterial = angryRedMaterial;
                }
            }

            if (angryFloorRing != null)
            {
                angryFloorRing.SetActive(true);
            }

            if (bubbleBackground != null)
            {
                bubbleBackground.color = new Color(1f, 0.92f, 0.92f);
            }

            GameManager.Instance.SpawnFloatingText(
                transform.position + Vector3.up * 2.6f,
                "ANGRY! (>10s)",
                angryRed
            );
        }

        public void DeliverPartialChai(int deliveredCups)
        {
            remainingDemand = Mathf.Max(1, remainingDemand - deliveredCups);
            waitTimer = Mathf.Max(0f, waitTimer - deliveredCups * 1.5f);
            UpdateDemandBubbleUI(false);

            GameManager.Instance.SpawnFloatingText(
                transform.position + Vector3.up * 2.4f,
                $"{remainingDemand} more Chai!",
                Color.yellow
            );
        }

        public void BoostDemandFromWeather(int extraCups)
        {
            if (state != CustomerState.SeatedWaiting) return;

            remainingDemand = Mathf.Min(5, remainingDemand + extraCups);
            originalDemand = Mathf.Max(originalDemand, remainingDemand);
            UpdateDemandBubbleUI(true);
        }

        public void UpdateDemandBubbleUI(bool showWeatherTag)
        {
            if (demandCupsText != null)
                demandCupsText.text = $"Chai x {remainingDemand}";

            if (demandPriceText != null)
                demandPriceText.text = $"${originalDemand * GameManager.Instance.pricePerChai}";

            if (weatherBonusTag != null)
                weatherBonusTag.gameObject.SetActive(showWeatherTag);
        }

        public void LeaveTable(bool happy)
        {
            state = happy ? CustomerState.LeavingHappy : CustomerState.LeavingAngry;

            if (worldBubbleCanvas != null)
                worldBubbleCanvas.gameObject.SetActive(false);

            if (angryFloorRing != null)
                angryFloorRing.SetActive(false);

            if (visualBodyRoot != null)
                visualBodyRoot.localPosition = Vector3.zero;

            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.isStopped = false;
                _agent.speed = happy ? 4.5f : 6.0f;
                _agent.SetDestination(_exitPoint);
                Destroy(gameObject, 5f);
            }
            else
            {
                Destroy(gameObject, 0.1f);
            }
        }

        private void UpdateLeavingWalk()
        {
            if (_agent != null && _agent.isOnNavMesh && !_agent.pathPending && _agent.remainingDistance <= 0.6f)
            {
                Destroy(gameObject);
            }
        }

        // Support direct tap on customer's world-space bubble button
        public void OnBubbleTapped()
        {
            if (_assignedTable != null && GameManager.Instance.player != null)
            {
                GameManager.Instance.player.OnTableClicked(_assignedTable);
            }
        }
    }
}
