using System.Collections.Generic;
using UnityEngine;

// This class is used as a workaround to initialize the Hand prefab data at runtime
// in the Valheim mod. It seems that serialized data for a prefab is tied to a
// scene, so the data isn't brought along when moving the AssetBundle outside
// of this project and loading it in at runtime in Valheim.
// So I copied all the data from the asset files directly into this
// code and manually copy it over at runtime just in time to when it is needed.
// It's super hacky and very tedious to copy the data over, but it should only
// be a one time thing and I don't have a good solution to move the data over
// in the asset bundles and have it auto-deserialize into the objects like
// it does if used in the same Unity project that it's built in.
// 
// This is being called from SteamVR_Skeleton_Poser.Awake().
// The data were all copied from these files:
// SteamVR/InteractionSystem/Poses/fallback_relaxed.asset
// SteamVR/InteractionSystem/Poses/fallback_point.asset
// SteamVR/InteractionSystem/Poses/fallback_fist.asset
//
// I'm not really sure this was actually important data to have copied
// over, but not having it populated was resulting in NullReference
// Exceptions and I didn't want to deal with any potential side effects.
namespace Valve.VR
{

    public class HandResourceInitializer 
    {

        public static readonly string LEFT_HAND = "LeftHand";
        public static readonly string RIGHT_HAND = "RightHand";

        public static void Initialize_SteamVR_Skeleton_Pose_Hand(ref SteamVR_Skeleton_Pose_Hand p, int index, string hand) {
            Debug.Log("Initialize_SteamVR_Skeleton_Pose_Hand - PoseIndex: " + index + " Hand: " + hand);
            string poseName = poseIndexToName[index];
            string poseKey = poseName + ":" + hand;
            if (poseToData.ContainsKey(poseKey)) {
                Debug.Log("Have Pose Data, populating SteamVR_Skeleton_Pose_Hand");
                PoseDat d = poseToData[poseKey];
                PopulatePoseData(ref p, d);
            } else {
                Debug.LogError("Did not have pose data for pose index: " + index + " and hand: " + hand);
            }
        }

        private static void PopulatePoseData(ref SteamVR_Skeleton_Pose_Hand p, PoseDat d) {
            p.thumbFingerMovementType = d.thumbFingerMovementType;
            p.indexFingerMovementType = d.indexFingerMovementType;
            p.middleFingerMovementType = d.middleFingerMovementType;
            p.ringFingerMovementType = d.ringFingerMovementType;
            p.ignoreRootPoseData = d.ignoreRootPoseData;
            p.ignoreWristPoseData = d.ignoreWristPoseData;
            p.position = d.position;
            p.rotation = d.rotation;
            p.bonePositions = d.bonePositions;
            p.boneRotations = d.boneRotations;
        }

        private static Dictionary<int, string> poseIndexToName = new Dictionary<int, string>()
        {
            {0, "fallback_relaxed"},
            {1, "fallback_point"},
            {2, "fallback_fist"}
        };

        private static Dictionary<string, PoseDat> poseToData = new Dictionary<string, PoseDat>()
        {
            {"fallback_relaxed:" + LEFT_HAND,  LeftHandFallbackRelaxed()},
            {"fallback_relaxed:" + RIGHT_HAND, RightHandFallbackRelaxed()},
            {"fallback_point:" + LEFT_HAND, LeftHandFallbackPoint()},
            {"fallback_point:" + RIGHT_HAND, RightHandFallbackPoint()},
            {"fallback_fist:" + LEFT_HAND, LeftHandFallbackFist()},
            {"fallback_fist:" + RIGHT_HAND, RightHandFallbackFist()}
        };

