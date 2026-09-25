using System.Collections.Generic;
using UnityEngine;
using UnityEngine.PostProcessing;
using UnityEngine.Rendering;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;

namespace ValheimVRMod.Scripts
{
    class UnderwaterEffectsUpdater : MonoBehaviour
    {
        private static readonly Color UNDER_WATER_OVERLAY_COLOR = new Color(0.5f, 0.75f, 0.75f);
        private static readonly Vector3 UNDER_WATER_OVERLAY_OFFSET = new Vector3(0, 0, 0.125f);
        private const float UNDER_WATER_OVERLAY_SIZE = 0.25f;
        private GameObject underwaterOverlay;
        private Material underwaterOverlayMaterial;
        private GameObject underwaterLightBlocker = null;
        // See VHVRConfig.UnderwaterWaveResolution(): 0 and 1 use the flat quad, higher values the wave following grid.
        private MeshFilter lightBlockerMeshFilter;
        private Mesh lightBlockerQuadMesh;
        private Mesh lightBlockerGridMesh;
        private int lightBlockerGridResolution;
        private Vector3[] lightBlockerGridVertices;
        private float[] lightBlockerGridOffsets;
        private WaterVolume lastWaterVolume;
        private float lightBlockerSink;
        private float lightBlockerEyeDepth;
        private static readonly Color LIGHT_BLOCKER_COLOR = new Color(0.5f, 0.5f, 0.625f, 1);
        // Snell's window has a half angle of about 48.6 degrees, i. e. a radius of about 1.13 times the depth.
        private const float LIGHT_BLOCKER_WINDOW_RADIUS_PER_DEPTH = 1.13f;
        private const float LIGHT_BLOCKER_WINDOW_MIN_RADIUS = 0.25f;
        // Where the window has turned fully into the blocker's color, relative to its clear radius.
        private const float LIGHT_BLOCKER_WINDOW_DARK_RATIO = 2;
        // Half the distance between the samples the tilted quad takes its slope from.
        private const float LIGHT_BLOCKER_SLOPE_SAMPLE_DISTANCE = 0.5f;
        // How far the grid follows the waves. Further out the fog and the reduced far clip plane hide the surface.
        private const float LIGHT_BLOCKER_GRID_RADIUS = 32;
        // The flat skirt around the grid, as wide as the flat quad, so the sky doesn't show past the grid's edge.
        private const float LIGHT_BLOCKER_SKIRT_RADIUS = 512;
        // Keeps the blocker just below the drawn surface, so it covers where the waves the game draws differ slightly
        // from those it computes on the CPU, rather than flickering against them.
        private const float LIGHT_BLOCKER_MAX_SINK = 0.05f;
        private Camera camera;
        private bool initialized = false;
        private bool isHidingWater;

        // A smooth blending factor for transitioning between using and not using under water effects
        public static float Underwaterness { get; private set; }

