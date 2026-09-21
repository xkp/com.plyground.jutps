using UnityEngine;
using UnityEditor;
using JUTPS;
using JUTPS.PhysicsScripts;
using JUTPS.InventorySystem;
using JU;
using JUTPS.FX;
using JUTPS.WeaponSystem;
using JU.CharacterSystem.AI;
using Unity.Netcode;
using Unity.Netcode.Components;
using JUTPS.CameraSystems;
using Unity.Netcode.Transports.UTP;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Plyground.JUTPS
{
    public static class PlygroundJUTPS_Core
    {
        public const int CharacterLayer = 9;

        public static readonly string TpsCameraControllerPrefabPath = "Assets/Julhiecio TPS Controller/Prefabs/Game/Camera Prefabs/ThirdPerson Camera Controller.prefab";
        public static readonly string UserInterfacePrefabPath = "Assets/Julhiecio TPS Controller/Prefabs/Game/UI Interfaces/JUTPS Default User Interface.prefab";

        public static void SetupNetcodeScene()
        {
            var singleton = NetworkManager.Singleton;

            if (!singleton)
            {
                singleton = GameObject.FindFirstObjectByType<NetworkManager>();
                if (!singleton)
                {
                    singleton = new GameObject("NetworkManager").AddComponent<NetworkManager>();
                    SetDirty(singleton);
                    SaveAssets();
                }
            }

            singleton.SetSingleton();

            if (singleton.NetworkConfig == null)
            {
                singleton.NetworkConfig = new NetworkConfig();
            }

            if (!singleton.NetworkConfig.NetworkTransport)
            {
                singleton.NetworkConfig.NetworkTransport = singleton.gameObject.AddComponent<UnityTransport>();
            }

            SetDirty(singleton.gameObject.scene);
            SaveAssets();
        }

        public static JUCharacterController AddNetcodeToCharacter(JUCharacterController tps)
        {
            SetupNetcodeScene();

            var netObj = GetOrAddComponent<NetworkObject>(tps.gameObject);
            var netTransform = GetOrAddComponent<NetworkTransform>(tps.gameObject);
            var netRigidbody = GetOrAddComponent<NetworkRigidbody>(tps.gameObject);
            var netAnimator = GetOrAddComponent<NetworkAnimator>(tps.gameObject);
            var netController = GetOrAddComponent<JUNetcodeCharacterController>(tps.gameObject);

            netAnimator.AuthorityMode = NetworkAnimator.AuthorityModes.Owner;
            netTransform.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
            netAnimator.Animator = tps.anim;

            netController.UserInterfacePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(UserInterfacePrefabPath);
            netController.CameraControllerPrefab = AssetDatabase.LoadAssetAtPath<TPSCameraController>(TpsCameraControllerPrefabPath);

            SetDirty(tps.gameObject);
            SaveAssets();

            return tps;
        }

        public static JUCharacterController BuildZombieCharacter(GameObject gameObject)
        {
            var tps = BuildCharacter(gameObject);
            var zombie = GetOrAddComponent<JU_AI_Zombie>(tps.gameObject);

            zombie.PatrolRandomlyIfNotHavePath = true;
            zombie.NavigationSettings.Mode = JUCharacterAIBase.NavigationModes.Simple;
            zombie.Head = null;

            SetDirty(gameObject);
            SaveAssets();

            return tps;
        }

        public static JUCharacterController BuildPatrolCharacter(GameObject gameObject)
        {
            var tps = BuildCharacter(gameObject);
            var patrol = GetOrAddComponent<JU_AI_PatrolCharacter>(tps.gameObject);

            patrol.PatrolRandomlyIfNotHavePath = true;
            patrol.NavigationSettings.Mode = JUCharacterAIBase.NavigationModes.Simple;
            patrol.Head = null;

            SetDirty(gameObject);
            SaveAssets();

            return tps;
        }

        public static JUCharacterController BuildCharacter(GameObject character)
        {
            if (!character)
            {
                Debug.LogError("There is no character model to build.");
                return null;
            }

            var anim = character.GetComponent<Animator>();
            if (!anim)
            {
                Debug.LogError($"The character model {character.name} does not have an ${typeof(Animator).Name}.");
                return null;
            }

            if (!anim.isHuman)
            {
                Debug.LogError($"The {typeof(Animator).Name} is not a Humanoid model.");
                return null;
            }

            if (!anim.avatar)
            {
                Debug.LogError($"The {typeof(Animator).Name} does not have an {typeof(Avatar).Name}.");
                return null;
            }

            // WIP
            // Fix character scale
            // {
            // 	var prefabParent = PrefabUtility.GetCorrespondingObjectFromSource(character);
            // 	var assetPath = AssetDatabase.GetAssetPath(prefabParent);
            // 	var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;


            // 	Debug.Assert(false, $"importer {importer}     importer type {importer?.GetType()?.Name}   asset path {assetPath}     prefabParent {prefabParent}");

            // 	var currentHeight = 2.0f;
            // 	var targetHeight = 1.8f;

            // 	// Get character size.
            // 	{
            // 		var renderers = character.GetComponentsInChildren<Renderer>();
            // 		var bounds = new Bounds();
            // 		for (int i = 0; i < renderers.Length; i++)
            // 		{
            // 			if (i == 0)
            // 			{
            // 				bounds = renderers[i].bounds;
            // 			}
            // 			else
            // 			{
            // 				bounds.Encapsulate(renderers[i].bounds);
            // 			}
            // 		}

            // 		currentHeight = bounds.size.y;
            // 	}


            // 	float currentGlobalScale = importer.globalScale;
            // 	float newScaleFactor = currentGlobalScale * (targetHeight / currentHeight);

            // 	importer.globalScale = newScaleFactor;
            // 	importer.SaveAndReimport();

            // 	// Opcional: Garante que a instância na cena esteja na escala neutra (1,1,1)
            // 	character.transform.localScale = Vector3.one;
            // }

            var animatorAssetPath = "Assets/Julhiecio TPS Controller/Animations/Animator/AnimatorTPS Controller.controller";

            var col = GetOrAddComponent<CapsuleCollider>(character);
            var noSlipMaterial = (PhysicsMaterial)Resources.Load("NoSlip", typeof(PhysicsMaterial));
            col.material = noSlipMaterial;

            var resizableCapsuleCollder = GetOrAddComponent<ResizableCapsuleCollider>(character);
            var rb = GetOrAddComponent<Rigidbody>(character);
            var footPlacement = GetOrAddComponent<JUFootPlacement>(character);
            var tps = GetOrAddComponent<JUCharacterController>(character);
            var ragdoll = GetOrAddComponent<AdvancedRagdollController>(character);
            var inventory = GetOrAddComponent<JUInventory>(character);
            var health = GetOrAddComponent<JUHealth>(character);
            //GetOrAddComponent<JU3HealthMessageBridge>(character);

            var animatorAsset = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(animatorAssetPath);

            anim.runtimeAnimatorController = animatorAsset;

            character.layer = CharacterLayer;

            var weaponCenter = CharacterCreateNewWeaponRotationCenter(character.transform);
            tps.PivotItemRotation = weaponCenter.gameObject;

            tps.Speed = 3;
            tps.CurvedMovement = true;
            tps.RotationSpeed = 3;
            tps.LerpRotation = true;
            tps.StoppingSpeed = 4;
            tps.RootMotion = true;
            tps.UnlimitedSprintDuration = true;

            rb.mass = 85;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.constraints = RigidbodyConstraints.FreezeRotation;

            col.height = 1.7f;
            col.center = new Vector3(0, 0.85f, 0);
            col.radius = 0.4f;

            health.SetMaxHealth(200);
            health.SetHealth(200);

            var footStep = GetOrAddComponent<JUFootstep>(character);
            footStep.LoadDefaultFootstepInInspector();

            GetOrAddComponent<BodyLeanInert>(character);
            GetOrAddComponent<ProceduralDrivingAnimation>(character);
            GetOrAddComponent<AudioSource>(character);

            SetupHitBoxes(tps);

            return tps;
        }

        private static WeaponAimRotationCenter CharacterCreateNewWeaponRotationCenter(Transform parent)
        {
            var ItemWieldPivotRotation = new GameObject("Item Wield Rotation Center");

            WeaponAimRotationCenter center = ItemWieldPivotRotation.AddComponent<WeaponAimRotationCenter>();
            ItemWieldPivotRotation.transform.position = parent.position + ItemWieldPivotRotation.transform.up;
            ItemWieldPivotRotation.transform.SetParent(parent);

            var WeaponPositionsParent = new GameObject("Item Wielding Hands Positions");
            WeaponPositionsParent.transform.position = ItemWieldPivotRotation.transform.position;
            WeaponPositionsParent.transform.parent = ItemWieldPivotRotation.transform;

            var smallWeaponPivot = parent.Find("Small Weapon Pivot");
            var smallLeftWeaponPivot = parent.Find("Small Left Weapon Pivot");
            var bigWeaponPivot = parent.Find("Big Weapon Pivot");
            var flashLightPivot = parent.Find("Flash Light Pivot");

            center.CreateWeaponPositionReference("Small Weapon Position Reference");

            if (smallWeaponPivot)
            {
                center.WeaponPositionTransform[0].position = smallWeaponPivot.position;
                center.WeaponPositionTransform[0].rotation = smallWeaponPivot.rotation;
            }
            else
            {
                center.WeaponPositionTransform[0].localPosition = new Vector3(0.212f, 0.227f, 0.407f);
                center.WeaponPositionTransform[0].localRotation = Quaternion.Euler(-8.626f, 12.322f, -84.111f);
            }

            center.CreateWeaponPositionReference("Big Weapon Position Reference");

            if (bigWeaponPivot)
            {
                center.WeaponPositionTransform[1].position = bigWeaponPivot.position;
                center.WeaponPositionTransform[1].rotation = bigWeaponPivot.rotation;
            }
            else
            {
                center.WeaponPositionTransform[1].localPosition = new Vector3(0.207000241f, 0.14000012f, 0.239999995f);
                center.WeaponPositionTransform[1].localRotation = Quaternion.Euler(0, 11.383f, -94.913f);
            }

            center.CreateWeaponPositionReference("Flash Light");

            if (flashLightPivot)
            {
                center.WeaponPositionTransform[2].position = flashLightPivot.position;
                center.WeaponPositionTransform[2].rotation = flashLightPivot.rotation;
            }
            else
            {
                center.WeaponPositionTransform[2].localPosition = new Vector3(0.302f, 0.167f, 0.258f);
                center.WeaponPositionTransform[2].localRotation = Quaternion.Euler(-81.350f, -33.581f, -49.971f);
            }

            center.CreateWeaponPositionReference("Left Hand Small Weapon Position");
            if (smallLeftWeaponPivot)
            {
                center.WeaponPositionTransform[3].position = smallLeftWeaponPivot.position;
                center.WeaponPositionTransform[3].rotation = smallLeftWeaponPivot.rotation;
            }
            else
            {
                center.WeaponPositionTransform[3].localPosition = new Vector3(-0.239f, 0.233f, 0.489f);
                center.WeaponPositionTransform[3].localRotation = Quaternion.Euler(355.53f, 349.75f, 95.66f);
            }

            // center.CreateWeaponPositionReference("Small Gun Prevent Cliping");
            // center.WeaponPositionTransform[3].localPosition = new Vector3(0.223f, 0.081f, 0.22f);
            // center.WeaponPositionTransform[3].localRotation = Quaternion.Euler(-80.399f, -267.951f, 178.884f);

            // center.CreateWeaponPositionReference("Big Gun Prevent Clipping");
            // center.WeaponPositionTransform[3].localPosition = new Vector3(0.217f, 0.046f, 0.259f);
            // center.WeaponPositionTransform[3].localRotation = Quaternion.Euler(-83.967f, -349.849f, 228.624f);

            center.StoreLocalTransform();

            SetDirty(center);

            return center;
        }

        private static void SetupHitBoxes(JUCharacterController tps)
        {
            if (!Resources.Load("HitBox"))
            {
                JUTPSEditor.MessageWindow.ShowMessage("HITBOX setup could not be done because cannot load HitBox prefab from Resources folder", "Error", "OK", 125, 276, 14, MessageType.Error);
                return;
            }

            GameObject HitBoxPrefab = Resources.Load("HitBox") as GameObject;

            Animator anim = tps.GetComponent<Animator>();
            if (anim == null)
            {
                JUTPSEditor.MessageWindow.ShowMessage("HITBOX setup could not be done because there's no Animator in the selected GameObject", "Error", "OK", 125, 276, 14, MessageType.Error);
                return;
            }
            else
            {
                //Humanoid error
                if (anim.isHuman == false)
                {
                    Debug.LogError("Your character rig is not HUMANOID type, please use a humanoid type character.", anim);
                    JUTPSEditor.MessageWindow.ShowMessage("Your character rig is not HUMANOID type, please use a humanoid type character.", "Setup could not be done", "OK", 125, 276, 14, MessageType.Error);
                    return;
                }
            }

            var leftArm = anim.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            var rightArm = anim.GetBoneTransform(HumanBodyBones.RightLowerArm);
            var leftKnee = anim.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            var rightKnee = anim.GetBoneTransform(HumanBodyBones.RightLowerLeg);

            var leftHand = anim.GetBoneTransform(HumanBodyBones.LeftHand);
            var rightHand = anim.GetBoneTransform(HumanBodyBones.RightHand);
            var leftFoot = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
            var rightFoot = anim.GetBoneTransform(HumanBodyBones.RightFoot);

            if (leftArm.GetComponentInChildren<Damager>(true) == false)
            {
                var leftArmHitBox = (GameObject)PrefabUtility.InstantiatePrefab(HitBoxPrefab, leftArm);
                leftArmHitBox.transform.localPosition = Vector3.zero;
                leftArmHitBox.transform.localRotation = Quaternion.identity;
                leftArmHitBox.transform.position = leftHand.position;
                Undo.RegisterCreatedObjectUndo(leftArmHitBox, "Hit Box Setup");
            }

            if (rightArm.GetComponentInChildren<Damager>(true) == false)
            {
                var rightArmHitBox = (GameObject)PrefabUtility.InstantiatePrefab(HitBoxPrefab, rightArm);
                rightArmHitBox.transform.localPosition = Vector3.zero;
                rightArmHitBox.transform.localRotation = Quaternion.identity;
                rightArmHitBox.transform.position = rightHand.position;
                Undo.RegisterCreatedObjectUndo(rightArmHitBox, "Hit Box Setup");

            }

            if (leftKnee.GetComponentInChildren<Damager>(true) == false)
            {
                var leftLegHitBox = (GameObject)PrefabUtility.InstantiatePrefab(HitBoxPrefab, leftKnee);
                leftLegHitBox.transform.localPosition = Vector3.zero;
                leftLegHitBox.transform.localRotation = Quaternion.identity;
                leftLegHitBox.transform.position = leftFoot.position;
                Undo.RegisterCreatedObjectUndo(leftLegHitBox, "Hit Box Setup");

            }

            if (rightKnee.GetComponentInChildren<Damager>(true) == false)
            {
                var rightLegHitBox = (GameObject)PrefabUtility.InstantiatePrefab(HitBoxPrefab, rightKnee);
                rightLegHitBox.transform.localPosition = Vector3.zero;
                rightLegHitBox.transform.localRotation = Quaternion.identity;
                rightLegHitBox.transform.position = rightFoot.position;
                Undo.RegisterCreatedObjectUndo(rightLegHitBox, "Hit Box Setup");
            }
        }

        public static T SpawnItemWithPivot<T>(Transform hand, GameObject weapon, Vector3 pos, Vector3 euler, Vector3 scale)
        {
            var newWeapon = (GameObject)PrefabUtility.InstantiatePrefab(weapon, hand.gameObject.scene);
            newWeapon.transform.SetParent(hand.transform, true);
            newWeapon.transform.position = pos;
            newWeapon.transform.eulerAngles = euler;
            newWeapon.transform.localScale = scale;

            return newWeapon.GetComponent<T>();
        }

        public static T SpawnWithoutPivot<T>(GameObject gameObject, GameObject weapon, Vector3 pos, Vector3 euler, Vector3 scale, bool rightHand = true)
        {
            var anim = gameObject.GetComponent<Animator>();
            var rightForeArm = anim.GetBoneTransform(HumanBodyBones.RightLowerArm);
            var hand = anim.GetBoneTransform(rightHand ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);

            var rightForeArmSize = Vector3.Distance(rightForeArm.position, hand.position);
            var localPos = Vector3.zero;

            localPos += Vector3.forward * rightForeArmSize * pos.z;
            localPos += Vector3.right * rightForeArmSize * pos.x;
            localPos += Vector3.up * rightForeArmSize * pos.y;

            var newWeapon = (GameObject)PrefabUtility.InstantiatePrefab(weapon, gameObject.scene);
            newWeapon.transform.SetParent(hand, true);
            newWeapon.transform.localPosition = localPos;
            newWeapon.transform.localEulerAngles = euler;
            newWeapon.transform.localScale = scale;

            return newWeapon.GetComponent<T>();
        }

        public static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
        {
            if (gameObject.TryGetComponent<T>(out var component))
                return component;

            return gameObject.AddComponent<T>();
        }

        public static GameObject SavePrefab(GameObject gameObject, string path)
        {
            var fullPath = $"Assets/plyground/Generated/{path}/{gameObject.name}.prefab";
            var directory = System.IO.Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
                System.IO.Directory.CreateDirectory(directory);

            return PrefabUtility.SaveAsPrefabAsset(gameObject, fullPath);
        }

        public static void SetDirty(Scene scene)
        {
#if UNITY_EDITOR
            EditorSceneManager.MarkSceneDirty(scene);
#endif
        }

        public static void SetDirty(UnityEngine.Object obj)
        {
#if UNITY_EDITOR
            EditorUtility.SetDirty(obj);
#endif
        }

        public static void SaveAssets()
        {
#if UNITY_EDITOR
            AssetDatabase.SaveAssets();
#endif
        }

        public static void RunMethod(Object obj, string methodName)
        {
            var type = obj.GetType();
            var method = type.GetMethod(methodName);
            Debug.Assert(method != null, $"Method '{methodName}' not found on object of type '{type.FullName}'");
            method.Invoke(obj, null);
        }
    }
}