        private static PoseDat LeftHandFallbackFist() {
            PoseDat pd = new PoseDat();
            pd.thumbFingerMovementType = 0;
            pd.indexFingerMovementType = 0;
            pd.middleFingerMovementType = 0;
            pd.ringFingerMovementType = 0;
            pd.ignoreRootPoseData = true;
            pd.ignoreWristPoseData = true;
            pd.position = new Vector3(0.000000059604645f, 0f, 0f);
            pd.rotation = new Quaternion(0f, -0f, -0f, -1f);
            pd.bonePositions = new Vector3[] {
                new Vector3(x: -0f, y: 0f, z: 0f),
                new Vector3(x: -0.034037687f, y: 0.03650266f, z: 0.16472164f),
                new Vector3(x: -0.016305087f, y: 0.027528726f, z: 0.017799662f),
                new Vector3(x: 0.040405963f, y: -0.000000051561553f, z: 0.000000045447194f),
                new Vector3(x: 0.032516792f, y: -0.000000051137583f, z: -0.000000012933195f),
                new Vector3(x: 0.030463902f, y: 0.00000016269207f, z: 0.0000000792839f),
                new Vector3(x: 0.0038021489f, y: 0.021514187f, z: 0.012803366f),
                new Vector3(x: 0.074204385f, y: 0.005002201f, z: -0.00023377323f),
                new Vector3(x: 0.043286677f, y: 0.000000059333324f, z: 0.00000018320057f),
                new Vector3(x: 0.028275194f, y: -0.00000009297885f, z: -0.00000012653295f),
                new Vector3(x: 0.022821384f, y: -0.00000014365155f, z: 0.00000007651614f),
                new Vector3(x: 0.005786922f, y: 0.0068064053f, z: 0.016533904f),
                new Vector3(x: 0.07095288f, y: -0.00077883265f, z: -0.000997186f),
                new Vector3(x: 0.043108486f, y: -0.00000009950596f, z: -0.0000000067041825f),
                new Vector3(x: 0.03326598f, y: -0.000000017544496f, z: -0.000000020628962f),
                new Vector3(x: 0.025892371f, y: 0.00000009984198f, z: -0.0000000020352908f),
                new Vector3(x: 0.004123044f, y: -0.0068582613f, z: 0.016562859f),
                new Vector3(x: 0.06587581f, y: -0.0017857892f, z: -0.00069344096f),
                new Vector3(x: 0.040331207f, y: -0.00000009449958f, z: -0.00000002273692f),
                new Vector3(x: 0.028488781f, y: 0.000000101152565f, z: 0.000000045493586f),
                new Vector3(x: 0.022430236f, y: 0.00000010846127f, z: -0.000000017428562f),
                new Vector3(x: 0.0011314574f, y: -0.019294508f, z: 0.01542875f),
                new Vector3(x: 0.0628784f, y: -0.0028440945f, z: -0.0003315112f),
                new Vector3(x: 0.029874247f, y: -0.000000034247638f, z: -0.00000009126629f),
                new Vector3(x: 0.017978692f, y: -0.0000000028448923f, z: -0.00000020797508f),
                new Vector3(x: 0.01801794f, y: -0.0000000200012f, z: 0.0000000659746f),
                new Vector3(x: 0.019716311f, y: 0.002801723f, z: 0.093936935f),
                new Vector3(x: -0.0075385696f, y: 0.01764465f, z: 0.10240429f),
                new Vector3(x: -0.0031984635f, y: 0.0072115273f, z: 0.11665362f),
                new Vector3(x: 0.000026269245f, y: -0.007118772f, z: 0.13072418f),
                new Vector3(x: -0.0018780098f, y: -0.02256182f, z: 0.14003526f)
            };
            pd.boneRotations = new Quaternion[] {
                new Quaternion(x: -6.123234e-17f, y: 1f, z: 6.123234e-17f, w: -0.00000004371139f),
                new Quaternion(x: -0.078608155f, y: -0.92027926f, z: 0.3792963f, w: -0.055146642f),
                new Quaternion(x: -0.2257035f, y: -0.836342f, z: 0.12641343f, w: 0.48333195f),
                new Quaternion(x: -0.01330204f, y: 0.0829018f, z: -0.43944824f, w: 0.89433527f),
                new Quaternion(x: 0.00072834245f, y: -0.0012028969f, z: -0.58829284f, w: 0.80864674f),
                new Quaternion(x: -1.3877788e-17f, y: -1.3877788e-17f, z: -5.551115e-17f, w: 1f),
                new Quaternion(x: -0.6173145f, y: -0.44918522f, z: -0.5108743f, w: 0.39517453f),
                new Quaternion(x: -0.041852362f, y: 0.11180638f, z: -0.72633374f, w: 0.67689514f),
                new Quaternion(x: -0.0005700487f, y: 0.115204416f, z: -0.81729656f, w: 0.56458294f),
                new Quaternion(x: -0.010756178f, y: 0.027241308f, z: -0.66610956f, w: 0.7452787f),
                new Quaternion(x: 6.938894e-18f, y: 1.9428903e-16f, z: -1.348151e-33f, w: 1f),
                new Quaternion(x: -0.5142028f, y: -0.4836996f, z: -0.47834843f, w: 0.522315f),
                new Quaternion(x: -0.09487112f, y: -0.05422859f, z: -0.7229027f, w: 0.68225396f),
                new Quaternion(x: 0.0076794685f, y: -0.09769542f, z: -0.7635977f, w: 0.6382125f),
                new Quaternion(x: -0.06366954f, y: 0.00036316764f, z: -0.7530614f, w: 0.6548623f),
                new Quaternion(x: 1.1639192e-17f, y: -5.602331e-17f, z: -0.040125635f, w: 0.9991947f),
                new Quaternion(x: -0.489609f, y: -0.46399677f, z: -0.52064353f, w: 0.523374f),
                new Quaternion(x: -0.088269405f, y: 0.012672794f, z: -0.7085384f, w: 0.7000152f),
                new Quaternion(x: -0.0005935501f, y: -0.039828163f, z: -0.74642265f, w: 0.66427904f),
                new Quaternion(x: -0.027121458f, y: -0.005438834f, z: -0.7788164f, w: 0.62664175f),
                new Quaternion(x: 6.938894e-18f, y: -9.62965e-35f, z: -1.3877788e-17f, w: 1f),
                new Quaternion(x: -0.47976637f, y: -0.37993452f, z: -0.63019824f, w: 0.47783276f),
                new Quaternion(x: -0.094065815f, y: 0.062634066f, z: -0.69046116f, w: 0.7144873f),
                new Quaternion(x: 0.00313052f, y: 0.03775632f, z: -0.7113834f, w: 0.7017823f),
                new Quaternion(x: -0.008087321f, y: -0.003009417f, z: -0.7361885f, w: 0.6767216f),
                new Quaternion(x: 0f, y: 0f, z: 1.9081958e-17f, w: 1f),
                new Quaternion(x: -0.54886997f, y: 0.1177861f, z: -0.7578353f, w: 0.33249632f),
                new Quaternion(x: 0.13243657f, y: -0.8730836f, z: -0.45493412f, w: -0.114980996f),
                new Quaternion(x: 0.17098099f, y: -0.92266804f, z: -0.34507802f, w: -0.019245595f),
                new Quaternion(x: 0.15011512f, y: -0.952169f, z: -0.25831383f, w: -0.064137466f),
                new Quaternion(x: 0.07684197f, y: -0.97957754f, z: -0.18576658f, w: -0.0037347008f)
            };
            return pd;
        }