        public void Init(Camera camera, PostProcessingBehaviour postProcessingBehaviour, PostProcessingProfile originalPostProcessingProfile)
        {
            this.camera = camera;

            underwaterOverlay = GameObject.CreatePrimitive(PrimitiveType.Quad);
            underwaterOverlay.layer = LayerUtils.WORLDSPACE_UI_LAYER;
            underwaterOverlay.transform.SetParent(camera.transform);
            underwaterOverlay.transform.localPosition = UNDER_WATER_OVERLAY_OFFSET;
            underwaterOverlay.transform.localRotation = Quaternion.identity;
            underwaterOverlay.transform.localScale = UNDER_WATER_OVERLAY_SIZE * 2 * Vector3.one;
            Destroy(underwaterOverlay.GetComponent<Collider>());
            var underwaterOverlayRenderer = underwaterOverlay.GetComponent<MeshRenderer>();
            underwaterOverlayMaterial = GameObject.Instantiate(VRAssetManager.GetAsset<Material>("VHVRMultiply"));
            underwaterOverlayMaterial.color = UNDER_WATER_OVERLAY_COLOR;
            underwaterOverlayRenderer.material = underwaterOverlayMaterial;
            underwaterOverlayRenderer.receiveShadows = false;
            underwaterOverlayRenderer.shadowCastingMode = ShadowCastingMode.Off;
            underwaterOverlayRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            underwaterOverlay.SetActive(false);

            underwaterLightBlocker = GameObject.CreatePrimitive(PrimitiveType.Quad);
            underwaterLightBlocker.layer = LayerUtils.WATERVOLUME_LAYER;
            // TODO: consider using a one-sided plane.
            underwaterLightBlocker.transform.localScale = new Vector3(1024, 1024, 1024);
            underwaterLightBlocker.transform.rotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
            Destroy(underwaterLightBlocker.GetComponent<Collider>());
            var underwaterLightBlockerRenderer = underwaterLightBlocker.GetComponent<MeshRenderer>();
            // The Fade material shows a window overhead (see CreateLightBlockerWindowTexture()). Without it in the
            // asset bundle, the opaque one still gets the window's brighter middle.
            Material lightBlockerMaterial;
            try
            {
                lightBlockerMaterial = VRAssetManager.GetAsset<Material>("StandardFade");
            }
            catch (KeyNotFoundException)
            {
                lightBlockerMaterial = VRAssetManager.GetAsset<Material>("StandardClone");
            }
            underwaterLightBlockerRenderer.material = Instantiate(lightBlockerMaterial);
            underwaterLightBlockerRenderer.material.color = Color.white;
            underwaterLightBlockerRenderer.material.mainTexture = CreateLightBlockerWindowTexture();
            underwaterLightBlockerRenderer.receiveShadows = false;
            underwaterLightBlockerRenderer.shadowCastingMode = ShadowCastingMode.Off;
            underwaterLightBlockerRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            lightBlockerMeshFilter = underwaterLightBlocker.GetComponent<MeshFilter>();
            lightBlockerQuadMesh = lightBlockerMeshFilter.sharedMesh;
            underwaterLightBlocker.SetActive(false);

            initialized = true;
        }

        void FixedUpdate()
        {
            if (!initialized)
            {
                return;
            }

            var localPlayer = Player.m_localPlayer;
            var elevation =
                localPlayer == null || !localPlayer.InWater() ?
                1 :
                transform.position.y - GetWaterLevel(transform.position);

            Underwaterness = GetUnderwaterness(elevation);
            if (Underwaterness > 0)
            {
                // Never sink the blocker below the eyes, where it would cover the view under water.
                lightBlockerSink = Mathf.Clamp(-elevation - 0.01f, 0, LIGHT_BLOCKER_MAX_SINK);
                lightBlockerEyeDepth = -elevation;
                UpdateLightBlockerQuad();
                underwaterLightBlocker.SetActive(true);
            }
            else
            {
                underwaterLightBlocker.SetActive(false);
            }

            camera.farClipPlane =
                Underwaterness >= 1 ?
                VRPlayer.MainCameraFarClipPlane :
                Mathf.Min(VRPlayer.MainCameraFarClipPlane, 32 / Underwaterness);

            if (isHidingWater)
            {
                if (elevation >= 0)
                {
                    camera.cullingMask |= (1 << LayerUtils.WATER);
                    isHidingWater = false;
                }
            }
            else if (elevation < -0.125f)
            {
                // This hides the water from the VR camera but not from the follow camera
                camera.cullingMask &= ~(1 << LayerUtils.WATER);
                isHidingWater = true;
            }
        }

