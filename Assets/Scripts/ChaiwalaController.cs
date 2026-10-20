using System.Collections.Generic;
using UnityEngine;

namespace TapriTycoon
{
    /// <summary>
    /// Controls the 3D Chaiwala player character (boy with cap & white kurta):
    /// - WASD / Arrow Keys / Mobile Joystick / Tap-to-Move locomotion
    /// - Auto-brewing inside the Tapri Brew Ring or manual SPACE / Tap Kettle
    /// - Visual 3D Chai Cup stack balancing & swaying on the player's head
    /// - Auto-serving customers when approaching a table
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class ChaiwalaController : MonoBehaviour
    {
        [Header("Movement Settings")]
        public float moveSpeed = 7.5f;
        public float rotationSpeed = 14f;
        public Vector2 courtyardBoundsX = new Vector2(-4.4f, 9.2f);
        public Vector2 courtyardBoundsZ = new Vector2(-3.6f, 6.8f);

        [Header("Brewing & Stack Settings")]
        public int currentChaiStack = 2;
        public float brewInterval = 0.28f;
        public float brewZoneRadius = 1.6f;
        public float serveTableRadius = 2.25f;
        public Transform brewZoneCenter;

        [Header("3D Head Stack Visuals")]
        public Transform headTrayAnchor;
        public GameObject headChaiCupPrefab;
        public float cupStackVerticalSpacing = 0.31f;
        public float stackSwayAmount = 0.025f;

        [Header("Character Limb Animation")]
        public Transform bodyRig;
        public Transform leftLeg;
        public Transform rightLeg;

        [Header("Mobile Virtual Joystick (Optional)")]
        public Vector2 joystickInput;

        private CharacterController _controller;
        private readonly List<Transform> _spawnedHeadCups = new List<Transform>();
        private float _brewCooldownTimer;
        private float _walkCycle;
        private Vector2 _currentStackSway;

        // Tap-to-move target state
        private bool _hasMoveTarget;
        private Vector3 _moveTargetPos;
        private TableSlot _pendingServeTable;
        private bool _autoBrewThenServe;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        private void Start()
        {
            SyncHeadStackVisuals();
        }

        private void Update()
        {
            if (_brewCooldownTimer > 0f)
            {
                _brewCooldownTimer -= Time.deltaTime;
            }

            HandleMovement();
            CheckBrewZoneAutoBrew();
            CheckNearbyTablesAutoServe();
            HandleKeyboardShortcuts();
        }

        private void HandleKeyboardShortcuts()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                BrewOneChai(true);
            }