        private static PoseDat RightHandFallbackFist() {
            PoseDat pd = new PoseDat();
            pd.thumbFingerMovementType = 0;
            pd.indexFingerMovementType = 0;
            pd.middleFingerMovementType = 0;
            pd.ringFingerMovementType = 0;
            pd.ignoreRootPoseData = true;
            pd.ignoreWristPoseData = true;
            pd.bonePositions = new Vector3[] {
                new Vector3(x: -0f, y: 0f, z: 0f),
                new Vector3(x: -0.034037687f, y: 0.03650266f, z: 0.16472164f),
                new Vector3(x: -0.016305087f, y: 0.027528726f, z: 0.017799662f),
                new Vector3(x: 0.040405963f, y: -0.000000051561553f, z: 0.000000045447194f),
                new Vector3(x: 0.032516792f, y: -0.000000051137583f, z: -0.000000012933195f),
                new Vector3(x: 0.030463902f, y: 0.00000016269207f, z: 0.0000000792839f),
                new Vector3(x: 0.0038021489f, y: 0.021514187f, z: 0.012803366f),
                new Vector3(x: 0.074204385f, y: 0.005002201f, z: -0.00023377323f),
                new Vector3(x: 0.043286677f, y: 0.000000059333324f, z: 0.00000018320057f),
                new Vector3(x: 0.028275194f, y: -0.00000009297885f, z: -0.00000012653295f),
                new Vector3(x: 0.022821384f, y: -0.00000014365155f, z: 0.00000007651614f),
                new Vector3(x: 0.005786922f, y: 0.0068064053f, z: 0.016533904f),
                new Vector3(x: 0.07095288f, y: -0.00077883265f, z: -0.000997186f),
                new Vector3(x: 0.043108486f, y: -0.00000009950596f, z: -0.0000000067041825f),
                new Vector3(x: 0.03326598f, y: -0.000000017544496f, z: -0.000000020628962f),
                new Vector3(x: 0.025892371f, y: 0.00000009984198f, z: -0.0000000020352908f),
                new Vector3(x: 0.004123044f, y: -0.0068582613f, z: 0.016562859f),
                new Vector3(x: 0.06587581f, y: -0.0017857892f, z: -0.00069344096f),
                new Vector3(x: 0.040331207f, y: -0.00000009449958f, z: -0.00000002273692f),
                new Vector3(x: 0.028488781f, y: 0.000000101152565f, z: 0.000000045493586f),
                new Vector3(x: 0.022430236f, y: 0.00000010846127f, z: -0.000000017428562f),
                new Vector3(x: 0.0011314574f, y: -0.019294508f, z: 0.01542875f),
                new Vector3(x: 0.0628784f, y: -0.0028440945f, z: -0.0003315112f),
                new Vector3(x: 0.029874247f, y: -0.000000034247638f, z: -0.00000009126629f),
                new Vector3(x: 0.017978692f, y: -0.0000000028448923f, z: -0.00000020797508f),
                new Vector3(x: 0.01801794f, y: -0.0000000200012f, z: 0.0000000659746f),
                new Vector3(x: 0.019716311f, y: 0.002801723f, z: 0.093936935f),
                new Vector3(x: -0.0075385696f, y: 0.01764465f, z: 0.10240429f),
                new Vector3(x: -0.0031984635f, y: 0.0072115273f, z: 0.11665362f),
                new Vector3(x: 0.000026269245f, y: -0.007118772f, z: 0.13072418f),
                new Vector3(x: -0.0018780098f, y: -0.02256182f, z: 0.14003526f)
            };
            pd.boneRotations = new Quaternion[] {
                new Quaternion(x: -6.123234e-17f, y: 1f, z: 6.123234e-17f, w: -0.00000004371139f),
                new Quaternion(x: -0.078608155f, y: -0.92027926f, z: 0.3792963f, w: -0.055146642f),
                new Quaternion(x: -0.2257035f, y: -0.836342f, z: 0.12641343f, w: 0.48333195f),
                new Quaternion(x: -0.01330204f, y: 0.0829018f, z: -0.43944824f, w: 0.89433527f),
                new Quaternion(x: 0.00072834245f, y: -0.0012028969f, z: -0.58829284f, w: 0.80864674f),
                new Quaternion(x: -1.3877788e-17f, y: -1.3877788e-17f, z: -5.551115e-17f, w: 1f),
                new Quaternion(x: -0.6173145f, y: -0.44918522f, z: -0.5108743f, w: 0.39517453f),
                new Quaternion(x: -0.041852362f, y: 0.11180638f, z: -0.72633374f, w: 0.67689514f),
                new Quaternion(x: -0.0005700487f, y: 0.115204416f, z: -0.81729656f, w: 0.56458294f),
                new Quaternion(x: -0.010756178f, y: 0.027241308f, z: -0.66610956f, w: 0.7452787f),
                new Quaternion(x: 6.938894e-18f, y: 1.9428903e-16f, z: -1.348151e-33f, w: 1f),
                new Quaternion(x: -0.5142028f, y: -0.4836996f, z: -0.47834843f, w: 0.522315f),
                new Quaternion(x: -0.09487112f, y: -0.05422859f, z: -0.7229027f, w: 0.68225396f),
                new Quaternion(x: 0.0076794685f, y: -0.09769542f, z: -0.7635977f, w: 0.6382125f),
                new Quaternion(x: -0.06366954f, y: 0.00036316764f, z: -0.7530614f, w: 0.6548623f),
                new Quaternion(x: 1.1639192e-17f, y: -5.602331e-17f, z: -0.040125635f, w: 0.9991947f),
                new Quaternion(x: -0.489609f, y: -0.46399677f, z: -0.52064353f, w: 0.523374f),
                new Quaternion(x: -0.088269405f, y: 0.012672794f, z: -0.7085384f, w: 0.7000152f),
                new Quaternion(x: -0.0005935501f, y: -0.039828163f, z: -0.74642265f, w: 0.66427904f),
                new Quaternion(x: -0.027121458f, y: -0.005438834f, z: -0.7788164f, w: 0.62664175f),
                new Quaternion(x: 6.938894e-18f, y: -9.62965e-35f, z: -1.3877788e-17f, w: 1f),
                new Quaternion(x: -0.47976637f, y: -0.37993452f, z: -0.63019824f, w: 0.47783276f),
                new Quaternion(x: -0.094065815f, y: 0.062634066f, z: -0.69046116f, w: 0.7144873f),
                new Quaternion(x: 0.00313052f, y: 0.03775632f, z: -0.7113834f, w: 0.7017823f),
                new Quaternion(x: -0.008087321f, y: -0.003009417f, z: -0.7361885f, w: 0.6767216f),
                new Quaternion(x: 0f, y: 0f, z: 1.9081958e-17f, w: 1f),
                new Quaternion(x: -0.54886997f, y: 0.1177861f, z: -0.7578353f, w: 0.33249632f),
                new Quaternion(x: 0.13243657f, y: -0.8730836f, z: -0.45493412f, w: -0.114980996f),
                new Quaternion(x: 0.17098099f, y: -0.92266804f, z: -0.34507802f, w: -0.019245595f),
                new Quaternion(x: 0.15011512f, y: -0.952169f, z: -0.25831383f, w: -0.064137466f),
                new Quaternion(x: 0.07684197f, y: -0.97957754f, z: -0.18576658f, w: -0.0037347008f)
            };
            return pd;
        }

