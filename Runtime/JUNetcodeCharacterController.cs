using System.Collections;
using JUTPS;
using JUTPS.CameraSystems;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

public class JUNetcodeCharacterController : NetworkBehaviour
{
    public struct IKTransformData : INetworkSerializable, System.IEquatable<IKTransformData>
    {
        public Vector3 Position;
        public Vector3 Rotation;

        public bool Equals(IKTransformData other)
        {
            return Position == other.Position &&
                   Rotation == other.Rotation;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Position);
            serializer.SerializeValue(ref Rotation);
        }
    }

    private JUCharacterController _tps;
    private NetworkVariable<int> _leftItemId = new NetworkVariable<int>(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<int> _rightItemId = new NetworkVariable<int>(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private NetworkVariable<Vector3> _lookAtPosition = new(Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private NetworkVariable<float> _lookWeightIK = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<float> _armsWeightIK = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<float> _leftHandWeightIK = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<float> _rightHandWeightIK = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private NetworkVariable<IKTransformData> _leftHandIK = new(new IKTransformData(), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<IKTransformData> _rightHandIK = new(new IKTransformData(), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    public GameObject UserInterfacePrefab;
    public TPSCameraController CameraControllerPrefab;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        _tps = gameObject.GetComponent<JUCharacterController>();
        if (IsOwner)
        {
            if (_tps.IsPlayer)
            {
                Instantiate(UserInterfacePrefab);
                var cameraController = Instantiate(CameraControllerPrefab);
                _tps.MyPivotCamera = cameraController;
            }
        }
        else
        {
            _tps.tag = "Untagged";
            _tps.enabled = false;
            _tps.MyPivotCamera = null;
            _tps.UseDefaultControllerInput = false;

            StartCoroutine(InitialSwitch());

            _tps.IKPositionLeftHand.SetParent(_tps.transform);
            _tps.IKPositionRightHand.SetParent(_tps.transform);
            _tps.LeftHandIKPositionTarget.SetParent(_tps.transform);
            _tps.RightHandIKPositionTarget.SetParent(_tps.transform);
        }

        _leftItemId.OnValueChanged += OnItemSwitched;
        _rightItemId.OnValueChanged += OnItemSwitched;
    }

    public override void OnNetworkDespawn()
    {
        _leftItemId.OnValueChanged -= OnItemSwitched;
        _rightItemId.OnValueChanged -= OnItemSwitched;
    }

    private void Update()
    {
        UpdateIfOwner();
        UpdateIfNonOwner();
    }

    private void UpdateIfOwner()
    {
        if (!IsOwner)
            return;

        // Updateitem in use.
        if (!_tps.HoldableItemInUseRightHand)
            _rightItemId.Value = -1;
        else
            _rightItemId.Value = _tps.HoldableItemInUseRightHand.ItemSwitchID;

        // Update animator IK weights.
        _lookWeightIK.Value = _tps.LookWeightIK;
        _armsWeightIK.Value = _tps.ArmsWeightIK;
        _leftHandWeightIK.Value = _tps.LeftHandWeightIK;
        _rightHandWeightIK.Value = _tps.RightHandWeightIK;

        // Update IK positions for hands.
        var leftHandPos = _tps.IKPositionLeftHand.position;
        var leftHandRot = _tps.IKPositionLeftHand.eulerAngles;
        var rightHandPos = _tps.IKPositionRightHand.position;
        var rightHandRot = _tps.IKPositionRightHand.eulerAngles;

        leftHandPos = _tps.transform.InverseTransformPoint(leftHandPos);
        rightHandPos = _tps.transform.InverseTransformPoint(rightHandPos);

        _leftHandIK.Value = new IKTransformData()
        {
            Position = leftHandPos,
            Rotation = leftHandRot,
        };

        _rightHandIK.Value = new IKTransformData()
        {
            Position = rightHandPos,
            Rotation = rightHandRot,
        };

        _lookAtPosition.Value = _tps.GetLookPosition();
    }

    private void UpdateIfNonOwner()
    {
        if (IsOwner)
            return;

        _tps.IKPositionLeftHand.localPosition = _leftHandIK.Value.Position;
        _tps.IKPositionLeftHand.eulerAngles = _leftHandIK.Value.Rotation;
        _tps.LeftHandIKPositionTarget.localPosition = _tps.IKPositionLeftHand.localPosition;
        _tps.LeftHandIKPositionTarget.localEulerAngles = _tps.IKPositionLeftHand.localEulerAngles;

        _tps.IKPositionRightHand.localPosition = _rightHandIK.Value.Position;
        _tps.IKPositionRightHand.eulerAngles = _rightHandIK.Value.Rotation;
        _tps.RightHandIKPositionTarget.localPosition = _tps.IKPositionRightHand.localPosition;
        _tps.RightHandIKPositionTarget.localEulerAngles = _tps.IKPositionRightHand.localEulerAngles;

        _tps.LookAtPosition = _lookAtPosition.Value;
    }

    void OnAnimatorIK(int layerIndex)
    {
        if (IsOwner)
            return;

        // Apply IK weights for non-owner characters.

        _tps.LeftHandWeightIK = _leftHandWeightIK.Value;
        _tps.RightHandWeightIK = _rightHandWeightIK.Value;
        _tps.LookWeightIK = _lookWeightIK.Value;
        _tps.ArmsWeightIK = _armsWeightIK.Value;

        if (_tps.IsDead || _tps.InverseKinematics == false)
            return;

        if (_tps.IsRolling == false && _tps.IsDriving == false)
        {
            _tps.LeftHandToRespectiveIKPosition(_tps.LeftHandWeightIK, _tps.LeftHandWeightIK * _tps.LeftElbowAdjustWeight);
            _tps.RightHandToRespectiveIKPosition(_tps.RightHandWeightIK, _tps.RightHandWeightIK * _tps.RightElbowAdjustWeight);

            Vector3 LookingPosition = _tps.GetLookPosition();

            // Body Look At IK
            float ProneBodyWeight = (_tps.LookAtBodyWeight == 0) ? 0 : 0.1f;
            float BodyWeight = _tps.IsProne ? ProneBodyWeight : _tps.LookAtBodyWeight;

            float LookingIntensity = Vector3.Dot(transform.forward, (LookingPosition - transform.position).normalized);
            _tps.LookAtIK(LookingPosition, LookingIntensity * _tps.LookWeightIK, BodyWeight, _tps.HeadIKBodyWeight);
        }
    }

    private void OnItemSwitched(int previousValue, int newValue)
    {
        _tps.SwitchToItem(_rightItemId.Value, true);
    }

    private IEnumerator InitialSwitch()
    {
        yield return new WaitForSeconds(0.2f);
        _tps.SwitchToItem(_rightItemId.Value, true);
    }
}