            // Click on 3D world to move, brew, or serve
            if (Input.GetMouseButtonDown(0) && !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                HandleScreenTap(Input.mousePosition);
            }
        }

        private void HandleScreenTap(Vector2 screenPos)
        {
            if (Camera.main == null) return;

            Ray ray = Camera.main.ScreenPointToRay(screenPos);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                if (hit.collider.CompareTag("Kettle") || hit.collider.CompareTag("TapriStall"))
                {
                    BrewOneChai(true);
                    if (brewZoneCenter != null)
                        SetMoveTarget(brewZoneCenter.position);
                    return;
                }

                TableSlot table = hit.collider.GetComponentInParent<TableSlot>();
                if (table != null)
                {
                    OnTableClicked(table);
                    return;
                }

                // Walk to ground point
                SetMoveTarget(hit.point);
                _pendingServeTable = null;
                _autoBrewThenServe = false;
            }
        }

        public void SetMoveTarget(Vector3 worldTarget)
        {
            _moveTargetPos = new Vector3(
                Mathf.Clamp(worldTarget.x, courtyardBoundsX.x, courtyardBoundsX.y),
                transform.position.y,
                Mathf.Clamp(worldTarget.z, courtyardBoundsZ.x, courtyardBoundsZ.y)
            );
            _hasMoveTarget = true;
        }

        public void OnTableClicked(TableSlot table)
        {
            if (table == null) return;

            if (table.HasSeatedCustomer)
            {
                if (currentChaiStack <= 0 && brewZoneCenter != null)
                {
                    // Walk to kettle first, brew up, then serve this table
                    GameManager.Instance.SpawnFloatingText(
                        transform.position + Vector3.up * 2.5f,
                        "Need Chai! Brewing...",
                        Color.yellow
                    );
                    SetMoveTarget(brewZoneCenter.position);
                    _pendingServeTable = table;
                    _autoBrewThenServe = true;
                    return;
                }

                float dist = Vector3.Distance(transform.position, table.transform.position);
                if (dist <= serveTableRadius * 1.3f)
                {
                    TryServeTable(table);
                }
                else
                {
                    SetMoveTarget(table.GetPlayerStandPoint());
                    _pendingServeTable = table;
                    _autoBrewThenServe = false;
                }
            }
            else
            {
                SetMoveTarget(table.GetPlayerStandPoint());
            }
        }

        private void HandleMovement()
        {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");

            Vector3 inputDir = new Vector3(h, 0f, v);

            if (joystickInput.sqrMagnitude > 0.01f)
            {
                inputDir = new Vector3(joystickInput.x, 0f, joystickInput.y);
                _hasMoveTarget = false;
            }
            else if (inputDir.sqrMagnitude > 0.01f)
            {
                inputDir.Normalize();
                _hasMoveTarget = false;
            }
            else if (_hasMoveTarget)
            {
                Vector3 toTarget = _moveTargetPos - transform.position;
                toTarget.y = 0f;

                if (toTarget.magnitude > 0.22f)
                {
                    inputDir = toTarget.normalized;
                }
                else
                {
                    _hasMoveTarget = false;
                    if (_autoBrewThenServe && currentChaiStack >= GameManager.Instance.maxStack && _pendingServeTable != null)
                    {
                        _autoBrewThenServe = false;
                        SetMoveTarget(_pendingServeTable.GetPlayerStandPoint());
                    }
                }
            }

            Vector3 velocity = inputDir * moveSpeed;
            _controller.Move(velocity * Time.deltaTime);

            // Clamp inside Tapri Courtyard bounds
            Vector3 clampedPos = transform.position;
            clampedPos.x = Mathf.Clamp(clampedPos.x, courtyardBoundsX.x, courtyardBoundsX.y);
            clampedPos.z = Mathf.Clamp(clampedPos.z, courtyardBoundsZ.x, courtyardBoundsZ.y);
            clampedPos.y = 0f;
            transform.position = clampedPos;

            AnimateBodyAndHeadStack(velocity);
        }

        private void AnimateBodyAndHeadStack(Vector3 velocity)
        {
            Vector3 flatVel = new Vector3(velocity.x, 0f, velocity.z);

            if (flatVel.sqrMagnitude > 0.05f)
            {
                Quaternion targetRot = Quaternion.LookRotation(flatVel.normalized, Vector3.up);
                if (bodyRig != null)
                {
                    bodyRig.rotation = Quaternion.Slerp(bodyRig.rotation, targetRot, Time.deltaTime * rotationSpeed);
                }

                _walkCycle += Time.deltaTime * 13f;
                float legSwing = Mathf.Sin(_walkCycle) * 32f;

                if (leftLeg != null) leftLeg.localRotation = Quaternion.Euler(legSwing, 0f, 0f);
                if (rightLeg != null) rightLeg.localRotation = Quaternion.Euler(-legSwing, 0f, 0f);
                if (bodyRig != null)
                {
                    Vector3 rigPos = bodyRig.localPosition;
                    rigPos.y = Mathf.Abs(Mathf.Sin(_walkCycle)) * 0.09f;
                    bodyRig.localPosition = rigPos;
                }
            }
            else
            {
                if (leftLeg != null)
                    leftLeg.localRotation = Quaternion.Slerp(leftLeg.localRotation, Quaternion.identity, Time.deltaTime * 10f);
                if (rightLeg != null)
                    rightLeg.localRotation = Quaternion.Slerp(rightLeg.localRotation, Quaternion.identity, Time.deltaTime * 10f);
                if (bodyRig != null)
                {
                    bodyRig.localPosition = Vector3.Lerp(bodyRig.localPosition, Vector3.zero, Time.deltaTime * 10f);
                }
            }

            // Sway 3D Chai Cups stacked on Player's Head based on velocity inertia
            Vector2 targetSway = new Vector2(-velocity.x * stackSwayAmount, -velocity.z * stackSwayAmount);
            _currentStackSway = Vector2.Lerp(_currentStackSway, targetSway, Time.deltaTime * 8f);

            for (int i = 0; i < currentChaiStack && i < _spawnedHeadCups.Count; i++)
            {
                Transform cup = _spawnedHeadCups[i];
                float factor = (i + 1) * 0.42f;
                cup.localPosition = new Vector3(
                    _currentStackSway.x * factor,
                    i * cupStackVerticalSpacing,
                    _currentStackSway.y * factor
                );
                cup.localRotation = Quaternion.Euler(
                    _currentStackSway.y * 20f,
                    0f,
                    -_currentStackSway.x * 20f
                );
            }
        }

        private void CheckBrewZoneAutoBrew()
        {
            if (brewZoneCenter == null) return;

            float dist = Vector3.Distance(transform.position, brewZoneCenter.position);
            if (dist <= brewZoneRadius && _brewCooldownTimer <= 0f && currentChaiStack < GameManager.Instance.maxStack)
            {
                BrewOneChai(false);

                if (_autoBrewThenServe && currentChaiStack >= GameManager.Instance.maxStack && _pendingServeTable != null)
                {
                    _autoBrewThenServe = false;
                    SetMoveTarget(_pendingServeTable.GetPlayerStandPoint());
                }
            }
        }

        private void CheckNearbyTablesAutoServe()
        {
            if (currentChaiStack <= 0 || GameManager.Instance.tableManager == null) return;

            foreach (TableSlot table in GameManager.Instance.tableManager.tables)
            {
                if (table != null && table.HasSeatedCustomer)
                {
                    float dist = Vector3.Distance(transform.position, table.transform.position);
                    if (dist <= serveTableRadius)
                    {
                        TryServeTable(table);
                        if (currentChaiStack <= 0) break;
                    }
                }
            }
        }

        public bool BrewOneChai(bool manualTrigger)
        {
            int maxStack = GameManager.Instance.maxStack;
            if (currentChaiStack >= maxStack)
            {
                if (manualTrigger)
                {
                    GameManager.Instance.SpawnFloatingText(
                        transform.position + Vector3.up * (2.6f + currentChaiStack * 0.25f),
                        "MAX STACK!",
                        new Color(0.98f, 0.75f, 0.14f)
                    );
                }
                return false;
            }

            currentChaiStack++;
            _brewCooldownTimer = brewInterval;
            SyncHeadStackVisuals();

            Vector3 topCupPos = headTrayAnchor != null
                ? headTrayAnchor.position + Vector3.up * (currentChaiStack * cupStackVerticalSpacing)
                : transform.position + Vector3.up * 2.2f;

            GameManager.Instance.PlayBrewEffects(topCupPos);
            return true;
        }

        public bool TryServeTable(TableSlot table)
        {
            if (table == null || !table.HasSeatedCustomer || currentChaiStack <= 0)
                return false;

            CustomerAI customer = table.currentCustomer;
            int needed = customer.remainingDemand;

            if (currentChaiStack >= needed)
            {
                currentChaiStack -= needed;
                SyncHeadStackVisuals();
                Vector3 origin = headTrayAnchor != null ? headTrayAnchor.position : transform.position + Vector3.up * 2.2f;
                table.CompleteCustomerOrder(false, origin);
            }
            else
            {
                int delivered = currentChaiStack;
                currentChaiStack = 0;
                SyncHeadStackVisuals();
                customer.DeliverPartialChai(delivered);
                GameManager.Instance.UpdateHUD();
            }

            return true;
        }

        public void SyncHeadStackVisuals()
        {
            if (headTrayAnchor == null || headChaiCupPrefab == null) return;

            while (_spawnedHeadCups.Count < currentChaiStack)
            {
                GameObject cupObj = Instantiate(headChaiCupPrefab, headTrayAnchor);
                cupObj.transform.localPosition = new Vector3(0f, _spawnedHeadCups.Count * cupStackVerticalSpacing, 0f);
                _spawnedHeadCups.Add(cupObj.transform);
            }

            for (int i = 0; i < _spawnedHeadCups.Count; i++)
            {
                _spawnedHeadCups[i].gameObject.SetActive(i < currentChaiStack);
            }
        }
    }
}
