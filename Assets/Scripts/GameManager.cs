using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TapriTycoon
{
    /// <summary>
    /// Central singleton managing Tapri Cash, Head Stack Upgrades, Staff Hiring,
    /// Offline Earnings, Isometric Camera Shake, Floating World Text, and UI HUD.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Core Economy & Progression")]
        public int money = 50;
        public int maxStack = 5;
        public int staffCount = 0;
        public int servedCount = 0;
        public int walkoutCount = 0;

        [Header("Upgrade Costs & Rules")]
        public int hireStaffCost = 100;
        public int upgradeStackCost = 200;
        public int walkoutPenalty = 20;
        public int pricePerChai = 10;

        [Header("Scene References")]
        public ChaiwalaController player;
        public TableManager tableManager;
        public WeatherSystem weatherSystem;
        public Transform cameraTransform;
        public Transform tapriKettlePoint;
        public Transform staffSpawnPoint;
        public ParticleSystem kettleSteamParticles;

        [Header("Prefabs")]
        public GameObject chotuStaffPrefab;
        public GameObject chaiCupPrefab;
        public GameObject floatingTextPrefab;

        [Header("HUD UI References")]
        public TextMeshProUGUI moneyText;
        public TextMeshProUGUI stackText;
        public TextMeshProUGUI staffText;
        public TextMeshProUGUI weatherText;
        public TextMeshProUGUI servedText;
        public Button hireStaffButton;
        public Button upgradeStackButton;
        public Button brewButton;

        [Header("Offline Earnings Popup")]
        public GameObject offlinePopupPanel;
        public TextMeshProUGUI offlineSummaryText;
        public TextMeshProUGUI offlineRewardText;

        private Vector3 _baseCameraLocalPos;
        private float _shakeTimer;
        private float _shakeMagnitude;
        private int _pendingOfflineReward;
        private readonly List<ChotuStaff> _activeStaff = new List<ChotuStaff>();

        private const string PREF_MONEY = "Tapri_Money";
        private const string PREF_MAX_STACK = "Tapri_MaxStack";
        private const string PREF_STAFF = "Tapri_StaffCount";
        private const string PREF_LAST_TIME = "Tapri_LastTimestamp";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }

            if (cameraTransform != null)
            {
                _baseCameraLocalPos = cameraTransform.localPosition;
            }

            if (hireStaffButton != null)
                hireStaffButton.onClick.AddListener(HireChotuStaff);

            if (upgradeStackButton != null)
                upgradeStackButton.onClick.AddListener(UpgradeMaxStack);

            if (brewButton != null && player != null)
                brewButton.onClick.AddListener(() => player.BrewOneChai(true));

            LoadGameProgress();
            UpdateHUD();
        }

        private void LateUpdate()
        {
            UpdateCameraFollowAndShake();
        }

        private void UpdateCameraFollowAndShake()
        {
            if (cameraTransform == null || player == null) return;

            Vector3 targetPos = _baseCameraLocalPos + new Vector3(
                player.transform.position.x * 0.18f,
                0f,
                player.transform.position.z * 0.15f
            );

            cameraTransform.localPosition = Vector3.Lerp(
                cameraTransform.localPosition,
                targetPos,
                Time.deltaTime * 5f
            );

            if (_shakeTimer > 0f)
            {
                _shakeTimer -= Time.deltaTime;
                float damper = Mathf.Clamp01(_shakeTimer / 0.25f);
                Vector2 offset = UnityEngine.Random.insideUnitCircle * (_shakeMagnitude * damper);
                cameraTransform.localPosition += new Vector3(offset.x, offset.y, 0f);
            }
        }

        public void TriggerCameraShake(float magnitude = 0.3f, float duration = 0.22f)
        {
            _shakeMagnitude = magnitude;
            _shakeTimer = duration;
        }

        public void OnCustomerServed(TableSlot table, int totalCupsServed, bool servedByStaff, Vector3 serveOrigin)
        {
            int reward = totalCupsServed * pricePerChai;
            money += reward;
            servedCount++;

            TriggerCameraShake(0.25f, 0.2f);
            SpawnFloatingText(
                table.transform.position + Vector3.up * 2.5f,
                $"+${reward}{(servedByStaff ? " (Staff)" : "")}",
                new Color(0.29f, 0.87f, 0.5f)
            );

            StartCoroutine(AnimateFlyingCupsRoutine(serveOrigin, table.cupDropPoint.position, totalCupsServed));
            UpdateHUD();
            SaveGameProgress();
        }

        public void OnCustomerAngryWalkout(TableSlot table)
        {
            money = Mathf.Max(0, money - walkoutPenalty);
            walkoutCount++;

            TriggerCameraShake(0.45f, 0.3f);
            SpawnFloatingText(
                table.transform.position + Vector3.up * 2.6f,
                $"-${walkoutPenalty} (Angry!)",
                new Color(0.94f, 0.27f, 0.27f)
            );

            UpdateHUD();
            SaveGameProgress();
        }

        public void HireChotuStaff()
        {
            if (money < hireStaffCost) return;

            money -= hireStaffCost;
            staffCount++;
            SpawnStaffActor(staffCount - 1);

            TriggerCameraShake(0.22f, 0.18f);
            if (staffSpawnPoint != null)
            {
                SpawnFloatingText(staffSpawnPoint.position + Vector3.up * 2.2f, "Hired Chotu! (-$100)", Color.green);
            }

            UpdateHUD();
            SaveGameProgress();
        }

        private void SpawnStaffActor(int index)
        {
            if (chotuStaffPrefab == null) return;

            Vector3 basePos = staffSpawnPoint != null ? staffSpawnPoint.position : new Vector3(-2.8f, 0f, 2.8f);
            Vector3 offset = new Vector3((index % 2) * 1.4f, 0f, (index / 2) * 1.3f);
            GameObject staffObj = Instantiate(chotuStaffPrefab, basePos + offset, Quaternion.identity);

            ChotuStaff staff = staffObj.GetComponent<ChotuStaff>();
            if (staff != null)
            {
                staff.Initialize(index, basePos + offset);
                _activeStaff.Add(staff);
            }
        }

        public void UpgradeMaxStack()
        {
            if (money < upgradeStackCost) return;

            money -= upgradeStackCost;
            maxStack += 2;

            TriggerCameraShake(0.22f, 0.18f);
            if (player != null)
            {
                SpawnFloatingText(
                    player.transform.position + Vector3.up * 2.8f,
                    $"Max Stack {maxStack}! (-$200)",
                    new Color(0.98f, 0.8f, 0.08f)
                );
            }

            UpdateHUD();
            SaveGameProgress();
        }

        public void PlayBrewEffects(Vector3 targetHeadPos)
        {
            if (kettleSteamParticles != null)
            {
                kettleSteamParticles.Emit(8);
            }

            if (tapriKettlePoint != null)
            {
                StartCoroutine(AnimateFlyingCupsRoutine(tapriKettlePoint.position, targetHeadPos, 1));
            }

            UpdateHUD();
        }

        private IEnumerator AnimateFlyingCupsRoutine(Vector3 from, Vector3 to, int count)
        {
            if (chaiCupPrefab == null) yield break;

            for (int i = 0; i < count; i++)
            {
                StartCoroutine(SingleFlyingCupArc(from, to, 0.25f));
                yield return new WaitForSeconds(0.045f);
            }
        }

        private IEnumerator SingleFlyingCupArc(Vector3 start, Vector3 end, float duration)
        {
            GameObject cup = Instantiate(chaiCupPrefab, start, Quaternion.identity);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                Vector3 pos = Vector3.Lerp(start, end, t);
                pos.y += Mathf.Sin(t * Mathf.PI) * 1.85f;
                cup.transform.position = pos;
                cup.transform.Rotate(Vector3.up, 420f * Time.deltaTime);
                yield return null;
            }

            Destroy(cup);
        }

        public void SpawnFloatingText(Vector3 worldPos, string message, Color color)
        {
            if (floatingTextPrefab == null) return;

            GameObject go = Instantiate(floatingTextPrefab, worldPos, Quaternion.identity);
            TextMeshPro tmp = go.GetComponentInChildren<TextMeshPro>();
            if (tmp != null)
            {
                tmp.text = message;
                tmp.color = color;
            }
            StartCoroutine(FloatAndFadeRoutine(go.transform, tmp, 1.15f));
        }

        private IEnumerator FloatAndFadeRoutine(Transform tr, TextMeshPro tmp, float duration)
        {
            float elapsed = 0f;
            Color baseColor = tmp != null ? tmp.color : Color.white;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                if (tr != null)
                {
                    tr.position += Vector3.up * (2.1f * Time.deltaTime);
                    if (Camera.main != null)
                    {
                        tr.rotation = Camera.main.transform.rotation;
                    }
                }
                if (tmp != null)
                {
                    float alpha = Mathf.Clamp01(1f - (elapsed / duration));
                    tmp.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
                }
                yield return null;
            }

            if (tr != null) Destroy(tr.gameObject);
        }

        public void UpdateHUD()
        {
            int currentChai = player != null ? player.currentChaiStack : 0;

            if (moneyText != null) moneyText.text = $"${money}";
            if (stackText != null) stackText.text = $"{currentChai} / {maxStack}";
            if (staffText != null) staffText.text = $"{staffCount} (1/3s)";
            if (servedText != null) servedText.text = $"Served: {servedCount} | Left: {walkoutCount}";

            if (weatherSystem != null && weatherText != null)
            {
                weatherText.text = weatherSystem.GetWeatherStatusSummary();
            }

            if (hireStaffButton != null)
                hireStaffButton.interactable = money >= hireStaffCost;

            if (upgradeStackButton != null)
                upgradeStackButton.interactable = money >= upgradeStackCost;
        }

        public void SaveGameProgress()
        {
            PlayerPrefs.SetInt(PREF_MONEY, money);
            PlayerPrefs.SetInt(PREF_MAX_STACK, maxStack);
            PlayerPrefs.SetInt(PREF_STAFF, staffCount);
            PlayerPrefs.SetString(PREF_LAST_TIME, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());
            PlayerPrefs.Save();
        }

        private void LoadGameProgress()
        {
            money = PlayerPrefs.GetInt(PREF_MONEY, 50);
            maxStack = PlayerPrefs.GetInt(PREF_MAX_STACK, 5);
            int savedStaff = PlayerPrefs.GetInt(PREF_STAFF, 0);

            staffCount = 0;
            for (int i = 0; i < savedStaff; i++)
            {
                staffCount++;
                SpawnStaffActor(i);
            }

            string lastTimeStr = PlayerPrefs.GetString(PREF_LAST_TIME, "");
            if (long.TryParse(lastTimeStr, out long lastUnix))
            {
                long nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                long elapsedSeconds = nowUnix - lastUnix;
                if (elapsedSeconds >= 30)
                {
                    ShowOfflineEarningsPopup(elapsedSeconds);
                }
            }
        }

        public void ShowOfflineEarningsPopup(long secondsAway)
        {
            long cappedSec = Math.Min(secondsAway, 3600 * 8);
            float ratePerMin = 20f + (staffCount * 35f) + ((maxStack - 5) * 5f);
            _pendingOfflineReward = Mathf.Max(20, Mathf.RoundToInt(((cappedSec / 60f) * ratePerMin) / 10f) * 10);

            if (offlinePopupPanel != null)
            {
                offlinePopupPanel.SetActive(true);
                if (offlineSummaryText != null)
                    offlineSummaryText.text = $"Away for {cappedSec / 60}m {cappedSec % 60}s — your Tapri stayed busy!";
                if (offlineRewardText != null)
                    offlineRewardText.text = $"+${_pendingOfflineReward}";
            }
        }

        public void CollectOfflineEarnings()
        {
            if (_pendingOfflineReward > 0)
            {
                money += _pendingOfflineReward;
                if (player != null)
                {
                    SpawnFloatingText(
                        player.transform.position + Vector3.up * 2.8f,
                        $"+${_pendingOfflineReward} Offline!",
                        Color.green
                    );
                }
                _pendingOfflineReward = 0;
                UpdateHUD();
                SaveGameProgress();
            }

            if (offlinePopupPanel != null)
            {
                offlinePopupPanel.SetActive(false);
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus) SaveGameProgress();
        }

        private void OnApplicationQuit()
        {
            SaveGameProgress();
        }
    }
}