        private static PoseDat LeftHandFallbackPoint() {
            PoseDat pd = new PoseDat();
            pd.thumbFingerMovementType = 0;
            pd.indexFingerMovementType = 0;
            pd.middleFingerMovementType = 0;
            pd.ringFingerMovementType = 0;
            pd.ignoreRootPoseData = true;
            pd.ignoreWristPoseData = true;
            pd.position = new Vector3(0f, 0f, 0f);
            pd.rotation = new Quaternion(0f, -0f, -0f, -1f);
            pd.bonePositions = new Vector3[] {
                new Vector3(x: -0f, y: 0f, z: 0f),
                new Vector3(x: -0.034037687f, y: 0.03650266f, z: 0.16472164f),
                new Vector3(x: -0.016305087f, y: 0.027528726f, z: 0.017799662f),
                new Vector3(x: 0.040405963f, y: -0.000000051561553f, z: 0.000000045447194f),
                new Vector3(x: 0.032516792f, y: -0.000000051137583f, z: -0.000000012933195f),
                new Vector3(x: 0.030463902f, y: 0.00000016269207f, z: 0.0000000792839f),
                new Vector3(x: 0.0038021489f, y: 0.021514187f, z: 0.012803366f),
                new Vector3(x: 0.074204385f, y: 0.005002201f, z: -0.00023377323f),
                new Vector3(x: 0.043286677f, y: 0.000000059333324f, z: 0.00000018320057f),
                new Vector3(x: 0.028275194f, y: -0.00000009297885f, z: -0.00000012653295f),
                new Vector3(x: 0.022821384f, y: -0.00000014365155f, z: 0.00000007651614f),
                new Vector3(x: 0.005786922f, y: 0.0068064053f, z: 0.016533904f),
                new Vector3(x: 0.07095288f, y: -0.00077883265f, z: -0.000997186f),
                new Vector3(x: 0.043108486f, y: -0.00000009950596f, z: -0.0000000067041825f),
                new Vector3(x: 0.03326598f, y: -0.000000017544496f, z: -0.000000020628962f),
                new Vector3(x: 0.025892371f, y: 0.00000009984198f, z: -0.0000000020352908f),
                new Vector3(x: 0.004123044f, y: -0.0068582613f, z: 0.016562859f),
                new Vector3(x: 0.06587581f, y: -0.0017857892f, z: -0.00069344096f),
                new Vector3(x: 0.040331207f, y: -0.00000009449958f, z: -0.00000002273692f),
                new Vector3(x: 0.028488781f, y: 0.000000101152565f, z: 0.000000045493586f),
                new Vector3(x: 0.022430236f, y: 0.00000010846127f, z: -0.000000017428562f),
                new Vector3(x: 0.0011314574f, y: -0.019294508f, z: 0.01542875f),
                new Vector3(x: 0.0628784f, y: -0.0028440945f, z: -0.0003315112f),
                new Vector3(x: 0.029874247f, y: -0.000000034247638f, z: -0.00000009126629f),
                new Vector3(x: 0.017978692f, y: -0.0000000028448923f, z: -0.00000020797508f),
                new Vector3(x: 0.01801794f, y: -0.0000000200012f, z: 0.0000000659746f),
                new Vector3(x: 0.019716311f, y: 0.002801723f, z: 0.093936935f),
                new Vector3(x: -0.0075385696f, y: 0.01764465f, z: 0.10240429f),
                new Vector3(x: -0.0031984635f, y: 0.0072115273f, z: 0.11665362f),
                new Vector3(x: 0.000026269245f, y: -0.007118772f, z: 0.13072418f),
                new Vector3(x: -0.0018780098f, y: -0.02256182f, z: 0.14003526f)
            };
            pd.boneRotations = new Quaternion[] {
                new Quaternion(x: -6.123234e-17f, y: 1f, z: 6.123234e-17f, w: -0.00000004371139f),
                new Quaternion(x: -0.078608155f, y: -0.92027926f, z: 0.3792963f, w: -0.055146642f),
                new Quaternion(x: -0.060760066f, y: -0.79196125f, z: 0.3942209f, w: 0.4622721f),
                new Quaternion(x: -0.005047277f, y: 0.083810456f, z: -0.34894884f, w: 0.933373f),
                new Quaternion(x: 0.00009335017f, y: -0.0014032124f, z: -0.15050922f, w: 0.98860765f),
                new Quaternion(x: -1.3877788e-17f, y: -1.3877788e-17f, z: -5.551115e-17f, w: 1f),
                new Quaternion(x: -0.6173145f, y: -0.44918522f, z: -0.5108743f, w: 0.39517453f),
                new Quaternion(x: 0.045986902f, y: 0.11017035f, z: -0.06647379f, w: 0.9906205f),
                new Quaternion(x: 0.09205483f, y: 0.00094662595f, z: -0.022614187f, w: 0.9954967f),
                new Quaternion(x: 0.010468128f, y: 0.027353302f, z: 0.0121929655f, w: 0.9994967f),
                new Quaternion(x: 6.938894e-18f, y: 1.9428903e-16f, z: -1.348151e-33f, w: 1f),
                new Quaternion(x: -0.5142028f, y: -0.4836996f, z: -0.47834843f, w: 0.522315f),
                new Quaternion(x: -0.10267462f, y: -0.037405714f, z: -0.59712917f, w: 0.79466695f),
                new Quaternion(x: -0.0031541286f, y: -0.0979462f, z: -0.6884634f, w: 0.7186201f),
                new Quaternion(x: -0.06366954f, y: 0.00036316764f, z: -0.7530614f, w: 0.6548623f),
                new Quaternion(x: 1.1639192e-17f, y: -5.602331e-17f, z: -0.040125635f, w: 0.9991947f),
                new Quaternion(x: -0.489609f, y: -0.46399677f, z: -0.52064353f, w: 0.523374f),
                new Quaternion(x: -0.08626322f, y: 0.022599243f, z: -0.6245973f, w: 0.7758391f),
                new Quaternion(x: -0.0049873046f, y: -0.039519195f, z: -0.66851556f, w: 0.7426307f),
                new Quaternion(x: -0.027121458f, y: -0.005438834f, z: -0.7788164f, w: 0.62664175f),
                new Quaternion(x: 6.938894e-18f, y: -9.62965e-35f, z: -1.3877788e-17f, w: 1f),
                new Quaternion(x: -0.47976637f, y: -0.37993452f, z: -0.63019824f, w: 0.47783276f),
                new Quaternion(x: -0.09135258f, y: 0.06652915f, z: -0.6598471f, w: 0.742853f),
                new Quaternion(x: 0.0072799353f, y: 0.037179545f, z: -0.62954974f, w: 0.77603596f),
                new Quaternion(x: -0.008087321f, y: -0.003009417f, z: -0.7361885f, w: 0.6767216f),
                new Quaternion(x: 0f, y: 0f, z: 1.9081958e-17f, w: 1f),
                new Quaternion(x: -0.54886997f, y: 0.1177861f, z: -0.7578353f, w: 0.33249632f),
                new Quaternion(x: 0.13243657f, y: -0.8730836f, z: -0.45493412f, w: -0.114980996f),
                new Quaternion(x: 0.17098099f, y: -0.92266804f, z: -0.34507802f, w: -0.019245595f),
                new Quaternion(x: 0.15011512f, y: -0.952169f, z: -0.25831383f, w: -0.064137466f),
                new Quaternion(x: 0.07684197f, y: -0.97957754f, z: -0.18576658f, w: -0.0037347008f)
            };
            return pd;
        }