        // A round window straight above the eyes, clear (or, with the opaque fallback material, bright) in the middle and
        // quickly turning into the blocker's color further out, like the Snell's window real water shows from below.
        // Its texture coordinates are scaled so that the texture's inscribed circle is LIGHT_BLOCKER_WINDOW_DARK_RATIO
        // times the clear radius, see UpdateLightBlockerWindow().
        private static Texture2D CreateLightBlockerWindowTexture()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, /* mipChain= */ false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var radius = new Vector2(x + 0.5f - size / 2f, y + 0.5f - size / 2f).magnitude / (size / 2f);
                    var t = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1 / LIGHT_BLOCKER_WINDOW_DARK_RATIO, 1, radius));
                    var color = Color.Lerp(Color.white, LIGHT_BLOCKER_COLOR, t);
                    color.a = t;
                    pixels[y * size + x] = color;
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private void UpdateLightBlockerWindow(float eyeDepth, bool isGrid)
        {
            // About the radius of Snell's window at this depth, but never too small to see when near the surface.
            var clearRadius = Mathf.Max(LIGHT_BLOCKER_WINDOW_MIN_RADIUS, LIGHT_BLOCKER_WINDOW_RADIUS_PER_DEPTH * eyeDepth);
            var darkRadius = clearRadius * LIGHT_BLOCKER_WINDOW_DARK_RATIO;
            var material = underwaterLightBlocker.GetComponent<MeshRenderer>().material;
            // The grid's texture coordinates are meters from the eyes, the quad's span its 1024 meters from 0 to 1.
            var scale = (isGrid ? 1 : 1024) / (2 * darkRadius);
            var offset = isGrid ? 0.5f : 0.5f - 0.5f * scale;
            material.mainTextureScale = new Vector2(scale, scale);
            material.mainTextureOffset = new Vector2(offset, offset);
        }

        private void UpdateLightBlockerQuad()
        {
            int resolution = VHVRConfig.UnderwaterWaveResolution();
            UpdateLightBlockerWindow(lightBlockerEyeDepth, isGrid: resolution >= 2);
            if (resolution >= 2)
            {
                if (resolution != lightBlockerGridResolution)
                {
                    BuildLightBlockerGrid(resolution);
                }
                // Placed and shaped every frame in LateUpdate(), in step with the waves the game draws.
                lightBlockerMeshFilter.sharedMesh = lightBlockerGridMesh;
                underwaterLightBlocker.transform.rotation = Quaternion.identity;
                underwaterLightBlocker.transform.localScale = Vector3.one;
                return;
            }

            lightBlockerMeshFilter.sharedMesh = lightBlockerQuadMesh;
            underwaterLightBlocker.transform.localScale = new Vector3(1024, 1024, 1024);
            var position = transform.position;
            if (resolution == 0)
            {
                underwaterLightBlocker.transform.position =
                    new Vector3(position.x, Player.m_localPlayer.m_waterLevel, position.z);
                underwaterLightBlocker.transform.rotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
                return;
            }

            // Tilted with the waves where the eyes are, like the top edge of the overlay.
            var d = LIGHT_BLOCKER_SLOPE_SAMPLE_DISTANCE;
            var slopeX = (GetWaterLevel(position + d * Vector3.right) - GetWaterLevel(position - d * Vector3.right)) / (2 * d);
            var slopeZ = (GetWaterLevel(position + d * Vector3.forward) - GetWaterLevel(position - d * Vector3.forward)) / (2 * d);
            underwaterLightBlocker.transform.position =
                new Vector3(position.x, GetWaterLevel(position) - lightBlockerSink, position.z);
            underwaterLightBlocker.transform.rotation =
                Quaternion.LookRotation(new Vector3(-slopeX, 1, -slopeZ).normalized, Vector3.forward);
        }

        // After the water's own Update() has set this frame's wave time, so both follow the waves as drawn.
        void LateUpdate()
        {
            if (!initialized)
            {
                return;
            }

            var localPlayer = Player.m_localPlayer;
            UpdateUnderwaterOverlay(
                localPlayer == null || !localPlayer.InWater() ?
                1 :
                camera.transform.position.y - GetDrawnWaterLevel(camera.transform.position));

            if (underwaterLightBlocker.activeSelf && lightBlockerMeshFilter.sharedMesh == lightBlockerGridMesh)
            {
                UpdateLightBlockerGrid();
            }
        }

        private void UpdateLightBlockerGrid()
        {
            var position = transform.position;
            var eyeLevel = GetWaterLevel(position);
            underwaterLightBlocker.transform.position = new Vector3(position.x, 0, position.z);
            int count = lightBlockerGridOffsets.Length;
            for (int z = 1; z < count - 1; z++)
            {
                for (int x = 1; x < count - 1; x++)
                {
                    var offsetX = lightBlockerGridOffsets[x];
                    var offsetZ = lightBlockerGridOffsets[z];
                    var level = Floating.GetWaterLevel(
                        new Vector3(position.x + offsetX, position.y, position.z + offsetZ), ref lastWaterVolume);
                    if (level <= -10000f)
                    {
                        level = eyeLevel;
                    }
                    lightBlockerGridVertices[z * count + x] = new Vector3(offsetX, level - lightBlockerSink, offsetZ);
                }
            }
            // The skirt continues the grid's edge outwards at the same height.
            for (int i = 0; i < count; i++)
            {
                int inner = Mathf.Clamp(i, 1, count - 2);
                SetSkirtHeight(i, 0, inner, 1);
                SetSkirtHeight(i, count - 1, inner, count - 2);
                SetSkirtHeight(0, i, 1, inner);
                SetSkirtHeight(count - 1, i, count - 2, inner);
            }
            lightBlockerGridMesh.vertices = lightBlockerGridVertices;
        }

        private void SetSkirtHeight(int x, int z, int innerX, int innerZ)
        {
            int count = lightBlockerGridOffsets.Length;
            lightBlockerGridVertices[z * count + x] =
                new Vector3(
                    lightBlockerGridOffsets[x],
                    lightBlockerGridVertices[innerZ * count + innerX].y,
                    lightBlockerGridOffsets[z]);
        }

        // A resolution by resolution grid around the eyes, denser near them, surrounded by a flat skirt.
        private void BuildLightBlockerGrid(int resolution)
        {
            lightBlockerGridResolution = resolution;
            int count = resolution + 2;
            lightBlockerGridOffsets = new float[count];
            lightBlockerGridOffsets[0] = -LIGHT_BLOCKER_SKIRT_RADIUS;
            lightBlockerGridOffsets[count - 1] = LIGHT_BLOCKER_SKIRT_RADIUS;
            for (int i = 0; i < resolution; i++)
            {
                var u = -1 + 2f * i / (resolution - 1);
                lightBlockerGridOffsets[i + 1] = Mathf.Sign(u) * u * u * LIGHT_BLOCKER_GRID_RADIUS;
            }

            lightBlockerGridVertices = new Vector3[count * count];
            var normals = new Vector3[count * count];
            for (int i = 0; i < normals.Length; i++)
            {
                normals[i] = Vector3.down;
            }
            var triangles = new int[(count - 1) * (count - 1) * 6];
            int t = 0;
            for (int z = 0; z < count - 1; z++)
            {
                for (int x = 0; x < count - 1; x++)
                {
                    int a = z * count + x;
                    int b = a + 1;
                    int c = a + count;
                    int d = c + 1;
                    // Wound to face down, towards the eyes under water.
                    triangles[t++] = a; triangles[t++] = d; triangles[t++] = c;
                    triangles[t++] = a; triangles[t++] = b; triangles[t++] = d;
                }
            }

            if (lightBlockerGridMesh == null)
            {
                lightBlockerGridMesh = new Mesh();
                lightBlockerGridMesh.MarkDynamic();
            }
            lightBlockerGridMesh.Clear();
            lightBlockerGridMesh.vertices = lightBlockerGridVertices;
            lightBlockerGridMesh.normals = normals;
            var uvs = new Vector2[count * count];
            for (int z = 0; z < count; z++)
            {
                for (int x = 0; x < count; x++)
                {
                    uvs[z * count + x] = new Vector2(lightBlockerGridOffsets[x], lightBlockerGridOffsets[z]);
                }
            }
            lightBlockerGridMesh.uv = uvs;
            lightBlockerGridMesh.triangles = triangles;
            // Fixed rather than recalculated as the waves move, large enough for any water level.
            lightBlockerGridMesh.bounds =
                new Bounds(Vector3.zero, new Vector3(2 * LIGHT_BLOCKER_SKIRT_RADIUS, 20000, 2 * LIGHT_BLOCKER_SKIRT_RADIUS));
        }

        private void UpdateUnderwaterOverlay(float elevation)
        {
            // The overlay tints what the near clip plane lets through from below the water surface, so its edge
            // belongs where the surface crosses that plane. It sits just beyond it to still be drawn, and is scaled
            // with it to keep covering the same field of view.
            var distance = camera.nearClipPlane + 0.001f;
            var halfSize = distance * UNDER_WATER_OVERLAY_SIZE / UNDER_WATER_OVERLAY_OFFSET.z;
            underwaterOverlay.transform.localScale = 2 * halfSize * Vector3.one;

            if (elevation > halfSize)
            {
                // Above water, hide the overlay
                underwaterOverlay.SetActive(false);
                return;
            }

            var facing = camera.transform.forward;

            if (facing.y > 0.875f && elevation > 0)
            {
                // Looking up near water, hide the overlay
                underwaterOverlay.SetActive(false);
                return;
            }

            if ((facing.y < -0.875f && elevation < 0) || elevation < -halfSize)
            {
                // Underwater or looking down near water, cover entire view with overlay
                underwaterOverlay.transform.localRotation = Quaternion.identity;
                underwaterOverlay.transform.localPosition = new Vector3(0, 0, distance);
                underwaterOverlay.SetActive(true);
                return;
            }

            // Eyes near water surface, rotate and move overlay to only cover the underwater part of view.
            // The overlay is small and close to the eyes compared to the length of the waves, so a straight top edge
            // matches the surface well as long as it follows the height and slope of the waves where the overlay is.
            var headingSqrMagnitude = facing.x * facing.x + facing.z * facing.z;
            var cosPitch = Mathf.Sqrt(headingSqrMagnitude);
            var heading = new Vector3(facing.x, 0, facing.z) / cosPitch;
            var right = new Vector3(heading.z, 0, -heading.x);
            var overlayCenter = camera.transform.position + heading * distance;
            var leftLevel = GetDrawnWaterLevel(overlayCenter - right * halfSize);
            var rightLevel = GetDrawnWaterLevel(overlayCenter + right * halfSize);
            var edgeElevation = camera.transform.position.y - (leftLevel + rightLevel) / 2;
            // Roll so that the top edge rises across the view as much as the water does.
            var roll = Mathf.Atan2(rightLevel - leftLevel, 2 * halfSize * cosPitch);
            underwaterOverlay.transform.rotation =
                Quaternion.LookRotation(camera.transform.forward, Vector3.up) *
                Quaternion.Euler(0, 0, roll * Mathf.Rad2Deg);
            // The middle of the top edge sits the overlay's half height above its center, less the pitch and roll.
            var verticalOffset = -edgeElevation - halfSize * cosPitch * Mathf.Cos(roll);
            // Horizontal compensation to keep the overlay at that distance in front of the camera
            var horizontalCompensation = (distance - facing.y * verticalOffset) / headingSqrMagnitude;
            underwaterOverlay.transform.position =
                camera.transform.position +
                new Vector3(horizontalCompensation * facing.x, verticalOffset, horizontalCompensation * facing.z);
            underwaterOverlay.SetActive(true);
        }

        // The height of the water surface where it is drawn this frame. Like WaterVolume.GetWaterSurface(), but on
        // the clock the water material is given (s_waterTime), which is smoothed and so can run slightly apart from
        // the day time GetWaterSurface() uses, shifting the waves by a few centimeters.
        private float GetDrawnWaterLevel(Vector3 position)
        {
            if (Floating.GetWaterLevel(position, ref lastWaterVolume) <= -10000f || lastWaterVolume == null)
            {
                return Player.m_localPlayer.m_waterLevel;
            }
            var volume = lastWaterVolume;
            float wave = 0;
            if (volume.m_useGlobalWind)
            {
                float depth = volume.Depth(position);
                if (depth != 0)
                {
                    float waveFactorBig = 1f - (float)WorldGenerator.DeepNorthWaveFade(position.x, position.z);
                    // The time the water material was actually given in Update(). WaterVolume.s_waterTime itself moves on
                    // again in MonoUpdaters' LateUpdate() and FixedUpdate().
                    float waterTime =
                        volume.m_waterSurface != null ?
                        volume.m_waterSurface.material.GetFloat(WaterVolume.s_shaderWaterTime) :
                        WaterVolume.s_waterTime;
                    wave = volume.CalcWave(position, depth, waterTime, 1f, waveFactorBig);
                }
            }
            return volume.transform.position.y + wave + volume.m_surfaceOffset;
        }

        // The height of the water surface, waves included, right at the given point. Player.m_waterLevel is sampled at
        // the body instead, which is off by however much the waves differ between there and the eyes.
        private static float GetWaterLevel(Vector3 position)
        {
            var level = Floating.GetLiquidLevel(position, 1f, LiquidType.Water);
            return level > -10000f ? level : Player.m_localPlayer.m_waterLevel;
        }

        private static float GetUnderwaterness(float elevation)
        {
            return Mathf.InverseLerp(0, -0.0625f, elevation);
        }
    }
}
