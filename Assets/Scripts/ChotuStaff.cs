using UnityEngine;
using UnityEngine.AI;

namespace TapriTycoon
{
    /// <summary>
    /// Autonomous "Chotu" Staff Helper AI:
    /// - Automatically serves 1 waiting customer table every 3 seconds
    /// - Uses NavMeshAgent (or smooth steering) to walk between the Tapri stall and urgent tables
    /// </summary>
    public class ChotuStaff : MonoBehaviour
    {
        [Header("Staff Settings")]
        public float serveCooldownSeconds = 3.0f;
        public float moveSpeed = 4.4f;

        private int _staffIndex;
        private Vector3 _homePosition;
        private float _serveTimer;
        private float _walkCycle;
        private NavMeshAgent _agent;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            if (_agent != null)
            {
                _agent.speed = moveSpeed;
            }
        }

        public void Initialize(int index, Vector3 homePos)
        {
            _staffIndex = index;
            _homePosition = homePos;
            _serveTimer = index * 0.75f;
        }

        private void Update()
        {
            if (GameManager.Instance == null || GameManager.Instance.tableManager == null)
                return;

            _serveTimer += Time.deltaTime;
            TableSlot urgentTable = GameManager.Instance.tableManager.GetMostUrgentWaitingTable();

            // Walk toward the most urgent waiting customer table, or return to home post
            Vector3 destination = urgentTable != null
                ? urgentTable.GetPlayerStandPoint()
                : _homePosition;

            MoveTowardsDestination(destination);

            // Auto-serve 1 customer every 3 seconds
            if (_serveTimer >= serveCooldownSeconds)
            {
                if (urgentTable != null && urgentTable.HasSeatedCustomer)
                {
                    _serveTimer -= serveCooldownSeconds;
                    urgentTable.CompleteCustomerOrder(true, transform.position + Vector3.up * 1.4f);
                }
                else
                {
                    _serveTimer = serveCooldownSeconds - 0.1f;
                }
            }
        }

        private void MoveTowardsDestination(Vector3 targetPos)
        {
            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.SetDestination(targetPos);
                return;
            }

            Vector3 diff = targetPos - transform.position;
            diff.y = 0f;
            float dist = diff.magnitude;

            if (dist > 0.15f)
            {
                Vector3 dir = diff.normalized;
                transform.position += dir * (moveSpeed * Time.deltaTime);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(dir, Vector3.up),
                    Time.deltaTime * 12f
                );

                _walkCycle += Time.deltaTime * 11f;
                Vector3 pos = transform.position;
                pos.y = Mathf.Abs(Mathf.Sin(_walkCycle)) * 0.06f;
                transform.position = pos;
            }
            else
            {
                Vector3 pos = transform.position;
                pos.y = Mathf.Lerp(pos.y, 0f, Time.deltaTime * 10f);
                transform.position = pos;
            }
        }
    }
}