        private static PoseDat RightHandFallbackPoint() {
            PoseDat pd = new PoseDat();
            pd.thumbFingerMovementType = 0;
            pd.indexFingerMovementType = 0;
            pd.middleFingerMovementType = 0;
            pd.ringFingerMovementType = 0;
            pd.ignoreRootPoseData = true;
            pd.ignoreWristPoseData = true;
            pd.position = new Vector3(0f, 0f, 0f);
            pd.rotation = new Quaternion(-0f, -0f, -0f, 1f);
            pd.bonePositions = new Vector3[] {
                new Vector3(x: -0f, y: 0f, z: 0f),
                new Vector3(x: -0.034037687f, y: 0.03650266f, z: 0.16472164f),
                new Vector3(x: -0.016305087f, y: 0.027528726f, z: 0.017799662f),
                new Vector3(x: 0.040405963f, y: -0.000000051561553f, z: 0.000000045447194f),
                new Vector3(x: 0.032516792f, y: -0.000000051137583f, z: -0.000000012933195f),
                new Vector3(x: 0.030463902f, y: 0.00000016269207f, z: 0.0000000792839f),
                new Vector3(x: 0.0038021489f, y: 0.021514187f, z: 0.012803366f),
                new Vector3(x: 0.074204385f, y: 0.005002201f, z: -0.00023377323f),
                new Vector3(x: 0.043286677f, y: 0.000000059333324f, z: 0.00000018320057f),
                new Vector3(x: 0.028275194f, y: -0.00000009297885f, z: -0.00000012653295f),
                new Vector3(x: 0.022821384f, y: -0.00000014365155f, z: 0.00000007651614f),
                new Vector3(x: 0.005786922f, y: 0.0068064053f, z: 0.016533904f),
                new Vector3(x: 0.07095288f, y: -0.00077883265f, z: -0.000997186f),
                new Vector3(x: 0.043108486f, y: -0.00000009950596f, z: -0.0000000067041825f),
                new Vector3(x: 0.03326598f, y: -0.000000017544496f, z: -0.000000020628962f),
                new Vector3(x: 0.025892371f, y: 0.00000009984198f, z: -0.0000000020352908f),
                new Vector3(x: 0.004123044f, y: -0.0068582613f, z: 0.016562859f),
                new Vector3(x: 0.06587581f, y: -0.0017857892f, z: -0.00069344096f),
                new Vector3(x: 0.040331207f, y: -0.00000009449958f, z: -0.00000002273692f),
                new Vector3(x: 0.028488781f, y: 0.000000101152565f, z: 0.000000045493586f),
                new Vector3(x: 0.022430236f, y: 0.00000010846127f, z: -0.000000017428562f),
                new Vector3(x: 0.0011314574f, y: -0.019294508f, z: 0.01542875f),
                new Vector3(x: 0.0628784f, y: -0.0028440945f, z: -0.0003315112f),
                new Vector3(x: 0.029874247f, y: -0.000000034247638f, z: -0.00000009126629f),
                new Vector3(x: 0.017978692f, y: -0.0000000028448923f, z: -0.00000020797508f),
                new Vector3(x: 0.01801794f, y: -0.0000000200012f, z: 0.0000000659746f),
                new Vector3(x: 0.019716311f, y: 0.002801723f, z: 0.093936935f),
                new Vector3(x: -0.0075385696f, y: 0.01764465f, z: 0.10240429f),
                new Vector3(x: -0.0031984635f, y: 0.0072115273f, z: 0.11665362f),
                new Vector3(x: 0.000026269245f, y: -0.007118772f, z: 0.13072418f),
                new Vector3(x: -0.0018780098f, y: -0.02256182f, z: 0.14003526f)
            };
            pd.boneRotations = new Quaternion[] {
                new Quaternion(x: -6.123234e-17f, y: 1f, z: 6.123234e-17f, w: -0.00000004371139f),
                new Quaternion(x: -0.078608155f, y: -0.92027926f, z: 0.3792963f, w: -0.055146642f),
                new Quaternion(x: -0.060760066f, y: -0.79196125f, z: 0.3942209f, w: 0.4622721f),
                new Quaternion(x: -0.005047277f, y: 0.083810456f, z: -0.34894884f, w: 0.933373f),
                new Quaternion(x: 0.00009335017f, y: -0.0014032124f, z: -0.15050922f, w: 0.98860765f),
                new Quaternion(x: -1.3877788e-17f, y: -1.3877788e-17f, z: -5.551115e-17f, w: 1f),
                new Quaternion(x: -0.6173145f, y: -0.44918522f, z: -0.5108743f, w: 0.39517453f),
                new Quaternion(x: 0.045986902f, y: 0.11017035f, z: -0.06647379f, w: 0.9906205f),
                new Quaternion(x: 0.09205483f, y: 0.00094662595f, z: -0.022614187f, w: 0.9954967f),
                new Quaternion(x: 0.010468128f, y: 0.027353302f, z: 0.0121929655f, w: 0.9994967f),
                new Quaternion(x: 6.938894e-18f, y: 1.9428903e-16f, z: -1.348151e-33f, w: 1f),
                new Quaternion(x: -0.5142028f, y: -0.4836996f, z: -0.47834843f, w: 0.522315f),
                new Quaternion(x: -0.10267462f, y: -0.037405714f, z: -0.59712917f, w: 0.79466695f),
                new Quaternion(x: -0.0031541286f, y: -0.0979462f, z: -0.6884634f, w: 0.7186201f),
                new Quaternion(x: -0.06366954f, y: 0.00036316764f, z: -0.7530614f, w: 0.6548623f),
                new Quaternion(x: 1.1639192e-17f, y: -5.602331e-17f, z: -0.040125635f, w: 0.9991947f),
                new Quaternion(x: -0.489609f, y: -0.46399677f, z: -0.52064353f, w: 0.523374f),
                new Quaternion(x: -0.08626322f, y: 0.022599243f, z: -0.6245973f, w: 0.7758391f),
                new Quaternion(x: -0.0049873046f, y: -0.039519195f, z: -0.66851556f, w: 0.7426307f),
                new Quaternion(x: -0.027121458f, y: -0.005438834f, z: -0.7788164f, w: 0.62664175f),
                new Quaternion(x: 6.938894e-18f, y: -9.62965e-35f, z: -1.3877788e-17f, w: 1f),
                new Quaternion(x: -0.47976637f, y: -0.37993452f, z: -0.63019824f, w: 0.47783276f),
                new Quaternion(x: -0.09135258f, y: 0.06652915f, z: -0.6598471f, w: 0.742853f),
                new Quaternion(x: 0.0072799353f, y: 0.037179545f, z: -0.62954974f, w: 0.77603596f),
                new Quaternion(x: -0.008087321f, y: -0.003009417f, z: -0.7361885f, w: 0.6767216f),
                new Quaternion(x: 0f, y: 0f, z: 1.9081958e-17f, w: 1f),
                new Quaternion(x: -0.54886997f, y: 0.1177861f, z: -0.7578353f, w: 0.33249632f),
                new Quaternion(x: 0.13243657f, y: -0.8730836f, z: -0.45493412f, w: -0.114980996f),
                new Quaternion(x: 0.17098099f, y: -0.92266804f, z: -0.34507802f, w: -0.019245595f),
                new Quaternion(x: 0.15011512f, y: -0.952169f, z: -0.25831383f, w: -0.064137466f),
                new Quaternion(x: 0.07684197f, y: -0.97957754f, z: -0.18576658f, w: -0.0037347008f)
            };
            return pd;
        }

