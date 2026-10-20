using UnityEngine;

namespace TapriTycoon
{
    /// <summary>
    /// Animates the passing green-and-yellow 3D Indian Auto-Rickshaw along the back highway road
    /// with engine chassis vibration and wheel rotation.
    /// </summary>
    public class AutoRickshaw : MonoBehaviour
    {
        [Header("Road Movement")]
        public float driveSpeed = 5.2f;
        public float startX = -18f;
        public float resetX = 18f;
        public float roadZ = -6.8f;

        [Header("Chassis Bounce & Wheels")]
        public float engineBounceSpeed = 18f;
        public float engineBounceAmplitude = 0.03f;
        public Transform[] wheels;

        private float _baseY;

        private void Start()
        {
            _baseY = transform.position.y;
        }

        private void Update()
        {
            Vector3 pos = transform.position;
            pos.x += driveSpeed * Time.deltaTime;
            pos.z = roadZ;
            pos.y = _baseY + Mathf.Abs(Mathf.Sin(Time.time * engineBounceSpeed)) * engineBounceAmplitude;

            if (pos.x > resetX)
            {
                pos.x = startX;
            }

            transform.position = pos;

            if (wheels != null)
            {
                float wheelAngle = driveSpeed * 120f * Time.deltaTime;
                for (int i = 0; i < wheels.Length; i++)
                {
                    if (wheels[i] != null)
                    {
                        wheels[i].Rotate(Vector3.right, wheelAngle, Space.Self);
                    }
                }
            }
        }
    }
}
