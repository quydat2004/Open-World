using UnityEngine;

namespace HueCity.Vehicle
{
    /// <summary>
    /// Điều khiển vật lý xe hơi sử dụng WheelCollider tiêu chuẩn Unity.
    /// Hỗ trợ truyền động cầu sau (RWD) hoặc 4 bánh (AWD), phanh và chống lật (Anti-Roll Bar).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class CarController : MonoBehaviour
    {
        [System.Serializable]
        public struct WheelPair
        {
            public WheelCollider leftWheel;
            public WheelCollider rightWheel;
            public Transform leftMesh;
            public Transform rightMesh;
        }

        [Header("Wheel Configuration")]
        [SerializeField] private WheelPair frontWheels;
        [SerializeField] private WheelPair rearWheels;

        [Header("Engine & Handling")]
        [SerializeField] private float motorTorque = 1600f;
        [SerializeField] private float brakeTorque = 3000f;
        [SerializeField] private float maxSteerAngle = 32f;
        [SerializeField] private float maxSpeedKmh = 90f;

        [Header("Physics & Stability")]
        [SerializeField] private Vector3 centerOfMassOffset = new Vector3(0f, -0.45f, 0f);
        [SerializeField] private float antiRollForce = 4500f;

        private Rigidbody rb;
        private float currentThrottle;
        private float currentSteer;
        private bool isBraking;

        public float CurrentSpeedKmh => rb != null ? rb.velocity.magnitude * 3.6f : 0f;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.centerOfMass += centerOfMassOffset;
        }

        public void SetInput(float throttle, float steer, bool brake)
        {
            currentThrottle = throttle;
            currentSteer = steer;
            isBraking = brake;
        }

        public void StopCar()
        {
            currentThrottle = 0f;
            currentSteer = 0f;
            isBraking = true;
            ApplyBrakeTorque(brakeTorque);
        }

        private void FixedUpdate()
        {
            HandleSteering();
            HandleMotorAndBrakes();
            ApplyAntiRollBar(frontWheels.leftWheel, frontWheels.rightWheel);
            ApplyAntiRollBar(rearWheels.leftWheel, rearWheels.rightWheel);
        }

        private void Update()
        {
            UpdateWheelMeshes();
        }

        private void HandleSteering()
        {
            float steerAngle = currentSteer * maxSteerAngle;
            if (frontWheels.leftWheel != null) frontWheels.leftWheel.steerAngle = steerAngle;
            if (frontWheels.rightWheel != null) frontWheels.rightWheel.steerAngle = steerAngle;
        }

        private void HandleMotorAndBrakes()
        {
            float speed = CurrentSpeedKmh;

            if (isBraking)
            {
                ApplyBrakeTorque(brakeTorque);
                ApplyMotorTorque(0f);
            }
            else
            {
                ApplyBrakeTorque(0f);

                // Giới hạn tốc độ tối đa
                if (speed < maxSpeedKmh)
                {
                    ApplyMotorTorque(currentThrottle * motorTorque);
                }
                else
                {
                    ApplyMotorTorque(0f);
                }
            }
        }

        private void ApplyMotorTorque(float torque)
        {
            // Dẫn động cầu sau (hoặc 4 bánh nếu cần)
            if (rearWheels.leftWheel != null) rearWheels.leftWheel.motorTorque = torque;
            if (rearWheels.rightWheel != null) rearWheels.rightWheel.motorTorque = torque;
        }

        private void ApplyBrakeTorque(float torque)
        {
            if (frontWheels.leftWheel != null) frontWheels.leftWheel.brakeTorque = torque * 0.7f;
            if (frontWheels.rightWheel != null) frontWheels.rightWheel.brakeTorque = torque * 0.7f;
            if (rearWheels.leftWheel != null) rearWheels.leftWheel.brakeTorque = torque * 0.3f;
            if (rearWheels.rightWheel != null) rearWheels.rightWheel.brakeTorque = torque * 0.3f;
        }

        private void ApplyAntiRollBar(WheelCollider left, WheelCollider right)
        {
            if (left == null || right == null) return;

            WheelHit hit;
            float travelL = 1.0f;
            float travelR = 1.0f;

            bool groundedL = left.GetGroundHit(out hit);
            if (groundedL)
            {
                travelL = (-left.transform.InverseTransformPoint(hit.point).y - left.radius) / left.suspensionDistance;
            }

            bool groundedR = right.GetGroundHit(out hit);
            if (groundedR)
            {
                travelR = (-right.transform.InverseTransformPoint(hit.point).y - right.radius) / right.suspensionDistance;
            }

            float antiRoll = (travelL - travelR) * antiRollForce;

            if (groundedL)
            {
                rb.AddForceAtPosition(left.transform.up * -antiRoll, left.transform.position);
            }

            if (groundedR)
            {
                rb.AddForceAtPosition(right.transform.up * antiRoll, right.transform.position);
            }
        }

        private void UpdateWheelMeshes()
        {
            SyncWheelMesh(frontWheels.leftWheel, frontWheels.leftMesh);
            SyncWheelMesh(frontWheels.rightWheel, frontWheels.rightMesh);
            SyncWheelMesh(rearWheels.leftWheel, rearWheels.leftMesh);
            SyncWheelMesh(rearWheels.rightWheel, rearWheels.rightMesh);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.relativeVelocity.magnitude > 4.5f)
            {
                HueCity.NPC.WorldStimulusManager.EmitStimulus(collision.contacts[0].point, 28f, HueCity.NPC.StimulusType.CarCrash, gameObject);
            }
        }

        private void SyncWheelMesh(WheelCollider col, Transform mesh)
        {
            if (col == null || mesh == null) return;

            col.GetWorldPose(out Vector3 pos, out Quaternion rot);
            mesh.position = pos;
            mesh.rotation = rot;
        }
    }
}