        private static PoseDat RightHandFallbackRelaxed() {
            PoseDat pd = new PoseDat();
            pd.thumbFingerMovementType = 0;
            pd.indexFingerMovementType = 0;
            pd.middleFingerMovementType = 0;
            pd.ringFingerMovementType = 0;
            pd.ignoreRootPoseData = true;
            pd.ignoreWristPoseData = true;
            pd.position = new Vector3(0f, 0f, 0f);
            pd.rotation = new Quaternion(-0f, -0f, -0f, 1f);
            pd.bonePositions = new Vector3[] {
                new Vector3(-0f, 0f, 0f),
                new Vector3(-0.034037687f, 0.03650266f, 0.16472164f),
                new Vector3(-0.012083233f, 0.028070247f, 0.025049694f),
                new Vector3(0.040405963f, -0.000000051561553f, 0.000000045447194f),
                new Vector3(0.032516792f, -0.000000051137583f, -0.000000012933195f),
                new Vector3(0.030463902f, 0.00000016269207f, 0.0000000792839f),
                new Vector3(0.0006324522f, 0.026866155f, 0.015001948f),
                new Vector3(0.074204385f, 0.005002201f, -0.00023377323f),
                new Vector3(0.043930072f, 0.000000059567498f, 0.00000018367103f),
                new Vector3(0.02869547f, -0.00000009398158f, -0.00000012649753f),
                new Vector3(0.022821384f, -0.00000014365155f, 0.00000007651614f),
                new Vector3(0.0021773134f, 0.007119544f, 0.016318738f),
                new Vector3(0.07095288f, -0.00077883265f, -0.000997186f),
                new Vector3(0.043108486f, -0.00000009950596f, -0.0000000067041825f),
                new Vector3(0.033266045f, -0.00000001320567f, -0.000000021670374f),
                new Vector3(0.025892371f, 0.00000009984198f, -0.0000000020352908f),
                new Vector3(0.0005134356f, -0.0065451227f, 0.016347693f),
                new Vector3(0.06587581f, -0.0017857892f, -0.00069344096f),
                new Vector3(0.04069671f, -0.000000095347104f, -0.000000022934731f),
                new Vector3(0.028746964f, 0.00000010089892f, 0.000000045306827f),
                new Vector3(0.022430236f, 0.00000010846127f, -0.000000017428562f),
                new Vector3(-0.002478151f, -0.01898137f, 0.015213584f),
                new Vector3(0.0628784f, -0.0028440945f, -0.0003315112f),
                new Vector3(0.030219711f, -0.00000003418319f, -0.00000009332872f),
                new Vector3(0.018186597f, -0.0000000050220166f, -0.00000020934549f),
                new Vector3(0.01801794f, -0.0000000200012f, 0.0000000659746f),
                new Vector3(-0.0060591106f, 0.05628522f, 0.060063843f),
                new Vector3(-0.04041555f, -0.043017667f, 0.019344581f),
                new Vector3(-0.03935372f, -0.07567404f, 0.047048334f),
                new Vector3(-0.038340144f, -0.09098663f, 0.08257892f),
                new Vector3(-0.031805996f, -0.08721431f, 0.12101539f)
            };
            pd.boneRotations = new Quaternion[] {
                new Quaternion(x: -6.123234e-17f, y: 1, z: 6.123234e-17f, w: -0.00000004371139f),
                new Quaternion(x: -0.078608155f, y: -0.92027926f, z: 0.3792963f, w: -0.055146642f),
                new Quaternion(x: -0.24104308f, y: -0.76422274f, z: 0.45859465f, w: 0.38412613f),
                new Quaternion(x: 0.085189685f, y: 0.0000513494f, z: -0.28143752f, w: 0.95579064f),
                new Quaternion(x: 0.0052029183f, y: -0.021480577f, z: -0.15888694f, w: 0.9870494f),
                new Quaternion(x: -1.3877788e-17f, y: -1.3877788e-17f, z: -5.551115e-17f, w: 1f),
                new Quaternion(x: -0.6442515f, y: -0.42213318f, z: -0.4782025f, w: 0.42197865f),
                new Quaternion(x: 0.08568421f, y: 0.023565516f, z: -0.19161178f, w: 0.9774394f),
                new Quaternion(x: 0.045650285f, y: 0.0043684426f, z: -0.095879465f, w: 0.99433607f),
                new Quaternion(x: -0.0020507684f, y: 0.022764975f, z: -0.15681197f, w: 0.987364f),
                new Quaternion(x: 6.938894e-18f, y: 1.9428903e-16f, z: -1.348151e-33f, w: 1f),
                new Quaternion(x: -0.546723f, y: -0.46074906f, z: -0.44252017f, w: 0.54127645f),
                new Quaternion(x: -0.17867392f, y: 0.047816366f, z: -0.24333772f, w: 0.9521429f),
                new Quaternion(x: 0.020366715f, y: -0.010060345f, z: -0.21893612f, w: 0.9754748f),
                new Quaternion(x: -0.010457605f, y: 0.026426358f, z: -0.19179714f, w: 0.981023f),
                new Quaternion(x: 1.1639192e-17f, y: -5.602331e-17f, z: -0.040125635f, w: 0.9991947f),
                new Quaternion(x: -0.5166922f, y: -0.4298879f, z: -0.49554786f, w: 0.5501435f),
                new Quaternion(x: -0.17289871f, y: 0.114340894f, z: -0.29726714f, w: 0.93202174f),
                new Quaternion(x: -0.0021954547f, y: -0.000443071f, z: -0.22544385f, w: 0.9742536f),
                new Quaternion(x: -0.00472193f, y: 0.011803731f, z: -0.35618067f, w: 0.93433064f),
                new Quaternion(x: 6.938894e-18f, y: -9.62965e-35f, z: -1.3877788e-17f, w: 1f),
                new Quaternion(x: -0.5269183f, y: -0.32674035f, z: -0.5840246f, w: 0.52394f),
                new Quaternion(x: -0.2006022f, y: 0.15258452f, z: -0.36497858f, w: 0.8962519f),
                new Quaternion(x: 0.0018557907f, y: 0.0004098564f, z: -0.25201905f, w: 0.96772045f),
                new Quaternion(x: -0.019474672f, y: 0.048342716f, z: -0.26703015f, w: 0.9622778f),
                new Quaternion(x: 0f, y: 0, z: 1.9081958e-17f, w: 1f),
                new Quaternion(x: 0.20274544f, y: 0.59426665f, z: 0.2494411f, w: 0.73723847f),
                new Quaternion(x: 0.6235274f, y: -0.66380864f, z: -0.29373443f, w: -0.29033053f),
                new Quaternion(x: 0.6780625f, y: -0.6592852f, z: -0.26568344f, w: -0.18704711f),
                new Quaternion(x: 0.7367927f, y: -0.6347571f, z: -0.14393571f, w: -0.18303718f),
                new Quaternion(x: 0.7584072f, y: -0.6393418f, z: -0.12667806f, w: -0.0036594148f)
            };
            return pd;
        }

        private static PoseDat LeftHandFallbackRelaxed()
        {
            PoseDat pd = new PoseDat();
            pd.thumbFingerMovementType = 0;
            pd.indexFingerMovementType = 0;
            pd.middleFingerMovementType = 0;
            pd.ringFingerMovementType = 0;
            pd.ignoreRootPoseData = true;
            pd.ignoreWristPoseData = true;
            pd.position = new Vector3(0f, 0f, 0f);
            pd.rotation = new Quaternion(0f, -0f, -0f, -1f);
            pd.bonePositions = new Vector3[] {
                new Vector3(-0f, 0f, 0f),
                new Vector3(-0.034037687f, 0.03650266f, 0.16472164f),
                new Vector3(-0.012083233f, 0.028070247f, 0.025049694f),
                new Vector3(0.040405963f, -0.000000051561553f, 0.000000045447194f),
                new Vector3(0.032516792f, -0.000000051137583f, -0.000000012933195f),
                new Vector3(0.030463902f, 0.00000016269207f, 0.0000000792839f),
                new Vector3(0.0006324522f, 0.026866155f, 0.015001948f),
                new Vector3(0.074204385f, 0.005002201f, -0.00023377323f),
                new Vector3(0.043930072f, 0.000000059567498f, 0.00000018367103f),
                new Vector3(0.02869547f, -0.00000009398158f, -0.00000012649753f),
                new Vector3(0.022821384f, -0.00000014365155f, 0.00000007651614f),
                new Vector3(0.0021773134f, 0.007119544f, 0.016318738f),
                new Vector3(0.07095288f, -0.00077883265f, -0.000997186f),
                new Vector3(0.043108486f, -0.00000009950596f, -0.0000000067041825f),
                new Vector3(0.033266045f, -0.00000001320567f, -0.000000021670374f),
                new Vector3(0.025892371f, 0.00000009984198f, -0.0000000020352908f),
                new Vector3(0.0005134356f, -0.0065451227f, 0.016347693f),
                new Vector3(0.06587581f, -0.0017857892f, -0.00069344096f),
                new Vector3(0.04069671f, -0.000000095347104f, -0.000000022934731f),
                new Vector3(0.028746964f, 0.00000010089892f, 0.000000045306827f),
                new Vector3(0.022430236f, 0.00000010846127f, -0.000000017428562f),
                new Vector3(-0.002478151f, -0.01898137f, 0.015213584f),
                new Vector3(0.0628784f, -0.0028440945f, -0.0003315112f),
                new Vector3(0.030219711f, -0.00000003418319f, -0.00000009332872f),
                new Vector3(0.018186597f, -0.0000000050220166f, -0.00000020934549f),
                new Vector3(0.01801794f, -0.0000000200012f, 0.0000000659746f),
                new Vector3(-0.0060591106f, 0.05628522f, 0.060063843f),
                new Vector3(-0.04041555f, -0.043017667f, 0.019344581f),
                new Vector3(-0.03935372f, -0.07567404f, 0.047048334f),
                new Vector3(-0.038340144f, -0.09098663f, 0.08257892f),
                new Vector3(-0.031805996f, -0.08721431f, 0.12101539f)
            };
            pd.boneRotations = new Quaternion[] {
                new Quaternion(x: -6.123234e-17f, y: 1, z: 6.123234e-17f, w: -0.00000004371139f),
                new Quaternion(x: -0.078608155f, y: -0.92027926f, z: 0.3792963f, w: -0.055146642f),
                new Quaternion(x: -0.24104308f, y: -0.76422274f, z: 0.45859465f, w: 0.38412613f),
                new Quaternion(x: 0.085189685f, y: 0.0000513494f, z: -0.28143752f, w: 0.95579064f),
                new Quaternion(x: 0.0052029183f, y: -0.021480577f, z: -0.15888694f, w: 0.9870494f),
                new Quaternion(x: -1.3877788e-17f, y: -1.3877788e-17f, z: -5.551115e-17f, w: 1f),
                new Quaternion(x: -0.6442515f, y: -0.42213318f, z: -0.4782025f, w: 0.42197865f),
                new Quaternion(x: 0.08568421f, y: 0.023565516f, z: -0.19161178f, w: 0.9774394f),
                new Quaternion(x: 0.045650285f, y: 0.0043684426f, z: -0.095879465f, w: 0.99433607f),
                new Quaternion(x: -0.0020507684f, y: 0.022764975f, z: -0.15681197f, w: 0.987364f),
                new Quaternion(x: 6.938894e-18f, y: 1.9428903e-16f, z: -1.348151e-33f, w: 1f),
                new Quaternion(x: -0.546723f, y: -0.46074906f, z: -0.44252017f, w: 0.54127645f),
                new Quaternion(x: -0.17867392f, y: 0.047816366f, z: -0.24333772f, w: 0.9521429f),
                new Quaternion(x: 0.020366715f, y: -0.010060345f, z: -0.21893612f, w: 0.9754748f),
                new Quaternion(x: -0.010457605f, y: 0.026426358f, z: -0.19179714f, w: 0.981023f),
                new Quaternion(x: 1.1639192e-17f, y: -5.602331e-17f, z: -0.040125635f, w: 0.9991947f),
                new Quaternion(x: -0.5166922f, y: -0.4298879f, z: -0.49554786f, w: 0.5501435f),
                new Quaternion(x: -0.17289871f, y: 0.114340894f, z: -0.29726714f, w: 0.93202174f),
                new Quaternion(x: -0.0021954547f, y: -0.000443071f, z: -0.22544385f, w: 0.9742536f),
                new Quaternion(x: -0.00472193f, y: 0.011803731f, z: -0.35618067f, w: 0.93433064f),
                new Quaternion(x: 6.938894e-18f, y: -9.62965e-35f, z: -1.3877788e-17f, w: 1f),
                new Quaternion(x: -0.5269183f, y: -0.32674035f, z: -0.5840246f, w: 0.52394f),
                new Quaternion(x: -0.2006022f, y: 0.15258452f, z: -0.36497858f, w: 0.8962519f),
                new Quaternion(x: 0.0018557907f, y: 0.0004098564f, z: -0.25201905f, w: 0.96772045f),
                new Quaternion(x: -0.019474672f, y: 0.048342716f, z: -0.26703015f, w: 0.9622778f),
                new Quaternion(x: 0f, y: 0, z: 1.9081958e-17f, w: 1f),
                new Quaternion(x: 0.20274544f, y: 0.59426665f, z: 0.2494411f, w: 0.73723847f),
                new Quaternion(x: 0.6235274f, y: -0.66380864f, z: -0.29373443f, w: -0.29033053f),
                new Quaternion(x: 0.6780625f, y: -0.6592852f, z: -0.26568344f, w: -0.18704711f),
                new Quaternion(x: 0.7367927f, y: -0.6347571f, z: -0.14393571f, w: -0.18303718f),
                new Quaternion(x: 0.7584072f, y: -0.6393418f, z: -0.12667806f, w: -0.0036594148f)
            };
            return pd;
        }

        private class PoseDat
        {
            public Vector3[] bonePositions;
            public Quaternion[] boneRotations;
            public bool ignoreRootPoseData;
            public bool ignoreWristPoseData;
            public Vector3 position;
            public Quaternion rotation;
            public SteamVR_Skeleton_FingerExtensionTypes thumbFingerMovementType;
            public SteamVR_Skeleton_FingerExtensionTypes indexFingerMovementType;
            public SteamVR_Skeleton_FingerExtensionTypes middleFingerMovementType;
            public SteamVR_Skeleton_FingerExtensionTypes ringFingerMovementType;
            public SteamVR_Skeleton_FingerExtensionTypes pinkyFingerMovementType;
        }

    }

}